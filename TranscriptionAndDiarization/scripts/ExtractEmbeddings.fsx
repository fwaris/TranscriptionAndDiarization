#load "packages.fsx"
open System
open FsOpenAI.Vision
open Plotly.NET
open System.IO
open FSharp.Control
open MBrace.FsPickler

type SpeakerInfo = {Speaker : string; Samples: Vtt.Utterance list; Embeddings : float32 array list}

type EmbeddingsInfo = 
    {
        Mp4 : string
        Vtt : string
        SpeakerInfos : SpeakerInfo list
    }

let refVideoFn = @"E:\s\transcriberTest\teamcall.mp4"
let refVttFn = @"E:\s\transcriberTest\teamcall.vtt"
let refSpeaker = "SPEAKER_00"
let EmbeddingOutFile =  @"E:\transcription_test\speaker_embeddings.xml"
let f = Path.GetDirectoryName EmbeddingOutFile
if Directory.Exists f |> not then Directory.CreateDirectory f |> ignore


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
    |> List.choose (fun inf -> if inf.Speaker = refSpeaker then Some inf.Embeddings else None )
    |> List.collect id


let saveEmbeddings() = 
    use str = File.Create EmbeddingOutFile
    FsPickler.CreateXmlSerializer().Serialize(str,refEmbeddings)

let loadEmbeddings() = 
    use str = File.OpenRead EmbeddingOutFile
    FsPickler.CreateXmlSerializer().Deserialize<(float32 []) list>(str)

(*
saveEmbeddings()
let embs = loadEmbeddings()
*)
