namespace TranscriptionClient 
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

module JobsListView =

    let create window model dispatch = 
        Border.create [
            Grid.column 1
            Grid.rowSpan 2
            Border.margin 2
            Border.borderThickness 1.0
            Border.borderBrush Brushes.LightBlue
            Border.child (
                Grid.create [
                    Grid.rowDefinitions "30.,*"
                    Grid.children [
                        Vls.textBlock "Your Jobs" [TextBlock.textAlignment TextAlignment.Center; TextBlock.horizontalAlignment HorizontalAlignment.Stretch; TextBlock.fontWeight FontWeight.Bold;]
                        ScrollViewer.create [
                            Grid.row 1
                            ScrollViewer.horizontalScrollBarVisibility ScrollBarVisibility.Auto
                            ScrollViewer.verticalScrollBarVisibility ScrollBarVisibility.Auto
                            ScrollViewer.content (
                                ListBox.create [
                                    ListBox.dataItems model.jobs.Current                                        
                                    ListBox.itemTemplate (
                                        DataTemplateView.create<_,_>(fun (item:Job) ->
                                            Grid.create [
                                                Grid.rowDefinitions "*,*,*,*,*,*,*"
                                                Grid.columnDefinitions "100.,*"
                                                Grid.margin 2
                                                Grid.children [  
                                                    Button.create [
                                                        Button.content "\u0078"
                                                        Button.onClick (fun _ -> JobSubmissionView.cancelJob window model dispatch item.JobId |> ignore)
                                                        Button.margin 2
                                                        Grid.row 0
                                                        Grid.column 0
                                                        Button.verticalAlignment VerticalAlignment.Center
                                                        Button.horizontalAlignment HorizontalAlignment.Left
                                                    ]
                                                    Vls.textBlock "Job Id:" [Grid.row 1]
                                                    Vls.textBlock item.JobId [Grid.row 1; Grid.column 1]
                                                    Vls.textBlock "Path:" [Grid.row 2]
                                                    Vls.textBlock item.Path [Grid.row 2; Grid.column 1; TextBlock.textWrapping TextWrapping.Wrap]
                                                    Vls.textBlock "Start Time:" [Grid.row 3]
                                                    Vls.textBlock (item.StartTime.ToShortTimeString()) [Grid.row 3; Grid.column 1]
                                                    Vls.textBlock "Status:" [Grid.row 4]
                                                    Vls.textBlock (string item.Status) [Grid.row 4; Grid.column 1]
                                                    Vls.textBlock "Diarize:" [Grid.row 5]
                                                    Vls.textBlock (sprintf "%A" item.Diarize) [Grid.row 5; Grid.column 1]                                                    
                                                    Vls.textBlock "Identify Speaker:" [Grid.row 6]
                                                    Vls.textBlock (sprintf "%A" item.IdentifySpeaker) [Grid.row 6; Grid.column 1]
                                                ]
                                            ]
                                        )
                                    )                                       
                                ]
                            )
                        ]                                        
                    ]
                ]
            )
        ]
    
