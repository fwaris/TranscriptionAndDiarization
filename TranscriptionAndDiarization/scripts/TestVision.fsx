#load "packages.fsx"

open System
open System.IO
open System.Text.RegularExpressions
open FSharp.Control

let testVideo() = 
    let frames = 
        FsOpenAI.Vision.Video.getFrames @"E:\s\transription_fw\input\FP&SC All-Team Meeting-20241114 1800-1.mp4" 20
        |> AsyncSeq.toBlockingSeq
        |> Seq.toList
        |> List.choose id

    frames |> List.iter FsOpenAI.Vision.Video.showImage

(*
testVideo()
*)

let vttFn = @"E:\s\transription_fw\input\FP&SC Director +-20241107 2001-1.vtt"

let videoFn = @"E:\s\transription_fw\input\FP&SC Director +-20241107 2001-1.mp4"

let uttrs = Vtt.parseVtt vttFn
let bySpeaker = uttrs |> List.groupBy (fun x ->x.Speaker )

let speakerSamples = 
    bySpeaker
    |> List.map(fun (spkr,spchs) -> spkr, Env.sampleN 5 spchs)
    |> List.map(fun (spkr,smpls) -> spkr, smpls |> List.map (fun u-> (u.From + u.To) / 2.0) )

let speakerFrames = 
    speakerSamples
    |> List.map (fun (spkr,times) -> spkr, FsOpenAI.Vision.Video.getFramesAtTimes videoFn times |> AsyncSeq.toBlockingSeq |> Seq.toList)

let spkr1 = speakerFrames |> List.find (fun (s,_) -> s = "SPEAKER_02")
let spkr1Imgs = spkr1 |> snd |> List.choose id
FsOpenAI.Vision.Video.showImage spkr1Imgs.[4]


let peterImage = @"E:\s\transription_fw\input\peter.png"

open FsOpenAI.Vision

let imageUri (imageBytes:string) = $"data:image/jpeg;base64,{imageBytes}"
let img1Content = File.ReadAllBytes peterImage |> Convert.ToBase64String |> imageUri
let img2Content = (snd spkr1).[4].Value |> Convert.ToBase64String |> imageUri
//let txContent = "Compare the person in the first image to the speaker in the second image and determine if the two are the same person. Note the speaker appears only in a part of the image"
let txContent = "Describe the two images"

let ep = Uri @"https://api.openai.com/v1/chat/completions"

open FsOpenAI.Vision.VisionApi
let cs : Content list = [ImageContent img1Content; ImageContent img2Content; TextContent txContent ]
let m = Message("user",cs)
let p = Payload([m], model="gpt-4o")

let openAIKey = Environment.GetEnvironmentVariable("OPENAI_KEY")

let answ = (VisionApi.processVision ep openAIKey p).Result

answ.Value.choices.Head.message.content