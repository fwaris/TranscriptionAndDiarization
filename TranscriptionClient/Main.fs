namespace TranscriptionClient
#nowarn "57"
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

[<AbstractClass; Sealed>]
type Views =
    static member main (window:Window)  =
        Component (fun ctx ->
            //notificatons
            let notificationManager = Avalonia.Controls.Notifications.WindowNotificationManager(window)
            notificationManager.MaxItems <- 1
            notificationManager.Position <- Avalonia.Controls.Notifications.NotificationPosition.BottomLeft
            notificationManager.Margin <- Thickness(10)
        
            let showNotification title text =
                let notification = Avalonia.Controls.Notifications.Notification(
                    title,
                    text,
                    Avalonia.Controls.Notifications.NotificationType.Information
                )
                notificationManager.Show(notification)

            let update (f:unit->unit) = Dispatcher.UIThread.InvokeAsync f |> ignore

            let model = {
                jobsInQueue = ctx.useState 0            
                localFolder = ctx.useState ""
                diarize = ctx.useState true
                tagSpeaker = ctx.useState true
                jobs  = ctx.useState []
                connectionState = ctx.useState Disconnected
                update = update
                showNotification = showNotification
            }
            
            let dispatch = function 
                | Status (id, status) -> JobSubmissionView.updateJobStatus model id status
                | Jobs j -> update (fun () -> model.jobsInQueue.Set j)
                | ConnectionState c -> update (fun () -> model.connectionState.Set c)

            let connection = lazy(Connection.create dispatch)
            let client= lazy(new TranscriptionClient(connection.Value,dispatch))
            
            //root view
            DockPanel.create [                
                DockPanel.children [
                    Grid.create [
                        Grid.rowDefinitions "150.,*,30."
                        Grid.columnDefinitions "*,*"
                        Grid.horizontalAlignment HorizontalAlignment.Stretch
                        Grid.children [
                            JobSubmissionView.create window model 
                            JobsListView.create window model
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
                                    Vls.textBlock 
                                        $"There are {model.jobsInQueue.Current} jobs in the service queue" 
                                        [ 
                                            TextBlock.textAlignment TextAlignment.Center
                                            TextBlock.horizontalAlignment HorizontalAlignment.Stretch
                                            TextBlock.fontStyle FontStyle.Italic
                                        ]
                                )
                            ]                            
                        ]
                    ]
                ]
            ]
        )
