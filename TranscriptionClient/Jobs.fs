namespace TranscriptionClient
open System
open System.IO
open TranscriptionInterop

module Jobs = 
    let JOBS_FILE = "jobs.json"
    let saveJobs model =
        task {
            try 
                let json = System.Text.Json.JsonSerializer.Serialize(model.runningJobs,Ser.serOptions())
                File.WriteAllText(JOBS_FILE, json)
            with ex -> 
                model.showNotification "" $"Error saving jobs: {ex.Message}"        
        }

    let loadJobs model =
        try
            if File.Exists JOBS_FILE then
                let json = File.ReadAllText JOBS_FILE
                let jobs = System.Text.Json.JsonSerializer.Deserialize<Job list>(json,Ser.serOptions())
                jobs
            else 
                []
        with ex -> 
            model.showNotification "" $"Error loading jobs: {ex.Message}"
            []

    //handle client restarts
    let recoverJobs model dispatch =
        task {
            let jobs = loadJobs model
            match jobs with
            | [] -> ()
            | _ ->
                let map = jobs |> List.map (fun j -> j.JobId,j) |> Map.ofList
                let! resp =  ServiceApi.invoke model dispatch (fun client -> task{ return! client.SyncJobs {jobIds= jobs |> List.map _.JobId} })
                let resolutions = 
                    resp.jobsStatus
                    |> List.map(fun x -> 
                        let localStatus = map |> Map.tryFind x.jobId |> Option.map _.Status
                        match x.status,localStatus with
                        | Error _                           ,_             -> None, Some x.jobId
                        | Cancelled                         ,_
                        | Cancelling                        ,_             -> None, Some x.jobId
                        | ``Not found in service queue``    ,Some(Created) -> Some map.[x.jobId],None
                        | y                      ,_             -> Some {map.[x.jobId] with Status=y},None
                    )
                let jobs = resolutions |> List.choose fst
                let toRemove = resolutions |> List.choose snd
                for r in toRemove do
                    do! ServiceApi.invoke model dispatch (fun client -> task{ return! client.ClearJob r })
                model.runningJobs.Value <- jobs
                dispatch (UpdateJobs jobs)
                for j in jobs do
                    match j.Status with
                    | Created                    -> ServiceApi.invoke model dispatch (fun client -> task{ return! client.QueueJob j.JobId }) |> ignore
                    | ``In service queue``       -> JobProcess.startPostCreate model dispatch j.JobId |> ignore
                    | ``Done server processing`` -> JobProcess.startPostServiceComplete model dispatch j.JobId |> ignore
                    | _ -> ()
        }

    let updateJobs model jobs =
        model.runningJobs.Value <- jobs
        model.invokeOnUIThread(fun _ ->  model.uiJobs.Set jobs)
        saveJobs model |> ignore
            
    let submitJob (model:Model) dispatch =
        task {
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
            updateJobs model (job::model.runningJobs.Value)
            JobProcess.startPostCreate model dispatch job.JobId |> ignore
        }

    let updateJobStatus model jobId status = 
        let js = model.runningJobs.Value |> List.map (fun j -> if j.JobId = jobId then {j with Status=status} else j)
        updateJobs model js

    let removeJob model jobId = 
        let js = model.runningJobs.Value |> List.filter(fun x -> x.JobId <> jobId)
        updateJobs model js

    let cancelJob model jobId dispatch =
        task {
                updateJobStatus model jobId Cancelling
                do! ServiceApi.invoke model dispatch (fun client -> task{ return! client.CancelJob jobId })
        }
