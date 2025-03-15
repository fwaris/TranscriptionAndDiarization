module GraphApi
open System.Net
open System.Net.Http
open System.IO
open FSharp.Control

type DecompressHndlr() as this =
    inherit HttpClientHandler()
    do
        this.AutomaticDecompression <- DecompressionMethods.GZip ||| DecompressionMethods.Deflate

type GraphClient(accessToken:string) as this =
    inherit HttpClient(new DecompressHndlr())
    do
        base.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate")
        this.DefaultRequestHeaders.Authorization <- new Headers.AuthenticationHeaderValue("Bearer", accessToken)

type RContentType =
    {
        name : string
    }

type RValue =
    {
        id : string
        webUrl : string
        contentType: RContentType
    }

type RItem =
    {
        ``@odata.nextLink`` : string
        value : RValue list
    }

type RItemLink =
    {
        ``@microsoft.graph.downloadUrl`` : string
        name :string
    }

let downLoadDoc accessToken folder (linkConstr:string->string) (r:RValue) =
    async {
        try
            use client = new GraphClient(accessToken)
            let! str = client.GetStringAsync(linkConstr r.id) |> Async.AwaitTask
            let d2 = System.Text.Json.JsonSerializer.Deserialize<RItemLink>(str)
            printfn "%A" d2
            let link = d2.``@microsoft.graph.downloadUrl``
            use! str2 = client.GetStreamAsync(link) |> Async.AwaitTask
            use fstr = File.Create($"{folder}/{d2.name}")
            do! str2.CopyToAsync(fstr) |> Async.AwaitTask
            printfn "done %A" r
        with ex ->
            printfn "%s" ex.Message
    }

let downloadDocs accessToken folder linkConstr (rv:RItem) =
    rv.value
    |> List.filter(fun x -> x.contentType.name = "Document")
    |> AsyncSeq.ofSeq
    |> AsyncSeq.iterAsyncParallelThrottled 5 (fun d -> downLoadDoc accessToken folder linkConstr d)

let getList accessToken (url:string) =
    async {
        try
            use client = new GraphClient(accessToken)
            let! str = client.GetStringAsync(url) |> Async.AwaitTask
            let rv = System.Text.Json.JsonSerializer.Deserialize<RItem>(str)
            printfn "done"
            return rv
        with ex ->
            printfn "%s" ex.Message
            return raise ex
    }

let downloadAll getItemsUrl accessToken folder linkConstr =
    Env.ensureDir folder
    async {
        use ctx = new System.Threading.CancellationTokenSource()
        let mutable url = getItemsUrl
        while url <> null do
            printfn $"fetching list with {url}"
            let! rv = getList accessToken (url)
            url <- rv.``@odata.nextLink``
            do! downloadDocs accessToken folder linkConstr rv
        printfn "done downloadAll"
    }

