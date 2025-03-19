namespace TranscriptionServiceHost
open FSharp.Control
open TranscriptionInterop

module JobAgent =

    let jobQueue = System.Threading.Channels.Channel.CreateUnbounded<Job>()

    let processJob processor job = 
        task {
            try 
                if not job.transcriptionJob.isCancelled then 
                    do! processor.transcribe job
                if not job.transcriptionJob.isCancelled && job.Diarize && job.IdenifySpeaker then 
                    do! processor.identifySpeaker job
                if not job.transcriptionJob.isCancelled then
                    do! Jobs.updateClient job JobsState.``Done server processing``  None                  
            with ex -> 
                Log.exn(ex,"Jobs.processJob")
                do! Jobs.updateClient job (JobsState.Error ex.Message) None 
                Jobs.clearJobFolder job.JobPath |> ignore
        }
    
    let terminateJob job  =
        task {
            match job.transcriptionJob.processId with 
            | Some pid -> 
                try 
                    Log.info $"terminating job {job.JobId}"
                    System.Diagnostics.Process.GetProcessById(pid).Kill()
                with ex -> 
                   Log.exn(ex,"Jobs.terminateJob")
            | None ->
                Log.warn $"Job {job.JobId} not started"
            Jobs.clearJobFolder job.JobPath |> ignore
        }

    //process jobs in the queue till service is stopped
    let createProcessor token processor = 
        let comp = async {
            let task = 
                jobQueue.Reader.ReadAllAsync()
                |> AsyncSeq.ofAsyncEnum
                |> AsyncSeq.iterAsync (fun job -> 
                    async {
                        try                   
                            Log.info $"processing job {job.JobId}"
                            do! processJob processor job |> Async.AwaitTask
                        with ex -> 
                            Log.exn(ex,"Jobs.processor")
                    })
            match! Async.Catch task with
            | Choice1Of2 _ -> Log.info "processor stopped"
            | Choice2Of2 ex -> Log.exn(ex,"Jobs.processor")
        }        
        Async.Start(comp,token)
        

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
                                Jobs.updateClient job (JobsState.Error "Unable to queue job") (numJobs()) |> ignore
                                remove job.JobId
                                Log.warn $"Unable to queue {jobId}. Job removed."
                            else
                                Jobs.updateClient job JobsState.``In service queue`` (numJobs()) |> ignore
                                Log.info $"Job {jobId} queued"
                        | None -> 
                            Log.warn $"Job {jobId} not found"
                    | Cancel jobId ->
                        match find jobId with
                        | Some job -> 
                            job.transcriptionJob.isCancelled <- true
                            terminateJob job |> ignore
                            remove jobId
                            Jobs.updateClient job JobsState.Cancelled (numJobs()) |> ignore                            
                            Log.info $"Job {jobId} cancelled"
                         | None -> 
                            Log.warn $"Job {jobId} not found so cannot cancel"
                    | JobAction.Done jobId -> 
                        match find jobId with
                        | Some job -> 
                            Jobs.clearJobFolder job.JobPath |> ignore
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
