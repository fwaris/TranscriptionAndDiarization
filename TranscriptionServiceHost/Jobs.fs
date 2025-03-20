namespace TranscriptionServiceHost
open System
open System.IO
open Microsoft.Extensions.Configuration
open FSharp.Control
open TranscriptionInterop
open FastTranscriber
open IdentitySpeaker

module Jobs =     
    let updateClient (job:Job) (status:JobsState) numJobs = 
        task {
            try 
                Log.info $"updating client {job.JobId} to {status}"
                do! job.Client.JobState {jobId=job.JobId; status=status}
                match numJobs with 
                | Some n ->  do! job.Client.JobsInQueue n 
                | None        -> ()
                if job.TranscriptionJob.isCancelled then 
                    job.status <- JobsState.Cancelled
                else
                    job.status <- status
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

    let idenficationJob (cfg:IConfiguration) jobPath =          
        {
            SpeakerName = Config.speakerName cfg
            EmbeddingsPath = Config.speakerEmbeddings cfg
            InputFolder = jobPath
            ModelPath = Config.audioEmbeddingsModelPath cfg
        }

    let transcriptionJob (cfg:IConfiguration) jobPath diarize = 
        {
            transcriberPath = Config.transcriberPath cfg
            ffmpegPath = Config.ffmpegPath cfg
            inputFolder = jobPath
            outputFolder = jobPath
            diarize = diarize
            processId = None
            isCancelled = false                
        }

    let create jobId jobPath clientId client diarize identifySpeaker txnJob idJob =  
        {
            JobId = jobId
            CreateTime = DateTime.Now
            JobPath = jobPath
            Diarize = diarize
            IdenifySpeaker = identifySpeaker            
            Client = client
            status = JobsState.Created
            TranscriptionJob = txnJob
            IdentificationJob = idJob
        }        

    let clearJobFolder jobPath = 
        task {
            try 
                System.IO.Directory.Delete(jobPath,true)
                Log.info $"job folder {jobPath} deleted"
            with ex -> 
                Log.exn(ex,"Jobs.clearJobFolder")
        }
