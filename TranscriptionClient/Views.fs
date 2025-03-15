namespace TranscriptionClient
#nowarn "57"
open System
open System.IO
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI
open Avalonia.Threading
open System.Threading
open TranscriptionInterop
open Avalonia.Layout
open Avalonia.Media

type Job = {JobId:string; Path:string; StartTime:DateTime}

[<AbstractClass; Sealed>]
type Views =
    static member main (window:Window)  =
        Component (fun ctx ->
            let jobsInQueue = ctx.useState 0            
            let msg = ctx.useState ""
            let localFolder = ctx.useState ""
            let jobs : IWritable<Job list> = ctx.useState []

            let getFolder() =
                async{
                    match! TranscriptionClient.Dialogs.openFileDialog(window) with
                    | Some f -> localFolder.Set f.Name
                    | None -> ()
                }
        
            let update (f:unit->unit) = Dispatcher.UIThread.InvokeAsync f |> ignore

            let dispatch = function 
                | Notification m -> update (fun _ -> msg.Set m)
                | Jobs i -> update (fun _ -> jobsInQueue.Set i)                
                | JobCancelled m -> update (fun _ -> msg.Set m)
                | JobDone m -> update (fun _ -> msg.Set m)
                | JobStarted m -> update (fun _ -> msg.Set m)
            
            let connection = lazy(Connection.create dispatch)
            let client = lazy(new TranscriptionClient(connection.Value,dispatch))

            let connect() = 
                task {
                    dispatch (Notification "Connecting")
                    do! connection.Value.StartAsync()
                    dispatch (Notification "Connected")
                }

            let cancelJob jobId = 
                task {
                    update (fun _ -> jobs.Set (jobs.Current |> List.filter (fun x -> x.JobId <> jobId)))
                }

            let startJob() =
                task {
                    if String.IsNullOrWhiteSpace localFolder.Current then 
                        dispatch (Notification "No folder set")
                    elif jobs.Current |> List.exists(fun j -> j.Path = localFolder.Current ) then
                        dispatch (Notification $"There is an existing job for the folder '{localFolder.Current}'")
                    elif Directory.Exists localFolder.Current |> not then
                        dispatch (Notification $"Folder does not exist '{localFolder.Current}'")
                    else
                        let js = {JobId="1"; StartTime=DateTime.Now; Path=localFolder.Current}::jobs.Current
                        update(fun () -> jobs.Set js)                        
                }

            //root view
            DockPanel.create [                
                DockPanel.children [
                    Grid.create [
                        Grid.rowDefinitions "50.,50.,50.,*"
                        Grid.columnDefinitions "2*,1.*"
                        Grid.horizontalAlignment HorizontalAlignment.Stretch
                        Grid.children [
                            StackPanel.create [
                                StackPanel.margin 2
                                Grid.row 0
                                StackPanel.orientation Orientation.Horizontal
                                StackPanel.horizontalAlignment HorizontalAlignment.Stretch
                                StackPanel.children [
                                    TextBlock.create [TextBlock.text "Folder:"; TextBlock.verticalAlignment VerticalAlignment.Center; TextBlock.margin 2]
                                    TextBlock.create [
                                        TextBlock.text localFolder.Current; 
                                        TextBlock.horizontalAlignment HorizontalAlignment.Stretch
                                        TextBlock.margin 2
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                    ]
                                    Button.create [
                                        Button.content "..."
                                        Button.onClick (fun _ -> getFolder() |> Async.Start )
                                        Button.margin 2
                                        Button.verticalAlignment VerticalAlignment.Center
                                    ]
                                ]
                            ]
                            StackPanel.create [
                                StackPanel.margin 2
                                Grid.row 1
                                StackPanel.orientation Orientation.Horizontal
                                StackPanel.horizontalAlignment HorizontalAlignment.Stretch
                                StackPanel.children [                                    
                                    TextBlock.create [TextBlock.text "All jobs in service queue:"; TextBlock.margin 2; TextBlock.verticalAlignment VerticalAlignment.Center]
                                    TextBlock.create [TextBlock.text (string jobsInQueue.Current); TextBlock.margin 2;  TextBlock.verticalAlignment VerticalAlignment.Center]
                                ]

                            ]
                            Button.create [
                                Grid.row 2
                                Button.content "Start Transcription Job"
                                Button.onClick (fun _ -> startJob() |> ignore)
                                Button.margin 2
                                Button.verticalAlignment VerticalAlignment.Center
                            ]
                            Border.create [
                                Grid.row 3
                                Grid.columnSpan 2
                                Border.horizontalAlignment HorizontalAlignment.Stretch
                                Border.verticalAlignment VerticalAlignment.Bottom
                                Border.margin 3
                                Border.background Brushes.DarkSlateGray
                                Border.child(
                                    TextBlock.create [
                                        TextBlock.margin 2
                                        TextBlock.text msg.Current                            
                                        TextBlock.horizontalAlignment HorizontalAlignment.Stretch
                                    ]  
                                )
                            ]
                            
                            StackPanel.create [
                                Grid.column 1
                                StackPanel.horizontalAlignment HorizontalAlignment.Stretch
                                StackPanel.children [
                                    TextBlock.create [TextBlock.text "Your Jobs"; TextBlock.margin 2; TextBlock.verticalAlignment VerticalAlignment.Center]
                                    ListBox.create [
                                        ListBox.dataItems jobs.Current
                                        ListBox.itemTemplate (
                                            DataTemplateView.create<_,_>(fun (item:Job) ->
                                                StackPanel.create [
                                                    StackPanel.orientation Orientation.Vertical
                                                    StackPanel.horizontalAlignment HorizontalAlignment.Stretch
                                                    StackPanel.children [
                                                        TextBlock.create [TextBlock.text item.JobId; TextBlock.margin 2]                         
                                                        TextBlock.create [TextBlock.text item.Path; TextBlock.margin 2; TextBlock.textWrapping TextWrapping.Wrap]                               
                                                        TextBlock.create [TextBlock.text (item.StartTime.ToShortTimeString()); TextBlock.margin 2]
                                                        Button.create [Button.content "Cancel"; Button.onClick (fun _ -> cancelJob item.JobId |> ignore)]
                                                    ]
                                                ]
                                            )
                                        )                                       
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        )
