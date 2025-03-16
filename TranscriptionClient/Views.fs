namespace TrascriberClient
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
type Job = {JobId:string; Path:string; StartTime:DateTime; Status:JobsState; Diarize : bool; IdentifySpeaker : bool}
    with member this.IsRunning = this.Status <> Done || this.Status <> Cancelled || not this.Status.IsError

module Vls = 
    let textBlock text ls = 
        TextBlock.create [
            yield TextBlock.text text
            yield TextBlock.margin 2
            yield TextBlock.verticalAlignment VerticalAlignment.Center
            for l in ls do
                yield l
        ]

module JobsPanel = 

    let getFolder window (localFolder:IWritable<_>) =
        async{
            match! TranscriptionClient.Dialogs.openFileDialog(window) with
            | Some f -> localFolder.Set f
            | None -> ()
        }
        

    let jobPanel window (localFolder:IWritable<_>) (diarize:IWritable<bool>) (tagSpeaker:IWritable<bool>)  =
        Border.create [
            Grid.row 0
            Border.horizontalAlignment HorizontalAlignment.Stretch
            Border.verticalAlignment VerticalAlignment.Top
            Border.margin 2
            Border.borderThickness 1.0
            Border.borderBrush Brushes.LightBlue
            Border.margin 2
            Border.clipToBounds true
            Border.child (
                Grid.create [
                    Grid.horizontalAlignment HorizontalAlignment.Stretch
                    Grid.rowDefinitions "*,*,*"
                    Grid.columnDefinitions "*,*,*"
                    Grid.children [
                        Vls.textBlock "Job Folder:" [Grid.row 0; Grid.column 0]
                        TextBlock.create [                                    
                            Grid.row 0
                            Grid.column 1                        
                            TextBlock.text localFolder.Current; 
                            TextBlock.horizontalAlignment HorizontalAlignment.Stretch
                            TextBlock.verticalAlignment VerticalAlignment.Center
                            TextBlock.margin 2
                            TextBlock.textWrapping TextWrapping.Wrap
                            TextBlock.background Brushes.Lavender
                            TextBlock.tip "Folder containing video (.mp4) files"
                        ]
                        Button.create [
                            Grid.row 0
                            Grid.column 2
                            Button.content "..."
                            Button.onClick (fun _ -> getFolder window localFolder |> Async.Start )
                            Button.margin 2
                            Button.verticalAlignment VerticalAlignment.Center
                        ]                                            
                        StackPanel.create [
                            Grid.row 1
                            Grid.columnSpan 3
                            StackPanel.margin 2
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.horizontalAlignment HorizontalAlignment.Stretch
                            StackPanel.children [
                                CheckBox.create [
                                    CheckBox.tip "Identifies distinct speakers in the audio transcript but takes longer to run"
                                    CheckBox.content "Diarize"
                                    CheckBox.isChecked diarize.Current
                                    CheckBox.onChecked (fun isChecked -> diarize.Set true)
                                    CheckBox.onUnchecked (fun isChecked -> diarize.Set false)
                                    CheckBox.margin 2
                                    CheckBox.verticalAlignment VerticalAlignment.Center
                                ]
                                CheckBox.create [
                                    CheckBox.tip "Identifes a specific speaker in the audio transcript. This particular speaker is configured in the service and cannot be changed from the client"
                                    CheckBox.content "Identify Speaker"
                                    CheckBox.isEnabled diarize.Current
                                    CheckBox.isChecked tagSpeaker.Current
                                    CheckBox.onChecked (fun isChecked -> tagSpeaker.Set true)
                                    CheckBox.onUnchecked (fun isChecked -> tagSpeaker.Set false)
                                    CheckBox.margin 2
                                    CheckBox.verticalAlignment VerticalAlignment.Center
                                ]
                            ]
                        ]
                        Button.create [
                            Grid.row 2
                            Grid.columnSpan 3
                            Button.content "Submit Transcription Job"
                            Button.onClick (fun _ -> startJob diarize.Current tagSpeaker.Current |> ignore)
                            Button.margin 2
                            Button.verticalAlignment VerticalAlignment.Center
                        ]
                    ]
                ])
            ]

