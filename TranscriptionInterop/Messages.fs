namespace TranscriptionInterop
open System
open System.Threading.Tasks
open Microsoft.AspNetCore.SignalR.Client


type ITranscriptionService =
    abstract member Echo : string -> Task<string>
    abstract member CreateJob : unit -> Task<string*string>
    abstract member StartJob : string -> Task
    abstract member CancelJob : string -> Task

type ITranscriptionClient =
    abstract member JobsInQueue : int -> Task
    abstract member JobsStartResult : string -> Task
    abstract member JobCancelResult : string -> Task
    abstract member JobDone : string -> Task

type ClientMsg = JobStarted of string  | Jobs of int | JobDone of string | JobCancelled of string | Notification of string

type TranscriptionClient(hub:HubConnection,dispatch:ClientMsg->unit ) = 
    
    let d1 = hub.On<int>("JobsInQueue",Jobs>>dispatch)
    let d2 = hub.On<string>("JobsStartResult",JobStarted>>dispatch)
    let d3 = hub.On<string>("JobDone",JobDone>>dispatch)
    let d4 = hub.On<string>("JobCancelResult",JobCancelled>>dispatch)
    
    interface ITranscriptionService with        
        member this.CancelJob(jobId: string): Task = 
            hub.InvokeAsync("CancelJob",jobId)            
        member  this.CreateJob(): Task<string*string> =             
            hub.InvokeAsync<string*string>("CreateJob")
            
        member this.Echo(arg1: string): Task<string> = 
            hub.InvokeAsync<string>(arg1)
        member this.StartJob(jobId: string): Task = 
            hub.InvokeAsync("StartJob",jobId)
        
    interface IDisposable with 
        member this.Dispose (): unit = [d1;d2;d3;d4] |> List.iter _.Dispose()
