namespace TranscriptionClient

open Microsoft.Extensions.Configuration
open System.IO

module Config =

    let config = lazy(        
        let builder = ConfigurationBuilder()
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional=false, reloadOnChange=true)
            .Build())

    let node = config.Value.["Node"].Trim()
    let port = config.Value.["Port"].Trim()
    let useSSH = match bool.TryParse(config.Value["UseSSH"].Trim()) with true,v -> v | _ -> false

