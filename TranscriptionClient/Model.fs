namespace TranscriptionClient
open TranscriptionInterop
open System
open System.Threading.Channels

type Job = {JobId:string; Path:string; StartTime:DateTime; Status:JobsState; Diarize : bool; IdentifySpeaker : bool; RemoteFolder:string}
    with
        member this.IsRunning() =
            match this.Status with
            | Error _ | Done | Cancelled -> false
            | _ -> true
        member this.SetStatus status = {this with Status=status}

type ConnectionState = Connected | Connecting | Disconnected | Reconnecting

exception JobException of string * string

type JobCancelResult = Cancel | Remove | Ignore

type ClientMsg =
    | Initialize
    | Connect
    | FromService of SrvJobStatus
    | ServiceJobCount of int
    | ConnectionState of ConnectionState
    | Notify of string
    | Exn of exn
    | UpsertJobs of Job list
    | RemoveJob of string
    | TryCancelJob of string
    | TryCancelJobResult of string * JobCancelResult
    | StartUpload of string
    | DoneUpload of string
    | StartDownload of string
    | DoneDownload of Job
    | CreateJob
    | JobCreated of Job
    | JobError of exn
    | Diarize of bool
    | TagSpeaker of bool
    | LocalFolder of string
    | OpenFolder
    | SubmitJob
    | Nop of unit

module MessageMailbox =
    let channel = Channel.CreateBounded<ClientMsg>(30)

type Model = {
    jobs : Job list
    jobsInQueue : int
    connectionState : ConnectionState
    localFolder : string
    diarize : bool
    tagSpeaker : bool
    mailbox : System.Threading.Channels.Channel<ClientMsg>
}
with static member Default win =
            {
                jobs=[]
                jobsInQueue=0
                connectionState=Disconnected
                localFolder = null
                diarize = true
                tagSpeaker = true
                mailbox=MessageMailbox.channel
            }

