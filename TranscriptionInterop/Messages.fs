namespace TranscriptionInterop
open System
open System.Threading.Tasks
open Microsoft.AspNetCore.SignalR.Client

type JobsState = 
    ``Waiting to be queued`` 
    | ``In service queue`` 
    | Transcribing 
    | Diarizing 
    | ``Speaker tagging``
    | Done 
    | Cancelled 
    | Cancelling
    | Error of string 

type ITranscriptionService =
    abstract member Echo : string -> Task<string>
    abstract member CreateJob : unit -> Task<string*string>
    abstract member StartJob : string -> Task
    abstract member CancelJob : string -> Task

type ITranscriptionClient =
    abstract member JobsInQueue : int -> Task
    abstract member JobState : string*JobsState -> Task

type ConnectionState = Connected | Disconnected | Reconnecting
type ClientMsg = Status of string*JobsState | Jobs of int | ConnectionState of ConnectionState

type TranscriptionClient(hub:HubConnection,dispatch:ClientMsg->unit ) = 
    
    let d1 = hub.On<int>("JobsInQueue",Jobs>>dispatch)
    let d2 = hub.On<string*JobsState>("JobState",Status>>dispatch)
    
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
        member this.Dispose (): unit = [d1;d2] |> List.iter _.Dispose()