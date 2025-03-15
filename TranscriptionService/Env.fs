[<AutoOpen>]
module Env
open System.Threading.Tasks
open System
open System.IO
open System.IO

type ServiceConfig = {
    ExePath : string
}

let inline runT (t:Task<'a>) = t.Wait(); t.Result
let inline runA t = t |> Async.RunSynchronously

let ensureDir dir = if Directory.Exists dir |> not then Directory.CreateDirectory dir |> ignore


let sampleN n xs = 
    let ys = Seq.toList xs
    if n >= ys.Length then 
        ys
    else 
        let rng = Random()
        let rec loop acc =
            if Set.count acc < n then 
                loop (Set.add (rng.Next(n)) acc)
            else
                acc
        let s = loop Set.empty                
        s 
        |> Seq.map(fun i -> ys.[i])
        |> Seq.toList

let sample frac xs = 
    let rng = Random()
    xs 
    |> Seq.filter(fun _ -> rng.NextDouble() < frac)


