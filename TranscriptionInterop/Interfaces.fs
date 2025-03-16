namespace TranscriptionInterop
open System
open System.Threading.Tasks
open Microsoft.AspNetCore.SignalR.Client
open System.Text.Json
open System.Text.Json.Serialization
module Ser =

    let serOptions() = 
        let o = JsonSerializerOptions(JsonSerializerDefaults.General)
        o.WriteIndented <- true
        o.ReadCommentHandling <- JsonCommentHandling.Skip
        JsonFSharpOptions.Default()
            //.WithUnionEncoding(JsonUnionEncoding.NewtonsoftLike) //consider for future as provides better roundtrip support
            .AddToJsonSerializerOptions(o)        
        o

type JobsState = 
    | Created                        //job created in service, waiting for data to be uploaded
    | Uploading of string            //client uploading video file
    | ``In service queue``           //data uploaded, waiting for service to start processing the job
    | Transcribing of string         //service is transcribing file file
    | Diarizing of string            //service is diarizing file
    | ``Speaker tagging`` of string  //service is identifying speakers
    | ``Done server processing``     //service has finished processing the job
    | Downloading of string          //client downloading .vtt file
    | Done                           //client has downloaded all vtt files
    | Cancelled                      //job was cancelled
    | Cancelling                     //job is being cancelled 
    | Error of string                

type JobCreation = {diarize:bool; identifySpeaker:bool}
type JobCreationResult = {jobId:string; jobPath:string}
type ConnectionState = Connected | Connecting | Disconnected | Reconnecting
type ClientUpdate = {jobId:string; status:JobsState}
type ClientMsg = Status of ClientUpdate | Jobs of int | ConnectionState of ConnectionState


type ITranscriptionService =
    abstract member Echo : string -> Task<string>
    abstract member CreateJob : JobCreation -> Task<JobCreationResult>
    abstract member QueueJob : string -> Task
    abstract member CancelJob : string -> Task
    abstract member ClearJob : string -> Task

type ITranscriptionClient =
    abstract member JobsInQueue : int -> Task
    abstract member JobState : ClientUpdate -> Task

type TranscriptionClient(hub:HubConnection) = 
        
    interface ITranscriptionService with        
        member this.Echo(arg1: string): Task<string> = 
            hub.InvokeAsync<string>(arg1)
        member  this.CreateJob jobCreaton : Task<JobCreationResult> =
            hub.InvokeAsync<JobCreationResult>("CreateJob",jobCreaton)
            
        member this.QueueJob(jobId: string): Task = 
            hub.InvokeAsync("StartJob",jobId)

        member this.ClearJob(jobId: string): Task = 
            hub.InvokeAsync("ClearJob",jobId)

        member this.CancelJob(jobId: string): Task = 
            hub.InvokeAsync("CancelJob",jobId)            
        