using MudBlazor.Services;
using Tirax.KMS.Database;
using Tirax.KMS.Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<IKmsServer, KmsServer>();

const string DbConnectionKey = "Neo4j";
builder.Services.AddSingleton<IKmsDatabase>(sp => {
    var connection = sp.GetRequiredService<IConfiguration>().GetConnectionString(DbConnectionKey)
                  ?? throw new InvalidOperationException($"Need '{DbConnectionKey}' connection");
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return new Neo4JDatabase(loggerFactory, GenericDbConnection.From(connection));
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