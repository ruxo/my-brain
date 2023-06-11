using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Tirax.KMS.App.Features.Authentication;

public static class Auth0OidcConfigurator
{
    public static void FixAuth0Endpoints(this IServiceCollection services) {
        services.AddSingleton<IPostConfigureOptions<OpenIdConnectOptions>, Auth0PostConfiguration>();
    }
    
    sealed class Auth0PostConfiguration : IPostConfigureOptions<OpenIdConnectOptions> {
        readonly IPostConfigureOptions<OpenIdConnectOptions> baseConfigurator;
        public Auth0PostConfiguration(IDataProtectionProvider dp) {
            baseConfigurator = new OpenIdConnectPostConfigureOptions(dp);
        }

        public void PostConfigure(string? name, OpenIdConnectOptions options) {
            baseConfigurator.PostConfigure(name, options);
            options.ConfigurationManager = new Auth0ConfigurationManager(options, options.ConfigurationManager!);
        }
    }
    
    sealed class Auth0ConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration> {
        readonly OpenIdConnectOptions options;
        readonly IConfigurationManager<OpenIdConnectConfiguration> baseConfigurator;
        public Auth0ConfigurationManager(OpenIdConnectOptions options, IConfigurationManager<OpenIdConnectConfiguration> baseConfigurator) {
            this.options = options;
            this.baseConfigurator = baseConfigurator;
        }

        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) {
            var config = await baseConfigurator.GetConfigurationAsync(cancel);
            // Auth0 OIDC configuration does not return End Session endpoint!
            if (string.IsNullOrEmpty(config.EndSessionEndpoint))
                config.EndSessionEndpoint = $"{options.Authority}v2/logout?client_id={options.ClientId}";
            return config;
        }

        public void RequestRefresh() => baseConfigurator.RequestRefresh();
    }
}