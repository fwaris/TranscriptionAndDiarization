#load "packages.fsx"
open System
Env.AppId
Env.TenantId
Env.ServiceAccount
Env.ServiceAccountPassword
let token = Secrets.getToken Env.AppId Env.TenantId |> runT
let stoken = Secrets.getTokenForServiceAccount Env.AppId Env.TenantId Env.ServiceAccount Env.ServiceAccountPassword |> runT
stoken.AccessToken

let LIST_URL = Environment.GetEnvironmentVariable("TSX_LIST_URL")