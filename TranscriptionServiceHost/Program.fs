namespace TranscriptionServiceHost
open System
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

module Pgm = 


    [<EntryPoint>]
    let main argv =
        Host.CreateDefaultBuilder(argv)
            .UseWindowsService()  // Makes it a Windows Service
            .ConfigureServices(fun services -> 
                services.AddHostedService<Worker>() |> ignore
            )
            .Build()
            .Run()

        0
