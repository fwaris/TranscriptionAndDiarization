namespace TranscriptionClient
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.DependencyInjection
open TranscriptionInterop
open System.Threading.Tasks


module Connection =     
    let mutable private _dispatch = fun (msg:ClientMsg) -> ()
    
    let private connection = lazy(
        let connection =
            HubConnectionBuilder()
                .AddJsonProtocol(fun o -> o.PayloadSerializerOptions <- Ser.serOptions())
                .WithAutomaticReconnect()
                .WithUrl("http://localhost:5000/hub")  
                .Build()                
        connection.add_Closed(fun exn -> _dispatch (ConnectionState Disconnected); Task.CompletedTask)
        connection.add_Reconnected(fun m -> _dispatch (ConnectionState Connected); Task.CompletedTask)
        connection.add_Reconnecting(fun exn -> _dispatch (ConnectionState Reconnecting); Task.CompletedTask)
        connection.On<int>("JobsInQueue",ServiceJobCount>>_dispatch)           |> ignore
        connection.On<SrvJobStatus>("JobState",FromService>>_dispatch)   |> ignore
        connection)

    let get (dispatch:ClientMsg->unit) = 
        _dispatch <- dispatch
        connection.Value

    let disconnect() = connection.Value.StopAsync() |> ignore

