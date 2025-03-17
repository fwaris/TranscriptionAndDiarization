namespace TranscriptionClient
open System
open System.IO
open TranscriptionInterop

module JobProcess =     

    let downLoadFiles (model:Model) dispatch (job:Job) =
        task {            
            let localPath = job.Path             
            let remotePath = job.RemoteFolder
            for f in Directory.GetFiles(remotePath,"*.vtt") do                    
                let fileName = Path.GetFileName f
                dispatch (Status {jobId=job.JobId; status=Downloading fileName})
                let localFile = Path.Combine(localPath,fileName)
                File.Copy(f,localFile,true)            
        }

    let startPostServiceComplete model dispatch (jobId:string) =
        task {    
            try
                let job = model.runningJobs.Value |> List.find(fun j -> j.JobId = jobId)
                do! downLoadFiles model dispatch job
                do! ServiceApi.invoke model dispatch (fun client -> task{ return! client.ClearJob jobId })
                dispatch (Status {jobId=job.JobId; status=Done})
            with ex ->
                dispatch (Status {jobId=jobId; status=Error ex.Message})
        }

    let uploadFiles model dispatch job = 
        task {
            let localPath = job.Path.Replace("\\","/")
            let remotePath = job.RemoteFolder
            for f in Directory.GetFiles(localPath,"*.mp4") do
                let fileName = Path.GetFileName f
                dispatch (Status {jobId=job.JobId; status=Uploading fileName})
                do! Async.Sleep 30000
                let remoteFile = Path.Combine(remotePath,fileName).Replace("\\","/")
                File.Copy(f,remoteFile,true)            
        }

    let startPostCreate model dispatch jobId = 
        task {
            try 
                let job = model.runningJobs.Value |> List.find(fun j -> j.JobId = jobId)
                do! uploadFiles model dispatch job
                do! ServiceApi.invoke model dispatch (fun client -> task{ return! client.QueueJob job.JobId })            
            with ex ->
                dispatch (Status {jobId=jobId; status=Error ex.Message})
        }
