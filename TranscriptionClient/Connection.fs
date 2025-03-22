namespace TranscriptionClient
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.DependencyInjection
open TranscriptionInterop
open System.Threading.Tasks
open Renci.SshNet

module Connection =
    let mutable private _dispatch = fun (msg:ClientMsg) -> ()
    let mutable private _ssh : SshClient = Unchecked.defaultof<_>

    let ssh() =
        let node = Config.node.Value
        let port = Config.port.Value
        let password = Config.sshPassword.Value
        let sshUser = Config.sshUser.Value
        let client = new SshClient(Config.node.Value,Config.sshUser.Value, Config.sshPassword.Value)
        client.Connect()
        let localPort = new ForwardedPortLocal("127.0.0.1", uint Config.sshLocalPort.Value, Config.node.Value, uint Config.port.Value);
        localPort.Exception.Add(fun ex -> MessageMailbox.channel.Writer.TryWrite (Notify ($"{ex.Exception.Message}")) |> ignore)
        client.AddForwardedPort(localPort)
        localPort.Start()
        client

    let private connection = lazy(
        if Config.useSSH.Value then
            try
                _ssh <- ssh()
            with ex ->
                failwith "Unable to establish SSH connection"
        let url = if Config.useSSH.Value then $"http://localhost:{Config.sshLocalPort.Value}/hub" else $"http://{Config.node.Value}:{Config.port.Value}/hub"
        let connection =
            HubConnectionBuilder()
                .AddJsonProtocol(fun o -> o.PayloadSerializerOptions <- Ser.serOptions())
                .WithAutomaticReconnect()
                .WithUrl(url)
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
        MessageMailbox.channel.Writer.TryComplete() |> ignore
        connection.Value.StopAsync() |> ignore
        if _ssh <> Unchecked.defaultof<_> then
            try
                try
                    _ssh.ForwardedPorts |> Seq.iter (fun p -> p.Stop())
                    _ssh.Disconnect()
                finally
                    _ssh.Dispose()
                    _ssh <- Unchecked.defaultof<_>
            with ex -> ()
