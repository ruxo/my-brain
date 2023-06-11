using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using Tirax.KMS;
using Tirax.KMS.App.Features.Authentication;
using Tirax.KMS.App.Services.Interop;
using Tirax.KMS.Database;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;

const string DbConnectionKey = "Neo4j";
var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("Initializing app...");
var (homeId, dbConn) = await createAppModel(builder.Configuration);
Console.WriteLine("Home: {0}", homeId);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<IKmsServer, KmsServer>();

builder.Services.AddSingleton<IKmsDatabase>(sp => {
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new Neo4JDatabase(loggerFactory, dbConn);
});

builder.Services.AddSingleton(sp => {
    var server = sp.GetRequiredService<IKmsServer>();
    return new AppModel.ViewModel(homeId, server);
});

builder.Services.AddMudServices();

builder.Services.FixAuth0Endpoints();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(opts => {
            opts.DefaultAuthenticateScheme = opts.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            opts.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
       .AddCookie()
       .AddOpenIdConnect(opts => {
            var c = builder.Configuration;
            opts.Authority = c["Oidc:Authority"]!;
            opts.ClientId = c["Oidc:ClientId"]!;
            opts.ClientSecret = c["Oidc:ClientSecret"]!;
            opts.ResponseType = "code";
            "openid profile tirax:kms:userx".Split(' ').Iter(opts.Scope.Add);
            opts.SaveTokens = true;
        });
builder.Services.AddScoped<KmsJs>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

static async Task<(ConceptId, GenericDbConnection)> createAppModel(IConfiguration config) {
    var connection = config.GetConnectionString(DbConnectionKey) ?? throw new InvalidOperationException($"Need '{DbConnectionKey}' connection");
    ILoggerFactory loggerFactory = new NullLoggerFactory();
    var dbConnection = GenericDbConnection.From(connection);
    var db = new Neo4JDatabase(loggerFactory, dbConnection);
    var server = new KmsServer(loggerFactory.CreateLogger<KmsServer>(), db);
    var home = await server.GetHome();
    return (home.Id, dbConnection);
}