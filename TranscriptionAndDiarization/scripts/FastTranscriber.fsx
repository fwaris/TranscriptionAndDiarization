open System
open System.IO
open System.Diagnostics

let ensureDir dir = if Directory.Exists dir |> not then Directory.CreateDirectory dir |> ignore

let processFile exePath outFolder (file:string) = 
    async {
        let pi = ProcessStartInfo()
        pi.FileName <- exePath
        pi.Arguments <- $""" "{file}" --language English --model medium --output_dir {outFolder} --output_format vtt --diarize pyannote_v3.1"""
        use p = new Process()
        p.StartInfo <- pi
        let r = p.Start()
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
    
let processLoop exePath maxCount inputFolder outputFolder = 
    let rec loop count = 
        async {
            let files = getFiles inputFolder outputFolder 
            for f in files do
                printfn $"processing {f}"
                do! processFile exePath outputFolder f
            if List.isEmpty files && count < maxCount then 
                printfn $"waiting ... {count}"
                do! Async.Sleep 10000            
                return! loop (count+1)
        }
    async {
        match! Async.Catch (loop 0) with 
        | Choice1Of2 _ -> printfn "Done"
        | Choice2Of2 ex -> printfn "%A" ex
    }

let testRun() = 
    let exePath = @"E:\s\transcription\Faster-Whisper-XXL\faster-whisper-xxl.exe"
    let inputFolder = @"E:\s\transcription\test"
    let outputFolder = @"E:\s\transcription\test"
    processLoop exePath 1 inputFolder outputFolder
    |> Async.RunSynchronously

(*
testRun()
*)

