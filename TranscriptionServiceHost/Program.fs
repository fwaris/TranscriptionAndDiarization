namespace TranscriptionServiceHost
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open Microsoft.AspNetCore.Builder

open Microsoft.AspNetCore.Hosting
open TranscriptionInterop


module Pgm = 

    type Startup() =
        member _.ConfigurServices(services : IServiceCollection) =
            ()

        member _.Configure ( app : IApplicationBuilder, env:  IWebHostEnvironment) =
            app
                .UseRouting()
                //.UseCors(fun p -> p.AllowAnyOrigin() |> ignore)                 
                .UseEndpoints(fun eps -> eps.MapHub<TranscriberHub>("/hub") |> ignore)
            |> ignore
        

    [<EntryPoint>]
    let main argv =
        let app =
            let builder = Host.CreateDefaultBuilder()
            builder
                // .ConfigureLogging(fun l -> 
                //     l.ClearProviders() |> ignore
                //     l.AddEventLog(fun s -> 
                //         s.SourceName <- "Transcriber"
                //         s.LogName <- "Application"
                //     )
                //     |> ignore
                // )
                .UseWindowsService()  // Makes it a Windows Service
                
                .ConfigureServices(fun services -> 
                    services
                        .AddSignalR()
                        .AddJsonProtocol(fun o -> o.PayloadSerializerOptions <- Ser.serOptions())
                        |> ignore
                    services.AddHostedService<Service>() |> ignore
                )                
                .ConfigureWebHostDefaults(fun wb -> 
                    wb
                        .UseStartup<Startup>()
                    |> ignore
                )
                .Build()
        app
            .Run()
        0
