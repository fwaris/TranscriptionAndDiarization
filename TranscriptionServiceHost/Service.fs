namespace TranscriptionServiceHost
open System
open System.Threading.Tasks
open System.Threading
open Microsoft.AspNetCore.SignalR
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open TranscriptionInterop

module Config = 
    let jobPath(cfg:IConfiguration) (jobId:string) = $"""{cfg.["JobsPath"]}/{jobId}"""
    let exePath (cfg:IConfiguration) = cfg.["ExePath"]

type Job = {
    JobId : string
    ClientId : string
    CreateTime : DateTime
}

type JobStartResult = Started | StartError of string 
type JobCancelResult = Cancelled | CancelError of string

type JobAction = 
    | Start of (IConfiguration*string*AsyncReplyChannel<JobStartResult>)
    | Cancel of (IConfiguration*string*AsyncReplyChannel<JobCancelResult>)

module Jobs = 
    let newId() = 
        Guid.NewGuid().ToByteArray() 
        |> Convert.ToBase64String 
        |> Seq.takeWhile (fun c -> c <> '=') 
        |> Seq.map (function '/' -> 'a' | c -> c)
        |> Seq.toArray 
        |> String

    let mutable private jobs : Map<string,Job> = Map.empty
    let addJob j = jobs <- jobs |> Map.add j.JobId j
    let deleteJob id = jobs <- jobs |> Map.remove id

    let create clientId = 
        let job = {JobId = newId(); ClientId = clientId; CreateTime = DateTime.Now }
        addJob job
        job

    let getJobs() = jobs

    let private startJob cfg job = 
        Log.info $"job started {job}"
        ()

    let agent = MailboxProcessor.Start(fun inbox -> 
        async {
            while true do 
                try 
                    match! inbox.Receive() with 
                    | Start (cfg,j,rc) -> rc.Reply(Started)
                    | Cancel (cfg,j,rc) -> deleteJob j; rc.Reply(Cancelled)
                with ex ->
                    Log.exn(ex,"Jobs.agent")
        })

type TranscriberHub(cfg:IConfiguration) = 
    inherit Hub<ITranscriptionClient>()

    interface ITranscriptionService with            
        member this.CreateJob(): Threading.Tasks.Task<string*string> = 
            let j = Jobs.create this.Context.ConnectionId
            let path = Config.jobPath cfg j.JobId
            task {return (j.JobId,path)}
            
        member this.Echo(arg1: string): Threading.Tasks.Task<string> = 
            task{return arg1}

        member this.StartJob(jobId: string): Threading.Tasks.Task = 
            task {
                match! Jobs.agent.PostAndAsyncReply(fun rc -> Start(cfg,jobId,rc)) with
                | Started -> do! this.Clients.Caller.JobsStartResult("Started")
                | StartError msg -> do! this.Clients.Caller.JobsStartResult(msg)
            }

        member this.CancelJob (jobId: string): Task = 
            task {
                match! Jobs.agent.PostAndAsyncReply(fun rc -> Cancel(cfg,jobId,rc)) with
                | Cancelled -> do! this.Clients.Caller.JobCancelResult("Cancelled")
                | CancelError msg -> do! this.Clients.Caller.JobCancelResult(msg)
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
                    do! hub.Clients.All.JobsInQueue (Jobs.getJobs().Count) |> Async.AwaitTask
                with ex -> 
                    Log.exn(ex,"startNotification")
                do! Async.Sleep 5000
        }

    override this.Dispose(): unit =
        ()    override this.ExecuteAsync(stoppingToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        Task.CompletedTask
    override this.ExecuteTask: System.Threading.Tasks.Task =
        Task.CompletedTask
    override this.StartAsync(cancellationToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        startNotification() |> Async.Start
        Task.CompletedTask        
    override this.StopAsync(cancellationToken: System.Threading.CancellationToken): System.Threading.Tasks.Task =
        ctx.Cancel()
        Task.CompletedTask

