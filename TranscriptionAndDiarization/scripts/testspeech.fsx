#load "packages.fsx"
open System
open System.Collections.Generic
open Microsoft.ML.OnnxRuntime
let model = @"E:\s\models\sherpa-onnx-reverb-diarization-v2\model.onnx"
let sherpa = @"E:\s\models\nemo_en_titanet_small.onnx"

let silero = @"E:\s\models\silero_vad.onnx"

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


ModelInfo.show model
ModelInfo.show sherpa
ModelInfo.show silero






