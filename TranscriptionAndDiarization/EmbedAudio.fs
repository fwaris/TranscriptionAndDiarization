module EmbedAudio
open System
open System.IO
open System.Buffers
open System.Collections.Generic
open Microsoft.ML.OnnxRuntime
open Microsoft.ML.OnnxRuntime.Tensors
open NAudio.Wave
open FFMpegCore
open System.Numerics

let pyannote = @"e:\s\models\pyannote-embedding-onnx\model.onnx"

let changeExt (inputFile:string) ext = 
    Path.Combine(Path.GetDirectoryName(inputFile),Path.GetFileNameWithoutExtension(inputFile) + ext)

let pyannoteModel = lazy(
    let s_opts = new SessionOptions(LogSeverityLevel=OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO)
    new InferenceSession(pyannote,s_opts))

let toMemory (sp:ISampleProvider) = 
    let writer = ArrayBufferWriter<float32>()
    let buff : float32 array = Array.zeroCreate 4096    
    let mutable read : int64 = sp.Read(buff,0,buff.Length)
    let mutable count = read 
    while (read > 0) do
        let buffSpan = buff.AsSpan().Slice(0, int read)
        writer.Write(buffSpan)
        read <- int64(sp.Read(buff,0,buff.Length))
        count <- count + read
    let mem = Memory(Array.zeroCreate writer.WrittenCount)
    writer.WrittenMemory.CopyTo(mem)
    mem

let getAudioSampleForSpan (inputFile:string) (from:TimeSpan) (span:TimeSpan) =
    use wv = new WaveFileReader(inputFile)
    let sp = wv.ToSampleProvider()
    sp.Skip(from).Take(span) |> toMemory

let getAudioSamples (tsMaxSeconds:TimeSpan) (inputFile:string) (times:(TimeSpan*TimeSpan) list) =
    let times = times |> List.sortBy fst
    times |> List.map(fun (f, t) -> 
        let span = min tsMaxSeconds (t - f)
        let m = getAudioSampleForSpan inputFile f span
        f,span,m)

let saveSample16 (inputFile:string) (outputFile:string) (startTime:TimeSpan) (endTime:TimeSpan) = 
    use wv = new WaveFileReader(inputFile)
    let sp = wv.ToSampleProvider()
    let sp1 = sp.Skip(startTime).Take(endTime - startTime)
    WaveFileWriter.CreateWaveFile16(outputFile,sp1)

module Dist = 
    open MathNet.Numerics
    open MathNet.Numerics.LinearAlgebra.Double
    let cosineDistance (v1:float32[]) (v2:float32[]) = MathNet.Numerics.Distance.Cosine(v1,v2)

(*
let saveSample (inputFile:string) (outputFile:string) (startTime:TimeSpan) (endTime:TimeSpan) = 
    use wv = new WaveFileReader(inputFile)
    let sp = wv.ToSampleProvider()
    let sp1 = sp.Skip(startTime).Take(endTime - startTime)
    WaveFileWriter.CreateWaveFile(outputFile,sp1.ToWaveProvider())

let convertToPCM (inputFile:string) =
    FFMpegArguments.FromFileInput(inputFile)
        .OutputToFile(changeExt inputFile ".pcm", true, fun o -> 
            o
                .WithAudioSamplingRate(16000)
                .ForceFormat("s16le")
            |> ignore)
        .ProcessSynchronously()
*)

module ModelInfo = 
    let printNode (kv:KeyValuePair<string,NodeMetadata>) =
         let k = kv.Key
         let v = kv.Value
         printfn $"{k} %A{v.Dimensions} {v.ElementType} {v.OnnxValueType}"
    
    let show (file:string) =
        use s_opts = new SessionOptions(LogSeverityLevel=OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO)
        use inf = new InferenceSession(file,s_opts)
        printfn "Model loaded: %s" file
        inf.InputMetadata |> Seq.iter printNode
        inf.OutputMetadata |> Seq.iter printNode

let convertTo16KhzWav skipIfExists (inputFile:string) =
    let outFile = changeExt inputFile ".wav"
    if not (skipIfExists && File.Exists outFile) then         
        let r = 
            FFMpegArguments.FromFileInput(inputFile)    
                .OutputToFile(outFile, true, fun o -> 
                    o
                        .WithAudioSamplingRate(16000)
                        .ForceFormat("wav")
                    |> ignore)
                .ProcessSynchronously()
        printfn $"created {outFile} : {r}"
    outFile

let toSamples (waveFile16Kz:string) = 
    use w = new WaveFileReader(waveFile16Kz)
    let targetFormat = WaveFormat(16000,16,1)
    if w.WaveFormat.Equals(targetFormat) |> not then failwith $"expecting format {targetFormat}"
    w.ToSampleProvider() |> toMemory

let toOnnxInput (name:string) (input:Memory<float32>) = 
    NamedOnnxValue.CreateFromTensor(name, DenseTensor(input,[|1; input.Length|]))

let getEmbeddings (input:NamedOnnxValue) = 
    let results  = pyannoteModel.Value.Run([input])
    results.[0].AsEnumerable<float32>() |> Seq.toArray

let getEmbeddingsFromFile = toSamples >> (toOnnxInput "waveform") >> getEmbeddings

let allPairsDist l1 l2 = 
    List.allPairs l1 l2 
    |> List.filter (fun (a,b) -> a <> b)
    |> List.map(fun (a,b) -> Dist.cosineDistance a b)
    |> List.average
