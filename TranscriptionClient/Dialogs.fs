namespace TranscriptionClient
open Avalonia
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts
open Avalonia.Layout
open System.Threading.Tasks
open Avalonia.FuncUI
open Avalonia.Controls
open Avalonia.Platform.Storage

module Dialogs =

    let openFileDialog (parent: Window) =
        async {
            // Initialize the folder picker dialog options
            let options = FolderPickerOpenOptions(
                Title = "Select Folder Containing Video File(s)",
                AllowMultiple = false
            )

            // Show the folder picker dialog
            let! folders = parent.StorageProvider.OpenFolderPickerAsync(options) |> Async.AwaitTask

            // Process the selected folder
            return
                folders
                |> Seq.tryHead
                |> Option.map _.TryGetLocalPath()                    
        }


type YesNoDialog(message: string) as this =
    inherit HostWindow()
    let tcs = new TaskCompletionSource<bool>()
    do
        base.Title <- "Confirmation"
        base.Width <- 400.0
        base.Height <- 150.0

        let content =            
            DockPanel.create [
                DockPanel.children [
                    TextBlock.create [
                        TextBlock.text message
                        TextBlock.margin (Thickness 10.0)
                        TextBlock.verticalAlignment VerticalAlignment.Center
                        TextBlock.horizontalAlignment HorizontalAlignment.Center
                    ]
                    StackPanel.create [
                        StackPanel.orientation Orientation.Horizontal
                        StackPanel.horizontalAlignment HorizontalAlignment.Center
                        StackPanel.children [
                            Button.create [
                                Button.content "Yes"
                                Button.margin (Thickness 5.0)
                                Button.onClick (fun _ ->
                                    tcs.SetResult(true)
                                    this.Close()
                                )
                            ]
                            Button.create [
                                Button.content "No"
                                Button.margin (Thickness 5.0)
                                Button.onClick (fun _ ->
                                    tcs.SetResult(false)
                                    this.Close()
                                )
                            ]
                        ]
                    ] 
                ]
                DockPanel.dock Dock.Bottom
            ]

        this.Content <- Component(fun ctx -> content)
    member this.ShowDialogAsync(parent: Window) : Task<bool> =
        base.ShowDialog(parent) |> ignore
        tcs.Task

