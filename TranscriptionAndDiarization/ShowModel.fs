namespace TranscriptionAndDiarization
open Microsoft.ML.OnnxRuntime

module ModelInfo = 

    let model = @"E:\s\models\sherpa-onnx-reverb-diarization-v2\model.onnx"
    let sherpa = @"E:\s\models\nemo_en_titanet_large.onnx"
    
    let show (file:string) =
        use s_opts = new SessionOptions(LogSeverityLevel=OrtLoggingLevel.ORT_LOGGING_LEVEL_INFO)
        use inf = new InferenceSession(file,s_opts)
        printfn "Model loaded: %s" file
        inf.InputMetadata |> Seq.iter (fun kv  -> printfn "Input: %A" (kv))
        inf.OutputMetadata |> Seq.iter (fun kv  -> printfn "Output: %A" (kv))

