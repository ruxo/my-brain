using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Logging;
using MudBlazor.Services;
using RZ.Database;
using Tirax.KMS;
using Tirax.KMS.Akka;
using Tirax.KMS.App.Features.Authentication;
using Tirax.KMS.App.Services.Akka;
using Tirax.KMS.App.Services.Interop;
using Tirax.KMS.Database;
using Tirax.KMS.Domain;
using Tirax.KMS.Server;

const string DbConnectionKey = "Neo4j";
var builder = WebApplication.CreateBuilder(args);

IdentityModelEventSource.ShowPII = builder.Environment.IsDevelopment();

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
       .AddCookie(opts => {
            opts.SlidingExpiration = false;
            opts.Cookie.HttpOnly = true;
            opts.Cookie.SameSite = SameSiteMode.Strict;
            opts.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        })
       .AddOpenIdConnect(opts => {
            var c = builder.Configuration;
            var audience = c["Oidc:Audience"]!;
            var authority = c["Oidc:Authority"]!;
            const string RequiredScopes = "openid profile";
            
            opts.Authority = authority;
            opts.ClientId = c["Oidc:ClientId"]!;
            opts.ClientSecret = c["Oidc:ClientSecret"]!;
            opts.ResponseType = "code";
            RequiredScopes.Split(' ').Iter(opts.Scope.Add);
            opts.SaveTokens = true;
            opts.UseTokenLifetime = true;

            var tokenValidation = opts.TokenValidationParameters;
            tokenValidation.ValidIssuer = authority;
            tokenValidation.ValidateIssuer = true;
            tokenValidation.ValidateIssuerSigningKey = true;
            tokenValidation.ValidateLifetime = true;

            opts.Events.OnRedirectToIdentityProvider = ctx => {
                ctx.ProtocolMessage.SetParameter("audience", audience);
                return Task.CompletedTask;
            };
            opts.Events.OnTokenValidated += ctx => {
                var accessToken = ctx.TokenEndpointResponse!.AccessToken;
                var jwtReader = new JwtSecurityTokenHandler();
                var jwt = jwtReader.ReadJwtToken(accessToken);
                var tokenIsValid = jwt.Audiences.Contains(audience);
                Console.WriteLine("Allowed Audiences: {0}", jwt.Audiences.Join(", "));
                if (!tokenIsValid) ctx.Fail("Required audience not found");

                var permissions = jwt.Claims.ToSeq().Where(claim => claim.Type == KmsPrincipal.PermissionsClaim);
                var additionalClaims = Seq1(new Claim(KmsPrincipal.AccessTokenClaim, accessToken)).Append(permissions);
                ctx.Principal!.AddIdentity(new(additionalClaims));
                return Task.CompletedTask;
            };
        });
builder.Services.AddScoped<KmsJs>();

builder.Services.AddAuthorizationCore(opts => {
    opts.AddPolicy(KmsAuthPolicy.Authenticated, b => b.RequireAuthenticatedUser());
    opts.AddPolicy(KmsAuthPolicy.User, b => b.RequireClaim(KmsPrincipal.PermissionsClaim, KmsAuthPolicy.User));
    opts.DefaultPolicy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .RequireClaim(KmsPrincipal.PermissionsClaim, KmsAuthPolicy.User)
                        .RequireClaim(KmsPrincipal.AccessTokenClaim)
                        .Build();
});

builder.Services.AddSingleton<IActorFacade, AkkaService>();
builder.Services.AddHostedService<AkkaService>(sp => (AkkaService)sp.GetRequiredService<IActorFacade>());

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