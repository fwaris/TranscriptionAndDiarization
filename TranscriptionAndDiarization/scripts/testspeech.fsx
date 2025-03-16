#load "packages.fsx"
open System
open Plotly.NET


let videoFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.mp4"
let vttFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.vtt"
let waveFile = EmbedAudio.convertTo16KhzWav true videoFn
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

let nonPeterEmbeddings =
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
    |> List.filter (fun (_,xs) -> xs.Length > 0)


let allPairsDist l1 l2 = 
    List.allPairs l1 l2 
    |> List.filter (fun (a,b) -> a <> b)
    |> List.map(fun (a,b) -> EmbedAudio.Dist.cosineDistance a b)
    |> List.average


let peterDists = allPairsDist peterEmbeddings peterEmbeddings

let peterToOthers =
    nonPeterEmbeddings
    |> List.map (fun (s,es)  -> s, allPairsDist peterEmbeddings es)

open Plotly.NET
let plotDists() =
    [["PETER-to-PETER", peterDists];peterToOthers]
    |> List.mapi(fun i xs -> 
        xs 
        |> Chart.Point
        |> Chart.withTraceInfo (if i=0 then "Avg. Dist between Peter Samples" else "Avg. Dist. between Peter samples and other speaker samples")
        |> Chart.withMarkerStyle(Size=10))
    |> Chart.combine
    |> Chart.withSize(800,400)
    |> Chart.withTitle "Avg. 'Distances' of Peter audio samples to themselves vs.<br>Peter with other Speakers"
    |> Chart.show

plotDists()
