module Log
open Microsoft.Extensions.Logging
type TranscriberLog = class end

let mutable _log : ILogger<TranscriberLog> = Unchecked.defaultof<_>
let info  (msg:string) = if _log <> Unchecked.defaultof<_> then _log.LogInformation(msg)
let warn (msg:string) = if _log <> Unchecked.defaultof<_> then _log.LogWarning(msg)
let error (msg:string) = if _log <> Unchecked.defaultof<_> then _log.LogError(msg)
let exn (exn:exn,msg) = if _log <> Unchecked.defaultof<_> then _log.LogError(exn,msg)
        
        
        
