namespace TranscriptionAndDiarization

module Pgm =

    open System
    open Microsoft.ML.OnnxRuntime.Tensors
    open System.IO
    open FSharp.Control
    open Microsoft.ML.OnnxRuntime
    open NAudio.Wave
    open System.Buffers
    let natveOnnx = @"c:\Users\fwaris\.nuget\packages\microsoft.ml.onnxruntime.gpu.windows\1.20.1\runtimes\win-x64\native\onnxruntime.dll"
    //System.Runtime.InteropServices.NativeLibrary.Load(natveOnnx)

    let videoFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.mp4"
    let vttFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.vtt"
    let waveFile = EmbedAudio.convertTo16KhzWav videoFn
    let uttrs = Vtt.parseVtt vttFn
    let bySpeaker = uttrs |> List.groupBy (fun x ->x.Speaker )
    let speakerLongSpeech = 
        bySpeaker
        |> List.map(fun (spkr,spchs) -> spkr, spchs |> List.filter(fun u -> (u.To - u.From).TotalSeconds > 10.))

    let PETER = "SPEAKER_32"

    let peterSamples = 
        speakerLongSpeech 
        |> List.filter (fun (s,_) -> s = PETER)
        |> List.map(fun (spkr,spchs) -> spkr, Env.sampleN 5 spchs)

    let MAX_TS = TimeSpan.FromSeconds 10.

    let peterEmbeddings = 
        peterSamples
        |> List.collect snd
        |> List.map(fun u -> u.From,u.To)
        |> EmbedAudio.getAudioSamples MAX_TS waveFile
        |> List.map (fun (_,_,m) -> m)
        |> List.map ((EmbedAudio.toOnnxInput "waveform") >> EmbedAudio.getEmbeddings)

    let nonPeterEmbeddings() =
        speakerLongSpeech
        |> List.filter(fun (s,_) -> s <> PETER)
        |> List.map(fun (s,spchs) -> 
            printfn $"processing {s}"
            s, 
            spchs 
            |> List.map(fun u -> u.From, u.To) 
            |> EmbedAudio.getAudioSamples MAX_TS waveFile 
            |> List.map (fun (f,t,m) -> printfn $"   - processing From:{f} - To:{t}"; m)
            |> List.map ((EmbedAudio.toOnnxInput "waveform") >> EmbedAudio.getEmbeddings))

    let testDist() =
        let npe = nonPeterEmbeddings()
        EmbedAudio.Dist.cosineDistance peterEmbeddings.[0] (snd npe.[3]).[0]
    
    testDist()