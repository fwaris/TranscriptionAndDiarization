namespace TranscriptionClient
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.DependencyInjection
open TranscriptionInterop
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open System.IO
open Renci.SshNet


module Connection =     
    let mutable private _dispatch = fun (msg:ClientMsg) -> ()    

    let ssh = lazy(
        let client = new SshClient(Config.node,"user01","don$X7")
        client.Connect()
        let localPort = new ForwardedPortLocal("127.0.0.1", 8080u, Config.node, 5000u);
        client.AddForwardedPort(localPort)
        client)
    
    let private connection = lazy(
        let connection =
            HubConnectionBuilder()
                .AddJsonProtocol(fun o -> o.PayloadSerializerOptions <- Ser.serOptions())
                .WithAutomaticReconnect()
                .WithUrl($"http://{Config.node}:{Config.port}/hub")  
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

    let disconnect() = 
        connection.Value.StopAsync() |> ignore
        if ssh.IsValueCreated then ssh.Value.Dispose()


