namespace TranscriptionServiceHost
open FSharp.Control
open TranscriptionInterop
open System.IO
open System.Threading.Tasks

type JobProcessor = {
    transcribe: Job -> Task<unit>
    identifySpeaker: Job -> Task<unit>
}

module JobProcessor =

    let transcribe job = 
        task {
            let files = FastTranscriber.getFiles job.transcriptionJob.inputFolder job.transcriptionJob.outputFolder
            for f in files do
                do! Jobs.updateClient job (Transcribing (Path.GetFileName f)) None
                do! FastTranscriber.processFile job.transcriptionJob f
        }


    let identifySpeaker job = task{return()}

    let processor = {
        transcribe = transcribe
        identifySpeaker = identifySpeaker
    }

module MockJobProcessor =

    let transcribe job = 
        task {
            let files = Directory.GetFiles(job.JobPath,"*.mp4")
            for f in files do
                do! Jobs.updateClient job (Transcribing (Path.GetFileName f)) None
                do! Async.Sleep 30000
        }

    let identifySpeaker job = task{return()}

    let processor = {
        transcribe        = transcribe
        identifySpeaker   = identifySpeaker
    }
    