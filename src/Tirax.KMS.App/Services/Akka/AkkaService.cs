using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.DependencyInjection;
using RZ.Database.Neo4J;
using Tirax.KMS.Akka;
using Tirax.KMS.Database;
using Tirax.KMS.Server;

namespace Tirax.KMS.App.Services.Akka;

public sealed class AkkaService : IHostedService, IAppFacade
{
    readonly IServiceProvider serviceProvider;
    readonly Lazy<ActorSystem> system;
    
    public LibraryFacade PublicLibrary { get; private set; } = new(ActorRefs.Nobody);

    public AkkaService(IServiceProvider serviceProvider, IHostApplicationLifetime appLifetime) {
        this.serviceProvider = serviceProvider;
        system = new(() => {
            var bootstrap = BootstrapSetup.Create()
                                          .WithConfig(ConfigurationFactory.ParseString("akka.actor.ask-timeout = 10s"));
            var diSetup = DependencyResolverSetup.Create(serviceProvider);
            
            var systemOptions = ActorSystemSetup.Create(bootstrap, diSetup);
            var sys = ActorSystem.Create("TiraxKMS", systemOptions);
            sys.WhenTerminated.ContinueWith(_ => appLifetime.StopApplication());
        
            return sys;
        });
    }
    
    public async Task StartAsync(CancellationToken cancellationToken) {
        var sys = system.Value;
            
        var db = serviceProvider.GetRequiredService<INeo4JDatabase>();
        var (root, tags) = await db.Read(async q => (await q.GetHome(), await q.GetTags()));
        
        var publicLibrarian = sys.ActorOf(Props.Create(() => new Librarian(db, root, tags)), "public-librarian");
        PublicLibrary = new(publicLibrarian);
        
        var resolver = DependencyResolver.For(sys);
        var workerProps = resolver.Props<ScheduledWorker>();
        sys.ActorOf(workerProps, "scheduled-worker");
    }

    public Task StopAsync(CancellationToken cancellationToken) => 
        CoordinatedShutdown.Get(system.Value).Run(CoordinatedShutdown.ClrExitReason.Instance);
}