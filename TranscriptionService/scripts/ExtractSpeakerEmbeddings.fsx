#load "packages.fsx"
open System
open System.IO
open MBrace.FsPickler

(*
Extract and save speaker embeddings from a reference video file
- required: corresponding vtt file that contains the speaker tags and timestamps
- required: the speaker tag for which the embeddings
*)

let refVideoFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.mp4"
let refVttFn = @"e:\s\transription_fw\test\FP&SC Dir+ Meeting-20240318 1727-1.vtt"
let refSpeaker = "SPEAKER_32"
let EmbeddingOutFile =  @"E:\transcription_test\speaker_embeddings.xml"
let f = Path.GetDirectoryName EmbeddingOutFile
if Directory.Exists f |> not then Directory.CreateDirectory f |> ignore
type SpeakerInfo = {Speaker : string; Samples: Vtt.Utterance list; Embeddings : float32 array list}

type EmbeddingsInfo = 
    {
        Mp4 : string
        Vtt : string
        SpeakerInfos : SpeakerInfo list
    }


let pyannote = @"e:\s\models\pyannote-embedding-onnx\model.onnx"
let model = lazy(EmbedAudio.pyannoteModel pyannote)

let MIN_SPEECH_TIME = TimeSpan.FromSeconds 5.0

let getEmbeddings (samples:Vtt.Utterance list) waveFile = 
    samples
    |> List.map(fun u -> u.From,u.To)
    |> EmbedAudio.getAudioSamples MIN_SPEECH_TIME waveFile
    |> List.map (fun (_,_,m) -> m)
    |> List.map ((EmbedAudio.toOnnxInput "waveform") >> (EmbedAudio.getEmbeddings model.Value))

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
