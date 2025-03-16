namespace TranscriptionClient
open Microsoft.AspNetCore.SignalR.Client
open TranscriptionInterop
open System.Threading.Tasks
open System
open Avalonia.FuncUI

module Connection =     
    let create (dispatch:ClientMsg->unit) = 
        let connection =
            HubConnectionBuilder()
                .WithUrl("http://localhost:5000/hub")
                .Build()

        connection.add_Closed(fun exn -> dispatch (ConnectionState Disconnected); Task.CompletedTask)
        connection.add_Reconnected(fun m -> dispatch (ConnectionState Connected); Task.CompletedTask)
        connection.add_Reconnecting(fun exn -> dispatch (ConnectionState Reconnecting); Task.CompletedTask)

        connection

type Job = {JobId:string; Path:string; StartTime:DateTime; Status:JobsState; Diarize : bool; IdentifySpeaker : bool}
    with member this.IsRunning = this.Status <> Done || this.Status <> Cancelled || not this.Status.IsError

type Model = {
    jobsInQueue : IWritable<int>
    localFolder : IWritable<string>
    diarize : IWritable<bool>
    tagSpeaker : IWritable<bool>
    jobs : IWritable<Job list> 
    connectionState :IWritable<ConnectionState>
    showNotification : string->string->unit
    update : (unit->unit) -> unit
}

