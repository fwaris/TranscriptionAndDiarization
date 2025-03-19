namespace TranscriptionClient
open System
open Elmish
open Avalonia.Media
open Avalonia
open Avalonia.Controls
open FSharp.Control
open TranscriptionInterop

module U =
    open Avalonia.Threading
    open System.IO
    open Avalonia.FuncUI.Hosts
    //notificatons
    let mutable private notificationManager : Notifications.WindowNotificationManager = Unchecked.defaultof<_>

    let initNotfications(window) =
        notificationManager <- Notifications.WindowNotificationManager(window)
        notificationManager.MaxItems <- 1
        notificationManager.Position <- Avalonia.Controls.Notifications.NotificationPosition.BottomLeft
        notificationManager.Margin <- Thickness(10)

    let showNotification title text =
        if notificationManager <> Unchecked.defaultof<_> then
            let notification = Avalonia.Controls.Notifications.Notification(
                title,
                text,
                Avalonia.Controls.Notifications.NotificationType.Information
            )
            notificationManager.Show(notification)

    let invokeOnUIThread (f:unit->unit) = Dispatcher.UIThread.InvokeAsync f |> ignore

    let getFolder win =
        async{
            match! TranscriptionClient.Dialogs.openFileDialog win with
            | Some f -> return f
            | None -> return null
        }    

    let submitJob model=
        if String.IsNullOrWhiteSpace model.localFolder then 
            Notify "Please select a folder containing video files" 
        elif model.jobs |> List.exists(fun j -> j.Path = model.localFolder ) then
            Notify $"There is an existing job for the folder '({model.localFolder}'"                        
        elif Directory.Exists model.localFolder |> not then
            Notify $"Folder does not exist '{model.localFolder}'"
        else 
            let mp4s = Directory.GetFiles(model.localFolder,"*.mp4") 
            if mp4s.Length = 0 then
                Notify $"No mp4 files found in '{model.localFolder}'"
            elif  (mp4s |> Array.filter (JobProcess.vttExists>>not)).Length = 0 then
                Notify $"All mp4 files in '{model.localFolder}' already have vtt files"
            else
                CreateJob

    let tryCancel (model,jobId,win) = 
        task {            
            let status = 
                model.jobs |> List.tryFind (fun x -> x.JobId = jobId)
                |> Option.map _.Status
            match status with
            | Some Done | Some Cancelled -> return (jobId,Remove)
            | Some Cancelling -> return (jobId,Ignore)
            | Some _ ->
                let! result =
                    Dispatcher.UIThread.InvokeAsync<bool>(fun _ ->
                        task {
                            let dlg = YesNoDialog("Are you sure you want to cancel this job?") 
                            return! dlg.ShowDialogAsync(win)
                        })
                return (jobId, if result then Cancel else Ignore)
            | None -> return (jobId,Ignore)
        }

    let connectionColor = function 
        | Connecting -> Brushes.Orange 
        | Connected -> Brushes.Green 
        | Disconnected -> Brushes.Gray 
        | Reconnecting -> Brushes.Yellow

module Update = 
    open Avalonia.Threading
    open Avalonia.FuncUI.Hosts

    let subscribeBackground (model:Model) =
        let backgroundEvent dispatch =
            let ctx = new System.Threading.CancellationTokenSource()
            let comp =
                async{
                    let comp =
                         model.mailbox.Reader.ReadAllAsync()
                         |> AsyncSeq.ofAsyncEnum
                         |> AsyncSeq.iter dispatch
                    match! Async.Catch(comp) with
                    | Choice1Of2 _ -> printfn "dispose subscribeBackground"
                    | Choice2Of2 ex -> printfn "%s" ex.Message
                }
            Async.Start(comp,ctx.Token)            
            {new IDisposable with member _.Dispose() = ctx.Dispose(); printfn "disposing subscription backgroundEvent";}
        backgroundEvent
        
    let subscriptions model =
    
        let sub2 = subscribeBackground model
        [
                [nameof sub2], sub2                
        ]
           
    let init _   = Model.Default null,Cmd.none

    let update (win:HostWindow) msg (model:Model) = 
        try
            match msg with
            | Initialize -> model, Cmd.OfTask.either Jobs.recoverJobs model UpsertJobs Exn
            | Notify s -> U.showNotification "" s; model,Cmd.none
            | Exn ex -> model, Cmd.ofMsg (Notify ex.Message)
            | ServiceJobCount j -> {model with jobsInQueue = j}, Cmd.none
            | ConnectionState c -> {model with connectionState=c}, Cmd.none
            //service messages
            | FromService ({jobId=id; status= ``Done server processing`` } as c) -> Jobs.updateStatus model id c.status,Cmd.ofMsg (StartDownload id)
            | FromService {jobId=id; status= Cancelled } -> Jobs.removeJob model id, Cmd.none
            | FromService s -> Jobs.updateStatus model s.jobId s.status, Cmd.none
            //jobs  
            | OpenFolder -> model,Cmd.OfAsync.perform U.getFolder win LocalFolder
            | LocalFolder f -> {model with localFolder=f}, Cmd.none
            | Diarize b -> {model with diarize=b}, Cmd.none
            | TagSpeaker b -> {model with tagSpeaker=b}, Cmd.none
            | JobError ex -> Jobs.setError model ex
            | SubmitJob -> model, Cmd.ofMsg (U.submitJob model)
            | RemoveJob id -> Jobs.removeJob model id, Cmd.none
            | UpsertJobs js -> Jobs.upsert model js, Cmd.none
            | CreateJob -> model, Cmd.OfTask.either JobProcess.createJob model JobCreated JobError
            | JobCreated j -> Jobs.upsert model [j], Cmd.ofMsg (StartUpload j.JobId)
            | StartUpload id -> model, Cmd.OfTask.either JobProcess.startUpload (model,id) DoneUpload JobError
            | DoneUpload id -> model, Cmd.OfTask.attempt JobProcess.queueJob (model,id)  Exn
            | StartDownload id -> model, Cmd.OfTask.either JobProcess.startDownload (model,id) DoneDownload JobError
            | DoneDownload j -> Jobs.upsert model [j], Cmd.none        
            | TryCancelJob jobId -> model, Cmd.OfTask.either U.tryCancel (model,jobId,win) TryCancelJobResult Exn
            | TryCancelJobResult (jobId,Cancel) -> 
                let model= Jobs.updateStatus model jobId Cancelling
                model, Cmd.OfTask.attempt JobProcess.cancelJob (model,jobId) JobError
            | TryCancelJobResult (jobId,Remove) -> model, Cmd.ofMsg (RemoveJob jobId)
            | TryCancelJobResult (_,Ignore) -> model, Cmd.none
            
        with ex -> 
            model, Cmd.ofMsg (Exn ex)
