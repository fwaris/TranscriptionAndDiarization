namespace TranscriptionClient
open System
open System.IO
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI
open TranscriptionInterop
open Avalonia.Layout
open Avalonia.Media
open Avalonia

module Vls = 
    let textBlock text ls = 
        TextBlock.create [
            yield TextBlock.text text
            yield TextBlock.margin 2
            yield TextBlock.verticalAlignment VerticalAlignment.Center
            for l in ls do
                yield l
        ]

module JobSubmissionView = 

    let create (model:Model) dispatch =
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
                    Grid.rowDefinitions "*,*,50."
                    Grid.columnDefinitions "75.,*,40"
                    Grid.children [
                        Vls.textBlock "Job Folder:" [Grid.row 0; Grid.column 0]
                        Border.create [
                            Grid.row 0
                            Grid.column 1
                            Border.background Brushes.DarkSlateGray
                            Border.horizontalAlignment HorizontalAlignment.Stretch
                            Border.margin (Thickness(0.,4.,0.,4.))
                            Border.child(
                            TextBlock.create [                        
                                Grid.row 0
                                Grid.column 1                        
                                TextBlock.margin (Thickness(2.,0.,0.,2.))
                                TextBlock.text model.localFolder
                                TextBlock.horizontalAlignment HorizontalAlignment.Stretch
                                TextBlock.verticalAlignment VerticalAlignment.Center
                                TextBlock.textWrapping TextWrapping.Wrap                            
                                TextBlock.tip "Folder containing video (.mp4) files"
                            ])
                        ]
                        Button.create [
                            Grid.row 0
                            Grid.column 2
                            Button.content "..."
                            Button.tip "Select a folder containing video (*.mp4*) files"
                            Button.onClick (fun _ -> dispatch OpenFolder)
                            Button.margin 4.0
                            Button.verticalAlignment VerticalAlignment.Center
                            Button.horizontalAlignment HorizontalAlignment.Left
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
                                    CheckBox.isChecked model.diarize
                                    CheckBox.onChecked (fun isChecked -> dispatch (Diarize true))
                                    CheckBox.onUnchecked (fun isChecked -> dispatch (Diarize false))
                                    CheckBox.margin 2
                                    CheckBox.verticalAlignment VerticalAlignment.Center
                                ]
                                CheckBox.create [
                                    CheckBox.tip "Identifes a specific speaker in the audio transcript. This particular speaker is configured in the service and cannot be changed from the client"
                                    CheckBox.content "Identify Speaker"
                                    CheckBox.isEnabled model.diarize
                                    CheckBox.isChecked model.tagSpeaker
                                    CheckBox.onChecked (fun isChecked -> dispatch (TagSpeaker true))
                                    CheckBox.onUnchecked (fun isChecked -> dispatch (TagSpeaker false))
                                    CheckBox.margin 2
                                    CheckBox.verticalAlignment VerticalAlignment.Center
                                ]
                            ]
                        ]
                        Button.create [
                            Grid.row 2
                            Grid.columnSpan 3
                            Button.content "Submit Transcription Job"
                            Button.onClick (fun _ -> dispatch SubmitJob)
                            Button.margin 2
                            Button.verticalAlignment VerticalAlignment.Center
                        ]
                    ]
                ])
            ]

