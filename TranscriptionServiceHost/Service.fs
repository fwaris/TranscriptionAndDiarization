namespace TranscriptionServiceHost
open System
open System.IO
open System.Threading.Tasks
open System.Threading
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
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
    ClientId : string
    Client : ITranscriptionClient
    CreateTime : DateTime
    JobPath : string
    Diarize : bool
    IdenifySpeaker : bool 
    mutable isCancelled : bool
    mutable processId : int option
}

type JobAction = AddJob of Job | Queue of string | Cancel of string | Done of string | NumbJobs of AsyncReplyChannel<int>

module Jobs = 
    let jobQueue = System.Threading.Channels.Channel.CreateUnbounded<Job>()
    
    let upddateClient (job:Job) (status:JobsState) = 
        task {
            try 
                do! job.Client.JobState {jobId=job.JobId; status=status}
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
            ClientId = clientId
            CreateTime = DateTime.Now
            Client = client
            JobPath = jobPath
            Diarize = diarize
            IdenifySpeaker = identifySpeaker            
            isCancelled = false
            processId = None
        }        

    let private startJob cfg job = 
        Log.info $"job started {job}"
        ()

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
                do! Async.Sleep 1000
                do! upddateClient job (Transcribing (Path.GetFileName f))
        }

    let transcribe job = 
        task{
            let files = Directory.GetFiles(job.JobPath,"*.mp4")
            for f in files do
                do! Async.Sleep 1000
                do! upddateClient job (Transcribing (Path.GetFileName f))                
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
                    do! upddateClient job JobsState.``Done server processing``                    
            with ex -> 
                Log.exn(ex,"Jobs.processJob")
                do! upddateClient job (JobsState.Error ex.Message)                
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
                clearJobFolder job.JobPath |> ignore
            | None ->
                Log.warn $"Job {job.JobId} not started"
        }

    //process jobs in the queue till service is stopped
    let processor = 
        jobQueue.Reader.ReadAllAsync()
        |> AsyncSeq.ofAsyncEnum
        |> AsyncSeq.iterAsync (fun job -> 
            async {
                try                   
                    do! processJob job |> Async.AwaitTask
                with ex -> 
                    Log.exn(ex,"Jobs.processor")
            })
        |> Async.Start
    
    let agent = MailboxProcessor.Start(fun inbox -> 
        let mutable _jobs = Map.empty
        let add job = _jobs <- _jobs |> Map.add job.JobId job
        let remove jobId = _jobs <- _jobs |> Map.remove jobId            
        async {
            while true do 
                try 
                    match! inbox.Receive() with
                    | AddJob job -> 
                        add job
                        Log.info $"Job {job.JobId} added"
                    | Queue jobId -> 
                        match _jobs |> Map.tryFind jobId with
                        | Some job -> 
                            if not (jobQueue.Writer.TryWrite(job)) then
                                upddateClient job (JobsState.Error "Unable to queue job") |> ignore
                                remove job.JobId
                                Log.warn $"Unable to queue {jobId}. Job removed."
                            else
                                Log.info $"Job {jobId} queued"
                        | None -> 
                            Log.warn $"Job {jobId} not found"
                    | Cancel jobId ->
                        match _jobs |> Map.tryFind jobId with
                        | Some job -> 
                            job.isCancelled <- true
                            terminateJob job |> ignore
                            remove jobId
                            upddateClient job JobsState.Cancelled |> ignore                            
                            Log.info $"Job {jobId} cancelled"
                            clearJobFolder job.JobPath |> ignore
                        | None -> 
                            Log.warn $"Job {jobId} not found so cannot cancel"
                    | Done jobId -> 
                        match _jobs |> Map.tryFind jobId with
                        | Some job -> 
                            clearJobFolder job.JobPath |> ignore
                            remove jobId
                            Log.info $"Job {jobId} done"
                        | None ->
                            Log.warn $"Job {jobId} not found so cannot clear"
                    | NumbJobs rep ->
                        rep.Reply (_jobs.Count)
                with ex ->
                    Log.exn(ex,"Jobs.agent")
        })

type TranscriberHub(cfg:IConfiguration) = 
    inherit Hub<ITranscriptionClient>()

    member this.CreateJob(j) = (this :> ITranscriptionService).CreateJob j
    member this.Echo(arg1: string) = (this :> ITranscriptionService).Echo(arg1)
    member this.QueueJob(jobId: string) = (this :> ITranscriptionService).QueueJob(jobId)
    member this.CancelJob(jobId: string) = (this :> ITranscriptionService).CancelJob(jobId)
    member this.ClearJob(jobId: string) = (this :> ITranscriptionService).ClearJob(jobId)

    interface ITranscriptionService with            
        member this.Echo(arg1: string): Threading.Tasks.Task<string> = 
            task{return arg1}

        member this.CreateJob(jobCreation:JobCreation): Threading.Tasks.Task<JobCreationResult> =             
            let path = Config.jobPath cfg (Jobs.newId())
            let diarize = jobCreation.diarize
            let identifySpeaker = jobCreation.identifySpeaker
            let j = Jobs.create path this.Context.ConnectionId this.Clients.Caller diarize identifySpeaker
            Jobs.agent.Post(AddJob j)
            task {return {jobId = j.JobId; jobPath = j.JobPath}}
            
        member this.QueueJob(jobId: string): Threading.Tasks.Task = 
            task {                
                Jobs.agent.Post(Queue jobId)
                do! this.Clients.Caller.JobState {jobId=jobId; status = JobsState.``In service queue``}
            }

        member this.ClearJob(jobId: string): Threading.Tasks.Task =
            task {
                Jobs.agent.Post(Done jobId)
                do! this.Clients.Caller.JobState {jobId = jobId; status=JobsState.Done}
            }

        member this.CancelJob (jobId: string): Task = 
            task {
                Jobs.agent.Post(Cancel jobId)
                do! this.Clients.Caller.JobState {jobId =jobId; status=JobsState.Cancelled}
            }
        
type Service(hub:IHubContext<TranscriberHub, ITranscriptionClient>, cfg:IConfiguration, lggr : ILogger<Log.TranscriberLog>) = 
    inherit BackgroundService()
    do Log._log <- lggr
    let cfg = {ExePath = cfg.["ExePath"]}    
    let ctx = new CancellationTokenSource()

    let startNotification() =
        async {
            while not ctx.IsCancellationRequested do 
                try 
                    let! numJobs = Jobs.agent.PostAndAsyncReply(fun rep -> NumbJobs rep)
                    do! hub.Clients.All.JobsInQueue (numJobs) |> Async.AwaitTask
                with ex -> 
                    Log.exn(ex,"startNotification")
                do! Async.Sleep 5000
        }

    override this.Dispose(): unit =
        ()    
    override this.ExecuteAsync(stoppingToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        Task.CompletedTask
    override this.ExecuteTask: System.Threading.Tasks.Task =
        Task.CompletedTask
    override this.StartAsync(cancellationToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        startNotification() |> Async.Start
        Task.CompletedTask        
    override this.StopAsync(cancellationToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        ctx.Cancel()
        Jobs.jobQueue.Writer.Complete()
        Task.CompletedTask

