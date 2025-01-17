module Vtt
open System
open System.IO
open System.Text.RegularExpressions

type Utterance = {
    Speaker : string
    From: TimeSpan
    To: TimeSpan
    Speech : string
}
let regTs1 = Regex(@"(\d\d):(\d\d).(\d\d\d)\s*-->\s*(\d\d):(\d\d)\.(\d\d\d)")
let regTs2 = Regex(@"(\d\d):(\d\d):(\d\d).(\d\d\d)\s*-->\s*(\d\d):(\d\d):(\d\d)\.(\d\d\d)")

let regSpeech = Regex(@"\[(\w*)\]\s*:\s*\b(.*)")

let (|Time|_|) (s:string) = 
    let ms = regTs1.Match(s)
    if ms.Success then 
        let gs = ms.Groups
        let m1,s1,f1 = int gs.[1].Value, int gs.[2].Value, int gs.[3].Value
        let m2,s2,f2 = int gs.[4].Value, int gs.[5].Value, int gs.[6].Value
        let t1 = TimeSpan(0,0,m1,s1,f1)
        let t2 = TimeSpan(0,0,m2,s2,f2)
        Some(t1,t2)
    else 
        let ms = regTs2.Match(s)
        if ms.Success then 
            let gs = ms.Groups
            let h1,m1,s1,f1 = int gs.[1].Value, int gs.[2].Value, int gs.[3].Value, int gs.[4].Value
            let h2,m2,s2,f2 = int gs.[5].Value, int gs.[6].Value, int gs.[7].Value, int gs.[8].Value
            let t1 = TimeSpan(0,h1,m1,s1,f1)
            let t2 = TimeSpan(0,h2,m2,s2,f2)
            Some(t1,t2)
        else 
            None
let (|Speech|_|) (s:string) =
    let ms = regSpeech.Match(s)
    if ms.Success then 
        let gs = ms.Groups
        let speaker = gs.[1].Value
        let speech = gs.[2].Value
        Some(speaker,speech)
    else 
        None

let parseVttLines (xs:string seq) = 
    xs 
    |> Seq.pairwise
    |> Seq.choose(fun (l1,l2) -> 
        match l1 with 
        | Time (t1,t2) -> 
            match l2 with 
            | Speech (spkr,spch) -> 
                {
                    Speaker = spkr
                    Speech = spch
                    From = t1
                    To = t2                
                }
                |> Some
            | _ -> None
        | _ ->  None)
    |> Seq.toList

let parseVtt (path:string) = File.ReadLines path |> parseVttLines 
