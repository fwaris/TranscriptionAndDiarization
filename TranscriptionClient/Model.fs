namespace TranscriptionClient
open Microsoft.AspNetCore.SignalR.Client
open Microsoft.Extensions.DependencyInjection
open TranscriptionInterop
open System.Threading.Tasks
open System
open Avalonia.FuncUI

type Job = {JobId:string; Path:string; StartTime:DateTime; Status:JobsState; Diarize : bool; IdentifySpeaker : bool; RemoteFolder:string}
    with member this.IsRunning() =
            match this.Status with
            | Error _ | Done | Cancelled -> false
            | _ -> true

type ConnectionState = Connected | Connecting | Disconnected | Reconnecting

type ClientMsg = 
    | Status of ClientUpdate 
    | Jobs of int 
    | ConnectionState of ConnectionState
    | UpdateJobs of Job list

type Model = {
    mutable runningJobs  : Ref<Job list>
    jobsInQueue : IWritable<int>
    localFolder : IWritable<string>
    diarize : IWritable<bool>
    tagSpeaker : IWritable<bool>
    uiJobs : IWritable<Job list> 
    connectionState :IWritable<ConnectionState>
    showNotification : string->string->unit
    invokeOnUIThread : (unit->unit) -> unit
    connection : HubConnection
}

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
        connection)

    let get (dispatch:ClientMsg->unit) = 
        _dispatch <- dispatch
        connection.Value

    let disconnect() = connection.Value.StopAsync() |> ignore

module Data =
    let jobs : Ref<Job list> = ref []