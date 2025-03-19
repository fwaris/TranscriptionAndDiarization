namespace TranscriptionServiceHost
open System
open TranscriptionInterop
open FastTranscriber

module Config = 
    open Microsoft.Extensions.Configuration
    open System.IO

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
    mutable status : JobsState
    transcriptionJob : TranscriptionJob
}

type JobAction = 
    | AddJob of Job 
    | Queue of string 
    | Cancel of string 
    | Done of string 
    | NumbJobs of AsyncReplyChannel<int>
    | SyncStatus of (ITranscriptionClient*string list * AsyncReplyChannel<SrvJobStatus list>)
