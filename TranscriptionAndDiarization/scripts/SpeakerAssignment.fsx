#r "nuget: Microsoft.SemanticKernel"
#load "../Env.fs"
open System
open System.IO
open Microsoft.SemanticKernel.ChatCompletion
open Microsoft.SemanticKernel.Connectors.OpenAI
open Azure


let inputFolder = @"E:\s\transcription\input"
let vtts = Directory.GetFiles(inputFolder,"*.vtt")
let vtt0 = vtts |> Seq.head

let model = "gpt-4o"
let openAIKey = Environment.GetEnvironmentVariable("OPENAI_KEY")

let  ch = new OpenAI.Chat.ChatClient("gpt-4o",openAIKey)
let client =  OpenAIChatCompletionService(model,openAIKey)
let opts = OpenAIPromptExecutionSettings()

let azureOpenAIkey = ClientModel.ApiKeyCredential "xxxxx"
let azureEndpoint = Uri "https://xxxx.openai.azure.com/openai/deployments/o1/chat/completions?api-version=2024-08-01-preview"
let azureModel = "o1"
let azureClient = Azure.AI.OpenAI.AzureOpenAIClient(azureEndpoint,azureOpenAIkey)

let test() = //test
    let rslt = client.GetChatMessageContentAsync("what is the meaning of life?", opts) |> runT
    printfn "%A" rslt.Content
    
type SpeakerAssignment = {
    SpeakerTag : string
    SpeakerName : string
}
type SpeakerAssignmentList = {SpeakerAssigments : SpeakerAssignment list}

let prompt vtt = $"""
The TRANSCRIPTION below contains the transcription of a conversation of multiple speakers in vtt format.
As it is, the various speakers are identified with tags such as 'SPEAKER_08', 'SPEAKER_01', etc.
Your task to analyze the text and try to find the speaker name for each tag. If you cannot find an associated speaker, 
use 'UNKNOWN' as speaker name. Account for all speakers in the conversation.

TRANSCRIPTION```
{vtt}
```

out the speaker tag and name according the json tags identified.
"""

let runFileAzure (file:string) = 
    async {
        let prompt = prompt (File.ReadAllText file)
        let opts = OpenAIPromptExecutionSettings()
        opts.ResponseFormat <- typeof<SpeakerAssignmentList>
        let client = azureClient.GetChatClient(azureModel)
        let! rslt = client.CompleteChatAsync(OpenAI.Chat.ChatMessage.CreateUserMessage(prompt)) |> Async.AwaitTask
        let json = rslt.Value.Content.[0].Text
        let t = System.Text.Json.JsonSerializer.Deserialize<SpeakerAssignmentList>(json)
        return file,t
    }

let runFile (file:string) = 
    async {
        let prompt = prompt (File.ReadAllText file)
        let opts = OpenAIPromptExecutionSettings()
        opts.ResponseFormat <- typeof<SpeakerAssignmentList>
        let! rslt = client.GetChatMessageContentAsync(prompt,opts) |> Async.AwaitTask
        let json = rslt.Content
        let t = System.Text.Json.JsonSerializer.Deserialize<SpeakerAssignmentList>(json)
        return file,t
    }


let spkrs = vtts |> Seq.map (fun f -> runFile f |> runA) |> Seq.toList

for (f,sp) in spkrs do 
    let text = File.ReadAllText f
    let text2 = 
        (text,sp.SpeakerAssigments |> List.filter(fun x->x.SpeakerName<>"UNKNOWN"))
        ||> List.fold (fun acc sp -> acc.Replace(sp.SpeakerTag,sp.SpeakerName))
    let fn2 = $"{f}.txt"
    File.WriteAllText(fn2,text2)

printfn "%A" (prompt (File.ReadAllText vtts.[0]))
