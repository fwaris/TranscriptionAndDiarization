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
                with ex ->
                    dispatch (ConnectionState Disconnected)
                    return raise ex
        }

    let invoke<'T> (model:Model) (f:ITranscriptionService->Task<'T>) =
        task {
            try
                let dispatch = model.mailbox.Writer.TryWrite>>ignore
                let hub = Connection.get dispatch
                do! checkConnect hub dispatch
                let client = TranscriptionClient(hub)
                return! f client
            with ex ->
                 return raise ex
        }

    let ping model = 
        task {
            return! invoke<string> model (fun c -> c.Echo("test"))
        }
