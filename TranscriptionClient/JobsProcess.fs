namespace TranscriptionClient
open System
open System.IO
open TranscriptionInterop

//background jobs processing 
module JobProcess =     
    let disp model msg = model.mailbox.Writer.TryWrite(msg) |> ignore

    let downLoadFiles (model:Model) (job:Job) =
        task {            
            try
                let localPath = job.Path             
                let remotePath = job.RemoteFolder
                for f in Directory.GetFiles(remotePath,"*.vtt") do                    
                    let fileName = Path.GetFileName f
                    disp model (FromService {jobId=job.JobId; status=Downloading fileName})
                    let localFile = Path.Combine(localPath,fileName)
                    File.Copy(f,localFile,true)            
            with ex ->
                return raise (JobException(job.JobId,ex.Message))
        }

    let startDownload (model,jobId:string) =
        task {    
            try
                let job = model.jobs |> List.find(fun j -> j.JobId = jobId)
                do! downLoadFiles model job
                do! ServiceApi.invoke model (fun client -> task{ return! client.ClearJob jobId })
                return {job with Status=Done}
            with ex ->
                return raise (JobException(jobId,ex.Message))
        }

    let uploadFiles model job = 
        task {
            try 
                let localPath = job.Path.Replace("\\","/")
                let remotePath = job.RemoteFolder
                let dispatch = disp model
                for f in Directory.GetFiles(localPath,"*.mp4") do
                    let fileName = Path.GetFileName f
                    dispatch (FromService {jobId=job.JobId; status=Uploading fileName})
                    do! Async.Sleep 30000
                    let remoteFile = Path.Combine(remotePath,fileName).Replace("\\","/")
                    File.Copy(f,remoteFile,true)            
            with ex ->
                return raise (JobException(job.JobId,ex.Message))
        }

    let startUpload (model,jobId) = 
        task {
            try
                let job = model.jobs |> List.find(fun j -> j.JobId = jobId)
                do! uploadFiles model  job
                return jobId
            with ex ->
                return raise (JobException(jobId,ex.Message))
        }

    let queueJob (model,jobId) = 
        task {
            try
                do! ServiceApi.invoke model (fun client -> task{ return! client.QueueJob jobId })
            with ex -> 
                return raise (JobException(jobId,ex.Message))
        }

    let createJob (model:Model)=
        task {
            try
                let jobCreation = {diarize=model.diarize; identifySpeaker=model.tagSpeaker}
                let! rslt = ServiceApi.invoke model (fun client -> task{ return! client.CreateJob jobCreation })
                let job = 
                    {
                        JobId=rslt.jobId
                        StartTime=DateTime.Now
                        Path=model.localFolder
                        Status = Created
                        Diarize = model.diarize
                        IdentifySpeaker= model.tagSpeaker
                        RemoteFolder=rslt.jobPath
                    }
                return job    
            with ex ->
                return raise (JobException("",ex.Message))
        }

    let cancelJob (model,jobId) =
        task {
            try
                do! ServiceApi.invoke model (fun client -> task{ return! client.CancelJob jobId })
            with ex -> 
                return raise (JobException(jobId,ex.Message))
        }
