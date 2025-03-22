namespace TranscriptionClient

open Microsoft.Extensions.Configuration
open System.IO

module Config =
    let k =  [|254uy; 219uy; 182uy; 127uy; 63uy; 255uy; 139uy; 157uy; 129uy; 240uy; 110uy; 170uy; 152uy; 133uy; 106uy; 130uy|]
    let config = lazy(
        let builder = ConfigurationBuilder()
        builder.SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional=true)
            .Build())

    let node = lazy(config.Value.GetValue("Node", "localhost"))
    let port = lazy(config.Value.GetValue("NodePort",57201))
    let useSSH = lazy(config.Value.GetValue("UseSSH", false))
    let sshUser = lazy(config.Value.GetValue("SSH:User","user not configured"))
    let sshLocalPort = lazy(config.Value.GetValue("SSH:LocalPort",57201))
    let sshPassword = lazy(config.Value.GetValue("SSH:EncPwd","password not configured") |> SimpleCrypt.decr k )

(*
#load "SimpleCrypt.fs"
SimpleCrypt.encr k "..."
*)
