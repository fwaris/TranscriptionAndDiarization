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
open FastTranscriber

type TranscriberHub(cfg:IConfiguration) = 
    inherit Hub<ITranscriptionClient>()

    member this.CreateJob(j) = (this :> ITranscriptionService).CreateJob j
    member this.Echo(arg1: string) = (this :> ITranscriptionService).Echo(arg1)
    member this.QueueJob(jobId: string) = (this :> ITranscriptionService).QueueJob(jobId)
    member this.CancelJob(jobId: string) = (this :> ITranscriptionService).CancelJob(jobId)
    member this.ClearJob(jobId: string) = (this :> ITranscriptionService).ClearJob(jobId)
    member this.SyncJobs(req) = (this :> ITranscriptionService).SyncJobs(req)

    interface ITranscriptionService with            
        member this.Echo(arg1: string): Threading.Tasks.Task<string> = 
            task{return arg1}

        member this.CreateJob(jobCreation:JobCreation): Threading.Tasks.Task<JobCreationResult> =             
            let jobId = Jobs.newId()
            let path = Config.jobPath cfg jobId
            let txnJob = {
                transcriberPath = Config.transcriberPath cfg
                ffmpegPath = Config.ffmpegPath cfg
                inputFolder = path
                outputFolder = path
                diarize = jobCreation.diarize
                processId = None
                isCancelled = false                
            }
            Log.info $"CreateJob - {this.Context.ConnectionId}"            
            let diarize = jobCreation.diarize
            let identifySpeaker = jobCreation.identifySpeaker
            let j = Jobs.create 
                            jobId 
                            path 
                            this.Context.ConnectionId 
                            this.Clients.Caller 
                            diarize 
                            identifySpeaker 
                            txnJob
            JobAgent.agent.Post(AddJob j)
            task {return {jobId = j.JobId; jobPath = j.JobPath}}
            
        member this.QueueJob(jobId: string): Threading.Tasks.Task = 
            task {                
                Log.info $"QueueJob - {this.Context.ConnectionId}"
                JobAgent.agent.Post(Queue jobId)
            }

        member this.ClearJob(jobId: string): Threading.Tasks.Task =
            task {
                Log.info $"ClearJob - {this.Context.ConnectionId}"
                JobAgent.agent.Post(JobAction.Done jobId)                
            }

        member this.CancelJob (jobId: string): Task = 
            task {
                Log.info $"CancelJob - {this.Context.ConnectionId}"
                JobAgent.agent.Post(Cancel jobId)                
            }

        //this is normally used to sync the client with the server after the client has been restarted
        member this.SyncJobs(req) : Task<JobSyncResponse> =
            task {
                Log.info $"SyncJobs - {this.Context.ConnectionId}"
                let! updates = JobAgent.agent.PostAndAsyncReply(fun rep -> SyncStatus(this.Clients.Caller,req.jobIds,rep))
                return {jobsStatus = updates}
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
                    let! numJobs = JobAgent.agent.PostAndAsyncReply(fun rep -> NumbJobs rep)
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
        let processor = JobProcessor.processor //MockJobProcessor.processor  
        JobAgent.createProcessor ctx.Token processor
        Task.CompletedTask        
    override this.StopAsync(cancellationToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        ctx.Cancel()
        JobAgent.jobQueue.Writer.Complete()
        Task.CompletedTask

