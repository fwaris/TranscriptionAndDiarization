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

    let getFolder window (localFolder:IWritable<_>) =
        async{
            match! TranscriptionClient.Dialogs.openFileDialog(window) with
            | Some f -> localFolder.Set f
            | None -> ()
        }
               
    let submitJob (model:Model) dispatch =
        task {
            try 
                if String.IsNullOrWhiteSpace model.localFolder.Current then 
                    model.showNotification "" "Please select a folder containing video files" 
                elif model.jobs.Current |> List.exists(fun j -> j.Path = model.localFolder.Current ) then
                    model.showNotification "" $"There is an existing job for the folder '{model.localFolder.Current}'"                        
                elif Directory.Exists model.localFolder.Current |> not then
                    model.showNotification "" $"Folder does not exist '{model.localFolder.Current}'"
                else
                    let diarize = model.diarize.Current
                    let tagSpeaker = model.tagSpeaker.Current
                    let jobCreation = {diarize=diarize; identifySpeaker=tagSpeaker}
                    let! rslt = ServiceApi.invoke model dispatch (fun client -> task{ return! client.CreateJob jobCreation })
                    let job = 
                        {
                            JobId=rslt.jobId
                            StartTime=DateTime.Now
                            Path=model.localFolder.Current
                            Status = Created
                            Diarize = diarize
                            IdentifySpeaker= tagSpeaker
                            RemoteFolder=rslt.jobPath
                        }                        
                    model.update(fun () -> model.jobs.Set (job::model.jobs.Current))
            with ex ->
                model.showNotification "" $"Error submitting job: {ex.Message}"
        }

    let updateJobStatus model jobId status = 
        let js = model.jobs.Current |> List.map (fun j -> if j.JobId = jobId then {j with Status=status} else j)
        model.update (fun () -> model.jobs.Set js)

    let removeJob model jobId = 
        let js = model.jobs.Current |> List.filter(fun x -> x.JobId <> jobId)
        model.jobs.Set js
            
    let cancelJob window (model:Model) dispatch jobId = 
        task {
            let job = model.jobs.Current |> List.tryFind (fun x -> x.JobId = jobId)
            match job with 
            | Some job -> 
                if job.IsRunning && not job.Status.IsCancelling then 
                    let dlg = YesNoDialog("Are you sure you want to cancel this job?") 
                    let! result = dlg.ShowDialogAsync(window)
                    if result then
                        if job.IsRunning then
                            updateJobStatus model jobId Cancelling
                            do! ServiceApi.invoke model dispatch (fun client -> task{ return! client.CancelJob jobId })
                elif job.Status.IsCancelled || job.Status.IsDone then 
                    removeJob model jobId
            | None -> ()
        }        

    let create window (model:Model) dispatch =
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
                                TextBlock.text model.localFolder.Current; 
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
                            Button.onClick (fun _ -> getFolder window model.localFolder |> Async.Start )
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
                                    CheckBox.isChecked model.diarize.Current
                                    CheckBox.onChecked (fun isChecked -> model.diarize.Set true)
                                    CheckBox.onUnchecked (fun isChecked -> model.diarize.Set false)
                                    CheckBox.margin 2
                                    CheckBox.verticalAlignment VerticalAlignment.Center
                                ]
                                CheckBox.create [
                                    CheckBox.tip "Identifes a specific speaker in the audio transcript. This particular speaker is configured in the service and cannot be changed from the client"
                                    CheckBox.content "Identify Speaker"
                                    CheckBox.isEnabled model.diarize.Current
                                    CheckBox.isChecked model.tagSpeaker.Current
                                    CheckBox.onChecked (fun isChecked -> model.tagSpeaker.Set true)
                                    CheckBox.onUnchecked (fun isChecked -> model.tagSpeaker.Set false)
                                    CheckBox.margin 2
                                    CheckBox.verticalAlignment VerticalAlignment.Center
                                ]
                            ]
                        ]
                        Button.create [
                            Grid.row 2
                            Grid.columnSpan 3
                            Button.content "Submit Transcription Job"
                            Button.onClick (fun _ -> submitJob model dispatch |> ignore)
                            Button.margin 2
                            Button.verticalAlignment VerticalAlignment.Center
                        ]
                    ]
                ])
            ]

