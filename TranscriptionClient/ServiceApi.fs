namespace TranscriptionClient
open TranscriptionInterop
open Microsoft.AspNetCore.SignalR.Client
open System.Threading.Tasks

module ServiceApi = 
    let checkConnect (hub:HubConnection) dispatch = 
        task {
            if hub.State <> HubConnectionState.Connected then
                try
                    dispatch (ConnectionState Connecting)
                    do! hub.StartAsync()
                    dispatch (ConnectionState Connected)
                    hub.On<int>("JobsInQueue",Jobs>>dispatch)               |> ignore
                    hub.On<ClientUpdate>("JobState",Status>>dispatch)   |> ignore
                with ex ->
                    dispatch (ConnectionState Disconnected)
                    return raise ex
        }

    let invoke<'T> (model:Model) dispatch (f:ITranscriptionService->Task<'T>) =
        task {
            try
                let hub = model.connection
                do! checkConnect hub dispatch
                let client = TranscriptionClient(hub)
                return! f client
            with ex ->
                 return raise ex
        }
