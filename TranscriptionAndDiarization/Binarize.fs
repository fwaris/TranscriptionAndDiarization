module Binarizer
open System

type Binarize(onset: float, offset: float, logScale: bool, minDurationOn: float, minDurationOff: float) =
    // Initialize the parameters
    member this.Onset = onset
    member this.Offset = offset
    member this.LogScale = logScale
    member this.MinDurationOn = minDurationOn
    member this.MinDurationOff = minDurationOff

    member this.Apply(scores: (float * float) list) =
        let mutable activeRegions = []
        let mutable isActive = false
        let mutable regionStart: float option = None

        for (time, score) in scores do
            let adjustedScore = if this.LogScale then Math.Exp(score) else score

            if not isActive && adjustedScore > this.Onset then
                isActive <- true
                regionStart <- Some time
            elif isActive && adjustedScore < this.Offset then
                isActive <- false
                match regionStart with
                | Some start when time - start >= this.MinDurationOn ->
                    activeRegions <- (start, time) :: activeRegions
                    regionStart <- None
                | _ -> regionStart <- None

        // Handle case where signal remains active till the end
        match regionStart with
        | Some start when isActive && (List.last scores |> fst) - start >= this.MinDurationOn ->
            activeRegions <- (start, (List.last scores |> fst)) :: activeRegions
        | _ -> ()

        // Merge regions separated by less than minDurationOff
        let mergedRegions =
            activeRegions
            |> List.rev // Ensure chronological order
            |> List.fold (fun acc region ->
                match acc with
                | [] -> [region]
                | (prevStart, prevEnd) :: tail when fst region - prevEnd < this.MinDurationOff ->
                    (prevStart, snd region) :: tail
                | _ -> region :: acc
            ) []

        mergedRegions |> List.rev

// Example Usage
let scores = [
    (0.0, 0.1)
    (0.1, 0.6)
    (0.2, 0.7)
    (0.3, 0.4)
    (0.4, 0.2)
]

let binarizer = Binarize(onset = 0.5, offset = 0.3, logScale = false, minDurationOn = 0.1, minDurationOff = 0.1)
let activeRegions = binarizer.Apply(scores)

activeRegions |> List.iter (fun (start, end_) ->
    printfn "Active from %f to %f seconds." start end_
)
