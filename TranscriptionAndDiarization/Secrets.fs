module Secrets 
open System.Security
open System
open System.Net
open System.Net.Http
open System.IO
open System.Threading.Tasks
open Microsoft.Identity.Client
open Microsoft.Identity.Client.Broker
open Azure.Identity
open Azure.Security.KeyVault.Secrets


let kvUri (keyVault:string) = $"https://{keyVault}.vault.azure.net";

let getCreds keyVault keyName = 
    printfn "getting ..."
    let kvUri = kvUri keyVault
    let c = new DefaultAzureCredential()
    let client = new SecretClient(new Uri(kvUri), c);        
    let r = client.GetSecret(keyName)
    r.Value.Value

/// get token using your Azure AD credentials
let getToken (appId:string) (tenantId:string) =
    task {
        let scopes = ["User.Read";"Sites.Read.All"; "Files.Read.All"]
        let publicClientApp = 
            PublicClientApplicationBuilder.Create(appId)
                .WithDefaultRedirectUri()
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                .Build();
        return! publicClientApp.AcquireTokenInteractive(scopes).ExecuteAsync()
    }

/// get token using the Azure Service Account without pop-up browser window

let getTokenForServiceAccount (appId:string) (tenantId:string) (username:string) (password:string) =
    task {
        // Define the scopes for the application permissions
        let scopes = ["User.Read"; "Sites.Read.All"; "Files.Read.All"] // Adjust scopes as needed

        // Create a public client application
        let publicClientApp = 
            PublicClientApplicationBuilder.Create(appId)
                .WithAuthority(AzureCloudInstance.AzurePublic, tenantId)
                .Build()

        // Acquire a token for the application using username and password directly
        return! publicClientApp.AcquireTokenByUsernamePassword(scopes, username, password).ExecuteAsync()
    }
