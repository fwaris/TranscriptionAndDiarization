[<AutoOpen>]
module Env
open System.Threading.Tasks
open System
open System.IO
open System.IO
let inline runT (t:Task<'a>) = t.Wait(); t.Result
let inline runA t = t |> Async.RunSynchronously

let ensureDir dir = if Directory.Exists dir |> not then Directory.CreateDirectory dir |> ignore

let KeyVault = Environment.GetEnvironmentVariable("TSX_AZURE_KEYVAULT")
let AppId = Environment.GetEnvironmentVariable("TSX_APP_ID")
let TenantId = Environment.GetEnvironmentVariable("TSX_TENANT_ID")
let ServiceAccount = Environment.GetEnvironmentVariable("TSX_SERVICE_ACCT")
let ServiceAccountPassword = Environment.GetEnvironmentVariable("TSX_SERVICE_ACCT_PWD")