namespace TranscriptionServiceHost


open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Hosting
open YourLibraryNamespace

type Worker() =
    inherit BackgroundService()

    let mutable libraryInstance: YourLibraryClass option = None

    override this.ExecuteAsync(ct: CancellationToken) =
        task {
            libraryInstance <- Some (TranscriptionService())
            libraryInstance.Value.StartProcessing() // Replace with your library's method

            while not ct.IsCancellationRequested do
                do! Task.Delay(1000, ct)
        }

    override this.StopAsync(ct: CancellationToken) =
        task {
            match libraryInstance with
            | Some lib -> lib.StopProcessing() // Clean up resources
            | None -> ()
        }
