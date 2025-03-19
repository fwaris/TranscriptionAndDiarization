module FastTranscriber
open System
open System.IO
open System.Diagnostics

type TranscriptionJob = 
    {
        transcriberPath : string
        ffmpegPath : string
        inputFolder : string
        outputFolder : string
        diarize : bool
        mutable processId : int option
        mutable isCancelled : bool
    }

let ensureDir dir = if Directory.Exists dir |> not then Directory.CreateDirectory dir |> ignore

let processFile (job:TranscriptionJob) (file:string) = 
    async {
        if not job.isCancelled then 
            let args=  
                if job.diarize then 
                    $""" "{file}" --language English --model medium --output_dir {job.outputFolder} --output_format vtt --diarize pyannote_v3.1"""
                else 
                    $""" "{file}" --language English --model medium --output_dir {job.outputFolder} --output_format vtt"""
            let pi = ProcessStartInfo()
            pi.FileName <- job.transcriberPath
            pi.Arguments <- args
            use p = new Process()
            p.StartInfo <- pi
            let r = p.Start()
            job.processId <- Some p.Id
            if not r then failwith "Unable to start process"
            do! p.WaitForExitAsync() |> Async.AwaitTask
    }
    
let getFiles inputFolder outputFolder = 
    ensureDir outputFolder
    let mp4Files = Directory.GetFiles(inputFolder,"*.mp4")
    let ttFiles = Directory.GetFiles(outputFolder,"*.vtt")
    let ttFilesNames = ttFiles |> Seq.map Path.GetFileNameWithoutExtension |> set
    mp4Files 
    |> Seq.filter (fun p -> ttFilesNames.Contains (Path.GetFileNameWithoutExtension p) |> not)
    |> Seq.toList
    
let processLoop (job:TranscriptionJob) maxCount= 
    let rec loop count = 
        async {
            let files = getFiles job.inputFolder job.outputFolder 
            for f in files do
                printfn $"processing {f}"
                do! processFile job f
            if List.isEmpty files && count < maxCount then 
                printfn $"waiting ... {count}"
                do! Async.Sleep 10000            
                return! loop (count+1)
        }
    async {
        match! Async.Catch (loop 0) with 
        | Choice1Of2 _ -> Log.info $"Done processLoop {job.inputFolder}"
        | Choice2Of2 ex -> Log.exn(ex,$"processLoop {job.inputFolder}")
    }

let testRun() = 
    let exePath = @"E:\s\transcription\Faster-Whisper-XXL\faster-whisper-xxl.exe"
    let inputFolder = @"E:\s\transcription\test"
    let outputFolder = @"E:\s\transcription\test"
    let job =   
        {
            transcriberPath = exePath
            ffmpegPath = ""
            inputFolder = inputFolder
            outputFolder = outputFolder
            diarize = true
            processId = None
            isCancelled = false
        }
    processLoop job 1
    |> Async.RunSynchronously

(*
testRun()
*)

