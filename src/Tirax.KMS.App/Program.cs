using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Services;
using Tirax.KMS;
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

async Task<(ConceptId, GenericDbConnection)> createAppModel(IConfiguration config) {
    var connection = config.GetConnectionString(DbConnectionKey) ?? throw new InvalidOperationException($"Need '{DbConnectionKey}' connection");
    ILoggerFactory loggerFactory = new NullLoggerFactory();
    var dbConnection = GenericDbConnection.From(connection);
    var db = new Neo4JDatabase(loggerFactory, dbConnection);
    var server = new KmsServer(loggerFactory.CreateLogger<KmsServer>(), db);
    var home = await server.GetHome();
    return (home.Id, dbConnection);
}