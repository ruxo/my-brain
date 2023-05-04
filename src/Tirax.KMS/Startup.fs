#nowarn "0020"

open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Logging
open Tirax.KMS.Server
open Tirax.KMS.Stardog
open MudBlazor.Services

let builder = WebApplication.CreateBuilder(Environment.GetCommandLineArgs())

let connection_string = builder.Configuration.GetConnectionString("Stardog")

builder.Services.AddSingleton<Stardog>(fun sp -> let logger = sp.GetRequiredService<ILogger<Stardog>>()
                                                 let connection = connection_string |> StardogConnection.from
                                                 Stardog(logger, connection))
builder.Services.AddSingleton<Server>()
builder.Services.AddControllersWithViews()
builder.Services.AddServerSideBlazor()
builder.Services.AddFunBlazorServer()
builder.Services.AddMudServices()

let app = builder.Build()

app.UseStaticFiles()

app.MapBlazorHub()
app.MapFunBlazor(Tirax.KMS.Index.page)

app.Run()