namespace TranscriptionClient
open System
open System.IO
open TranscriptionInterop

//handle job related operations f
module  Jobs = 
    open Elmish

    let JOBS_FILE = "jobs.json"
    let saveJobs model =
        task {
            try 
                let json = System.Text.Json.JsonSerializer.Serialize(model.jobs,Ser.serOptions())
                File.WriteAllText(JOBS_FILE, json)
            with ex -> 
                model.mailbox.Writer.TryWrite (Notify $"Error saving jobs: {ex.Message}" ) |> ignore     
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
            model.mailbox.Writer.TryWrite (Notify $"Error loading jobs: {ex.Message}" ) |> ignore  
            []

    let updateStatus model id status = 
        let m = {model with jobs = model.jobs |> List.map(fun j -> if j.JobId = id then {j with Status=status}; else j)}
        saveJobs m |> ignore
        m

    let upsert model jobs = 
        let jobs = 
            (jobs @ model.jobs)
            |> List.distinctBy _.JobId
        let m = {model with jobs=jobs}
        saveJobs m |> ignore
        m

    let processRecoveredJobs model  jobs =
        task {
            do! Async.Sleep 1000
            let dispatch = JobProcess.disp model
            for j in jobs do
                match j.Status with
                | Created                    -> dispatch (StartUpload j.JobId)
                | ``In service queue``       -> () //do nothing on client as eventually the service will process the job and notify the client
                | ``Done server processing`` -> dispatch (StartDownload j.JobId) 
                | _ -> ()
        }

    //handle client restarts
    let recoverJobs model =
        task {
            let dispatch = JobProcess.disp model
            let jobs = loadJobs model
            match jobs with
            | [] -> return []
            | _ ->
                let map = jobs |> List.map (fun j -> j.JobId,j) |> Map.ofList
                let! resp = ServiceApi.invoke model (fun client -> task{ return! client.SyncJobs {jobIds= jobs |> List.map _.JobId} }) |> Async.AwaitTask
                let resolutions = 
                    resp.jobsStatus
                    |> List.map(fun x -> 
                        let localStatus = map |> Map.tryFind x.jobId |> Option.map _.Status
                        match x.status,localStatus with
                        | Error _                           ,_             -> None, Some x.jobId
                        | Cancelled                         ,_
                        | Cancelling                        ,_             -> None, Some x.jobId
                        | ``Not found in service queue``    ,Some(Created) -> Some map.[x.jobId],None
                        | ``Not found in service queue``    ,Some(Done)    -> None,None
                        | y                      ,_             -> Some {map.[x.jobId] with Status=y},None
                    )
                let jobs = resolutions |> List.choose fst
                let toRemove = resolutions |> List.choose snd
                for r in toRemove do
                    do! ServiceApi.invoke model (fun client -> task{ return! client.ClearJob r })
                processRecoveredJobs model jobs |> ignore
                return jobs
        }
            
    let removeJob model jobId = 
        let m = {model with jobs = model.jobs |> List.filter(fun x -> x.JobId <> jobId)}
        saveJobs m |> ignore
        m

    let setError model (exn:Exception) =
        let ret =
            let exn = if exn.InnerException <> null then exn.InnerException else exn            
            match exn with 
            | JobException(id,msg) -> updateStatus model id (Error msg), Cmd.none            
            | _ -> model, Cmd.ofMsg (Notify exn.Message)
        ret
