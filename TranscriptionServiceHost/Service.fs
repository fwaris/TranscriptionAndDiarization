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
            Log.info $"CreateJob - {this.Context.ConnectionId}"            
            let jobId = Jobs.newId()
            let path = Config.jobPath cfg jobId
            let diarize = jobCreation.diarize
            let identifySpeaker = jobCreation.identifySpeaker
            let j = Jobs.create jobId path this.Context.ConnectionId this.Clients.Caller diarize identifySpeaker
            Jobs.agent.Post(AddJob j)
            task {return {jobId = j.JobId; jobPath = j.JobPath}}
            
        member this.QueueJob(jobId: string): Threading.Tasks.Task = 
            task {                
                Log.info $"QueueJob - {this.Context.ConnectionId}"
                Jobs.agent.Post(Queue jobId)
            }

        member this.ClearJob(jobId: string): Threading.Tasks.Task =
            task {
                Log.info $"ClearJob - {this.Context.ConnectionId}"
                Jobs.agent.Post(JobAction.Done jobId)                
            }

        member this.CancelJob (jobId: string): Task = 
            task {
                Log.info $"CancelJob - {this.Context.ConnectionId}"
                Jobs.agent.Post(Cancel jobId)                
            }

        //this is normally used to sync the client with the server after the client has been restarted
        member this.SyncJobs(req) : Task<JobSyncResponse> =
            task {
                Log.info $"SyncJobs - {this.Context.ConnectionId}"
                let! updates = Jobs.agent.PostAndAsyncReply(fun rep -> SyncStatus(this.Clients.Caller,req.jobIds,rep))
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

