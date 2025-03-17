namespace TranscriptionClient
#nowarn "57"
#nowarn "40"
open System
open System.IO
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI
open Avalonia.Threading
open TranscriptionInterop
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Controls.Primitives
open Avalonia

module U =
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

    let connectionColor = function 
        | Connecting -> Brushes.Orange 
        | Connected -> Brushes.Green 
        | Disconnected -> Brushes.Gray 
        | Reconnecting -> Brushes.Yellow


[<AbstractClass; Sealed>]
type Views =
    static member main (window:Window)  =
        Component (fun ctx ->
            
            //messages may come from the server or the UI
            let rec dispatch = function
                | Status {jobId=id; status= ``Done server processing`` } -> JobProcess.startPostServiceComplete model dispatch id |> ignore
                | Status {jobId=id; status= Cancelled } -> Jobs.removeJob model id
                | Status {jobId=id; status=status} -> Jobs.updateJobStatus model id status
                | Jobs j -> model.invokeOnUIThread (fun _ -> model.jobsInQueue.Set j)
                | ConnectionState c -> model.invokeOnUIThread (fun _ -> model.connectionState.Set c)
                | UpdateJobs js -> model.invokeOnUIThread (fun _ -> model.uiJobs.Set js)

            and connection = Connection.get dispatch

            and model = {
                runningJobs = Data.jobs
                jobsInQueue = ctx.useState 0            
                localFolder = ctx.useState ""
                diarize = ctx.useState true
                tagSpeaker = ctx.useState true
                uiJobs  = ctx.useState []
                connectionState = ctx.useState Disconnected
                invokeOnUIThread = U.invokeOnUIThread
                showNotification = U.showNotification
                connection = connection
            }

            // Jobs.recoverJobs model dispatch |> ignore
                        
            //root view
            DockPanel.create [                
                DockPanel.children [
                    Grid.create [
                        Grid.rowDefinitions "150.,*,30."
                        Grid.columnDefinitions "*,*"
                        Grid.horizontalAlignment HorizontalAlignment.Stretch
                        Grid.children [
                            JobSubmissionView.create window model dispatch
                            JobsListView.create window model dispatch
                            Border.create [
                                Grid.row 2
                                Grid.columnSpan 2
                                Border.horizontalAlignment HorizontalAlignment.Stretch
                                Border.verticalAlignment VerticalAlignment.Bottom
                                Border.margin 3
                                Border.background Brushes.DarkSlateGray
                                Border.borderThickness 1.0
                                Border.borderBrush Brushes.LightBlue                            
                                Border.child(
                                    StackPanel.create [
                                        StackPanel.orientation Orientation.Horizontal
                                        StackPanel.children [
                                            Ellipse.create [
                                                Shapes.Ellipse.tip $"Service connection: {model.connectionState.Current}"
                                                Shapes.Ellipse.fill (U.connectionColor model.connectionState.Current)
                                                Shapes.Ellipse.width 10.
                                                Shapes.Ellipse.height 10.
                                                Shapes.Ellipse.margin (Thickness(5.,0.,5.,0.))
                                                Shapes.Ellipse.verticalAlignment VerticalAlignment.Center
                                            ]
                                            Vls.textBlock 
                                                (if model.connectionState.Current.IsDisconnected then "" else $"There are {model.jobsInQueue.Current} jobs in the service queue")
                                                [ 
                                                    TextBlock.textAlignment TextAlignment.Center
                                                    TextBlock.horizontalAlignment HorizontalAlignment.Stretch
                                                    TextBlock.fontStyle FontStyle.Italic
                                                ]
                                        ]
                                    ]
                                )
                            ]                            
                            GridSplitter.create [
                                Grid.column 1
                                Grid.rowSpan 3
                                GridSplitter.verticalAlignment VerticalAlignment.Center
                                GridSplitter.height 50.
                                GridSplitter.horizontalAlignment HorizontalAlignment.Left                                
                                GridSplitter.background Brushes.DarkGray                                
                            ]
                        ]
                    ]
                ]
            ]
        )
