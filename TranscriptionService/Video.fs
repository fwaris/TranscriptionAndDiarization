namespace FsOpenAI.Vision
open OpenCvSharp
open System.Drawing
open System.IO
open FSharp.Control

module Video =    
    open System.IO
    open System.Runtime.InteropServices
    open System
    //returns the number of frames, fps, width, height and format of the video
    let getInfo f = 
        use clipIn = new VideoCapture(f:string)
        let fc = clipIn.FrameCount
        clipIn.FrameCount,clipIn.Fps,clipIn.FrameWidth,clipIn.FrameHeight,string clipIn.Format


    let toBmp (mat:Mat) = 
        let ptr = mat.CvPtr
        use bmp = OpenCvSharp.Extensions.BitmapConverter.ToBitmap(mat)
        use ms = new MemoryStream()
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png)
        ms.Position <- 0L
        Some(ms.ToArray())

    let readFrame (clipIn:VideoCapture) n =
        async {
            let _ = clipIn.PosFrames <- n
            use mat = new Mat()
            let resp =
                if clipIn.Read(mat) then
                    toBmp mat
                else
                    None
            mat.Release()
            return resp
        }

    let readFrameAtTime (clipIn:VideoCapture) (t:TimeSpan) =
        clipIn.PosMsec <- int t.TotalMilliseconds
        use mat = new Mat()
        let resp =
            if clipIn.Read(mat) then
                toBmp mat
            else
                None
        mat.Release()
        resp

    let getFramesAtTimes (file:string) (ts:TimeSpan list) =
        asyncSeq {
            use clipIn = new VideoCapture(file:string)
            for t in ts do
                let frame = readFrameAtTime clipIn t
                yield frame
            clipIn.Release()
        }        

    let openOsSpecificFile path =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let psi =
                new System.Diagnostics.ProcessStartInfo(FileName = path, UseShellExecute = true)

            System.Diagnostics.Process.Start(psi) |> ignore
        elif RuntimeInformation.IsOSPlatform(OSPlatform.Linux) then
            System.Diagnostics.Process.Start("xdg-open", path) |> ignore
        elif RuntimeInformation.IsOSPlatform(OSPlatform.OSX) then
            System.Diagnostics.Process.Start("open", path) |> ignore
        else
            invalidOp "Not supported OS platform"

    let saveImage (img:byte[]) (path:string) =
        use ms = new MemoryStream(img)
        let bmp = System.Drawing.Image.FromStream(ms)
        use str = File.Create path
        bmp.Save(str,Imaging.ImageFormat.Png)

    let showImage (img:byte[])  = 
        let fn = Path.GetTempFileName() + ".png"
        saveImage img fn
        openOsSpecificFile fn


    let getFrames file maxFrames = 
        asyncSeq {
            use clipIn = new VideoCapture(file:string)
            let frames = 
                if clipIn.FrameCount <= maxFrames then 
                    [0..clipIn.FrameCount-1] 
                else
                    let skip = clipIn.FrameCount / maxFrames
                    [
                       yield  0                          // keep first
                       for i in 1..maxFrames-1 do        // evenly spaced frames
                           yield i*skip
                       yield clipIn.FrameCount-1         // keep last
                    ]
                    |> set                               // remove duplicates
                    |> Seq.toList
                    |> List.sort
            for n in frames do
                let! frame = readFrame clipIn n
                yield frame
            clipIn.Release()
        }

