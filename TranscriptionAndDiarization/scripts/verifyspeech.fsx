#load "packages.fsx"
open System
open FsOpenAI.Vision
open Plotly.NET
open System.IO
open FSharp.Control

type SpeakerInfo = {Speaker : string; Samples: Vtt.Utterance list; Embeddings : float32 array list}

type EmbeddingsInfo = 
    {
        Mp4 : string
        Vtt : string
        SpeakerInfos : SpeakerInfo list
    }

let refVideoFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.mp4"
let refVttFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.vtt"
let refPETER = "SPEAKER_32"
let folder = @"E:\s\transription_fw\input"

let filesToProcess =
    Seq.append 
        (Directory.GetFiles(folder,"*.vtt"))
        (Directory.GetFiles(folder,"*.mp4"))
    |> Seq.groupBy(fun x -> Path.GetFileNameWithoutExtension x)
    |> Seq.map (fun xs-> 
        let fns = 
            xs 
            |> snd 
            |> Seq.sortBy (fun y -> Path.GetExtension y) 
            |> Seq.toList
        if fns.Length <> 2 then failwith $"expecting .mp4 & .vtt, got {fns}"
        fns.[0],fns.[1])
//    |> Seq.filter(fun (mp4,vtt) -> mp4 <> refVideoFn)        
    |> Seq.toList

let MIN_SPEECH_TIME = TimeSpan.FromSeconds 5.0

let getEmbeddings (samples:Vtt.Utterance list) waveFile = 
    samples
    |> List.map(fun u -> u.From,u.To)
    |> EmbedAudio.getAudioSamples MIN_SPEECH_TIME waveFile
    |> List.map (fun (_,_,m) -> m)
    |> List.map ((EmbedAudio.toOnnxInput "waveform") >> EmbedAudio.getEmbeddings)

let getSamples (mp4,vtt) = 
    printfn  $"processing {mp4} ..."
    let wavefile = EmbedAudio.convertTo16KhzWav true mp4
    let uttrs = Vtt.parseVtt vtt
    let bySpeaker = uttrs |> List.groupBy (fun x ->x.Speaker )
    let speakerLongSpeech = 
        bySpeaker
        |> List.map(fun (spkr,spchs) -> 
            spkr, 
            spchs |> List.filter(fun u -> (u.To - u.From) > MIN_SPEECH_TIME))
    {
        Mp4 = mp4
        Vtt = vtt
        SpeakerInfos = 
            speakerLongSpeech 
            |> List.map (fun (spkr,samples) -> 
                {
                    Speaker = spkr
                    Samples = samples
                    Embeddings = getEmbeddings samples wavefile
                })
    }
    
let refEmbeddings =
    getSamples(refVideoFn,refVttFn).SpeakerInfos
    |> List.choose (fun inf -> if inf.Speaker = refPETER then Some inf.Embeddings else None )
    |> List.collect id

let otherEmbeddings = filesToProcess |> List.map getSamples

open Plotly.NET
let plotDists refEmbeddings (otherEmbeddings:EmbeddingsInfo) =
    let refDists = EmbedAudio.allPairsDist refEmbeddings refEmbeddings
    let otherDists = 
        otherEmbeddings.SpeakerInfos
        |> List.filter (fun x ->x.Embeddings.Length > 0)
        |> List.map(fun s -> s.Speaker, EmbedAudio.allPairsDist refEmbeddings s.Embeddings )
    [["PETER-to-PETER", refDists]; otherDists]
    |> List.mapi(fun i xs -> 
        xs 
        |> Chart.Point
        |> Chart.withTraceInfo (if i=0 then "Avg. Dist between Peter Samples" else "Avg. Dist. between Peter samples and other speaker samples")
        |> Chart.withMarkerStyle(Size=10))
    |> Chart.combine
    |> Chart.withSize(800,400)
    |> Chart.withTitle $"{otherEmbeddings.Mp4}<br>Avg. 'Distances' of Peter audio samples to themselves vs.<br>Peter with other Speakers"
    |> Chart.show

let plotAllDists() =
    otherEmbeddings
    |> List.iter(fun o -> plotDists refEmbeddings o)

(*
plotAllDists()
*)

let nearestToRefSpeakers = 
    otherEmbeddings
    |> List.map(fun oe -> 
        let minDistSpeaker = 
            oe.SpeakerInfos
            |> List.filter (fun e -> e.Embeddings |> List.isEmpty |> not)
            |> List.minBy(fun o -> EmbedAudio.allPairsDist refEmbeddings o.Embeddings)
        oe.Mp4,minDistSpeaker)

let getImageSamples (mp4,spkr:SpeakerInfo) = 
    printfn $"getting samples from {mp4} for speaker tag {spkr.Speaker}"
    let timeStamps = spkr.Samples |> List.map(_.From) |> Env.sampleN 3
    Video.getFramesAtTimes mp4 timeStamps
    |> AsyncSeq.choose id
    |> AsyncSeq.iter Video.showImage
    |> Async.Start

let replaceSpeakerTag outputFolder (mp4,(speakerInfo:SpeakerInfo)) =
    Env.ensureDir outputFolder
    let vttFile = EmbedAudio.changeExt mp4 ".vtt"
    let text = File.ReadAllText vttFile
    let text' = text.Replace(speakerInfo.Speaker,"PETER")
    let outFile = Path.Combine(outputFolder, Path.GetFileName(vttFile))
    File.WriteAllText(outFile,text')

nearestToRefSpeakers.[0] |> getImageSamples
nearestToRefSpeakers.[1] |> getImageSamples
nearestToRefSpeakers.[2] |> getImageSamples
nearestToRefSpeakers.[3] |> getImageSamples
nearestToRefSpeakers.[4] |> getImageSamples

nearestToRefSpeakers
|> List.iter (replaceSpeakerTag  @"e:\s\transription_fw\tagged")



