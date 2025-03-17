namespace TranscriptionServiceHost
open System
open System.IO
open Microsoft.Extensions.Configuration
open FSharp.Control
open TranscriptionInterop

module Config = 

    let jobPath(cfg:IConfiguration) (jobId:string) = 
        let rootPath = cfg.["JobsPath"]
        let path = $"{rootPath}/{jobId}".Replace("\\","/")
        try 
            if Directory.Exists(path) |> not then Directory.CreateDirectory(path) |> ignore
        with ex -> 
            Log.exn(ex,"Config.jobPath")
        path

    let transcriberPath (cfg:IConfiguration) = cfg.["TranscriberPath"]

    let ffmpegPath (cfg:IConfiguration) = cfg.["FfmpegPath"]

type Job = {
    JobId : string    
    CreateTime : DateTime
    JobPath : string
    Diarize : bool
    IdenifySpeaker : bool 
    mutable Client : ITranscriptionClient
    mutable isCancelled : bool
    mutable processId : int option
    mutable status : JobsState
}

type JobAction = 
    | AddJob of Job 
    | Queue of string 
    | Cancel of string 
    | Done of string 
    | NumbJobs of AsyncReplyChannel<int>
    | SyncStatus of (ITranscriptionClient*string list * AsyncReplyChannel<ClientUpdate list>)

module Jobs = 
    let jobQueue = System.Threading.Channels.Channel.CreateUnbounded<Job>()
    
    let updateClient (job:Job) (status:JobsState) numJobs = 
        task {
            try 
                Log.info $"updating client {job.JobId} to {status}"
                do! job.Client.JobState {jobId=job.JobId; status=status}
                match numJobs with 
                | Some n ->  do! job.Client.JobsInQueue n 
                | None        -> ()
                if job.isCancelled then 
                    job.status <- JobsState.Cancelled
                else
                    job.status <- status
            with ex -> 
                Log.exn(ex,"Jobs.upddateClient")
        }

    let newId() = 
        Guid.NewGuid().ToByteArray() 
        |> Convert.ToBase64String 
        |> Seq.takeWhile (fun c -> c <> '=') 
        |> Seq.map (function '/' -> 'a' | c -> c)
        |> Seq.toArray 
        |> String

    let create jobPath clientId client diarize identifySpeaker= 
        {
            JobId = newId()
            CreateTime = DateTime.Now
            JobPath = jobPath
            Diarize = diarize
            IdenifySpeaker = identifySpeaker            
            isCancelled = false
            Client = client
            processId = None
            status = JobsState.Created
        }        

    let private clearJobFolder jobPath = 
        task {
            try 
                System.IO.Directory.Delete(jobPath,true)
                Log.info $"job folder {jobPath} deleted"
            with ex -> 
                Log.exn(ex,"Jobs.clearJobFolder")
        }

    let transcribeAndDiarize job = 
        task {
            let files = Directory.GetFiles(job.JobPath,"*.mp4")
            for f in files do
                do! updateClient job (Transcribing (Path.GetFileName f)) None
                do! Async.Sleep 30000
        }

    let transcribe job = 
        task{
            let files = Directory.GetFiles(job.JobPath,"*.mp4")
            for f in files do
                do! updateClient job (Transcribing (Path.GetFileName f)) None 
                do! Async.Sleep 30000
        }

    let identifySpeaker job = task{return()}

    let processJob job = 
        task {
            try 
                if not job.isCancelled then 
                    if job.Diarize then 
                        do! transcribeAndDiarize job
                    else
                        do! transcribe job                
                if not job.isCancelled then 
                    do! identifySpeaker job
                if not job.isCancelled then
                    do! updateClient job JobsState.``Done server processing``  None                  
            with ex -> 
                Log.exn(ex,"Jobs.processJob")
                do! updateClient job (JobsState.Error ex.Message) None 
                clearJobFolder job.JobPath |> ignore
        }
    
    let terminateJob job  =
        task {
            match job.processId with 
            | Some pid -> 
                try 
                    Log.info $"terminating job {job.JobId}"
                    System.Diagnostics.Process.GetProcessById(pid).Kill()
                with ex -> 
                   Log.exn(ex,"Jobs.terminateJob")
            | None ->
                Log.warn $"Job {job.JobId} not started"
            clearJobFolder job.JobPath |> ignore
        }

    //process jobs in the queue till service is stopped
    let processor = 
        jobQueue.Reader.ReadAllAsync()
        |> AsyncSeq.ofAsyncEnum
        |> AsyncSeq.iterAsync (fun job -> 
            async {
                try                   
                    Log.info $"processing job {job.JobId}"
                    do! processJob job |> Async.AwaitTask
                with ex -> 
                    Log.exn(ex,"Jobs.processor")
            })
        |> Async.Start
    
    //agent to handle job actions; also holds agent state
    let agent = MailboxProcessor.Start(fun inbox -> 
        let mutable _jobs = Map.empty
        let add job = _jobs <- _jobs |> Map.add job.JobId job
        let remove jobId = _jobs <- _jobs |> Map.remove jobId   
        let find jobId = _jobs |> Map.tryFind jobId         
        let numJobs() = Some _jobs.Count        
        async {
            while true do 
                try 
                    match! inbox.Receive() with
                    | AddJob job -> 
                        job.status <- JobsState.Created
                        add job
                        Log.info $"Job {job.JobId} added"
                    | Queue jobId -> 
                        match find jobId with
                        | Some job -> 
                            if not (jobQueue.Writer.TryWrite(job)) then
                                updateClient job (JobsState.Error "Unable to queue job") (numJobs()) |> ignore
                                remove job.JobId
                                Log.warn $"Unable to queue {jobId}. Job removed."
                            else
                                updateClient job JobsState.``In service queue`` (numJobs()) |> ignore
                                Log.info $"Job {jobId} queued"
                        | None -> 
                            Log.warn $"Job {jobId} not found"
                    | Cancel jobId ->
                        match find jobId with
                        | Some job -> 
                            job.isCancelled <- true
                            terminateJob job |> ignore
                            remove jobId
                            updateClient job JobsState.Cancelled (numJobs()) |> ignore                            
                            Log.info $"Job {jobId} cancelled"
                         | None -> 
                            Log.warn $"Job {jobId} not found so cannot cancel"
                    | Done jobId -> 
                        match find jobId with
                        | Some job -> 
                            clearJobFolder job.JobPath |> ignore
                            remove jobId
                            Log.info $"Job {jobId} done"
                        | None ->
                            Log.warn $"Job {jobId} not found so cannot clear"
                    | NumbJobs rep ->
                        rep.Reply (_jobs.Count)
                    | SyncStatus(client,jobIds,rep) -> 
                        let updates = jobIds |> List.map (fun jobId -> 
                            match find jobId with
                            | Some job -> 
                                job.Client <- client
                                {jobId = jobId; status = job.status }
                            | None -> 
                                {jobId = jobId; status = JobsState.``Not found in service queue``}
                        )
                        rep.Reply updates
                with ex ->
                    Log.exn(ex,"Jobs.agent")
        })
