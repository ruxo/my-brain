#nowarn "0020"

open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Tirax.KMS.Server
open Tirax.KMS.Stardog


let builder = WebApplication.CreateBuilder(Environment.GetCommandLineArgs())

let connection_string = builder.Configuration.GetConnectionString("Stardog")

builder.Services.AddSingleton(connection_string |> StardogConnection.from |> Stardog)
builder.Services.AddSingleton<Server>()
builder.Services.AddControllersWithViews()
builder.Services.AddServerSideBlazor().Services.AddFunBlazorServer()


let app = builder.Build()

app.UseStaticFiles()

app.MapBlazorHub()
app.MapFunBlazor(Tirax.KMS.Index.page)

app.Run()