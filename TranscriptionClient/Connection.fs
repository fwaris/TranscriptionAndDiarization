namespace TranscriptionClient
open Microsoft.AspNetCore.SignalR.Client
open TranscriptionInterop
open System.Threading.Tasks

module Connection =     
    let create (dispatch:ClientMsg->unit) = 
        let connection =
            HubConnectionBuilder()
                .WithUrl("http://localhost:5000/hub")
                .Build()

        connection.add_Closed(fun exn -> dispatch (Notification $"Close error {exn.Message}"); Task.CompletedTask)
        connection.add_Reconnected(fun m -> dispatch (Notification $"Reconnect: {m}"); Task.CompletedTask)
        connection.add_Reconnecting(fun exn -> dispatch (Notification $"reconnecting: {exn.Message}"); Task.CompletedTask)

        connection
