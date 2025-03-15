namespace TranscriptionClient

open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open System.IO
 open Avalonia.Platform.Storage

module Dialogs =
    open Avalonia.Platform.Storage
   
    let openFileDialog (parent: Window) =
        async {
            // Initialize the open file dialog options
            let options = FolderPickerOpenOptions(
                Title = "Select Folder Containing Video File(s)",
                AllowMultiple = false
            )

            // Show the file picker dialog
            let! folder = parent.StorageProvider.OpenFolderPickerAsync(options) |> Async.AwaitTask
            return folder |> Seq.tryHead
        }
