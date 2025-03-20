module IdentitySpeaker
open System
open System.IO
open MBrace.FsPickler
open FSharp.Control
open Microsoft.ML.OnnxRuntime

type SpeakerInfo = {Speaker : string; Samples: Vtt.Utterance list; Embeddings : float32 array list}

type IdentificationJob = 
    {
        SpeakerName : string
        EmbeddingsPath : string
        InputFolder : string
        ModelPath : string
    }

type EmbeddingsInfo = 
    {
        Mp4 : string
        Vtt : string
        SpeakerInfos : SpeakerInfo list
    }

let filesToProcess folder =
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

let getEmbeddings model (samples:Vtt.Utterance list) waveFile = 
    samples
    |> List.map(fun u -> u.From,u.To)
    |> EmbedAudio.getAudioSamples MIN_SPEECH_TIME waveFile
    |> List.map (fun (_,_,m) -> m)
    |> List.map ((EmbedAudio.toOnnxInput "waveform") >> EmbedAudio.getEmbeddings model)

let getSamples model (mp4,vtt) = 
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
                    Embeddings = getEmbeddings model samples wavefile
                })
    }
    
let nearestToRefSpeakers refEmbeddings otherEmbeddings = 
    otherEmbeddings
    |> List.map(fun oe -> 
        let minDistSpeaker = 
            oe.SpeakerInfos
            |> List.filter (fun e -> e.Embeddings |> List.isEmpty |> not)
            |> List.minBy(fun o -> EmbedAudio.allPairsDist refEmbeddings o.Embeddings)
        oe,minDistSpeaker)

let replaceSpeakerTag speaker (vttFile,(speakerInfo:SpeakerInfo)) =
    let text = File.ReadAllText vttFile
    let text' = text.Replace(speakerInfo.Speaker,speaker)
    File.WriteAllText(vttFile,text')

let loadRefEmbeddings (path:string) = 
    use str = File.OpenRead path
    FsPickler.CreateXmlSerializer().Deserialize<(float32 []) list>(str)

let tagSpeaker (job:IdentificationJob) = 
    use model = EmbedAudio.pyannoteModel job.ModelPath
    let refEmbeddings = loadRefEmbeddings job.EmbeddingsPath
    let files = filesToProcess job.InputFolder
    for fs in files do
        let samples = getSamples model fs
        let (oe,speaker) = nearestToRefSpeakers refEmbeddings [samples] |> List.head
        replaceSpeakerTag job.SpeakerName (oe.Vtt,speaker)  
