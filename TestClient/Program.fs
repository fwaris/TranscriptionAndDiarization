module Pgm 
open System.Threading.Tasks
open Microsoft.AspNetCore.SignalR
open Microsoft.AspNetCore.SignalR.Client

let connection = 
    HubConnectionBuilder()
        .WithUrl("http://localhost:5000/hub")
        .Build()

connection.add_Closed(fun exn -> task{printfn $"close {exn.Message}"})
connection.add_Reconnected(fun m -> task{printfn $"reconnected {m}"})
connection.add_Reconnecting(fun exn -> task{ printfn $"reconnecting: {exn.Message}"})
        
connection.StartAsync() |> Async.AwaitTask |> Async.RunSynchronously

//connection.InvokeAsync("Echo","data1") |> Async.AwaitTask |> Async.RunSynchronously |> printfn "%A"


System.Console.ReadLine() |> ignore

