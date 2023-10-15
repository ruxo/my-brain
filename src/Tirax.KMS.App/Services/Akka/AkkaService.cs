﻿using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.DependencyInjection;
using Tirax.KMS.Akka;
using Tirax.KMS.Server;

namespace Tirax.KMS.App.Services.Akka;

public sealed class AkkaService : IHostedService, IAppFacade
{
    readonly IServiceProvider serviceProvider;
    readonly Lazy<ActorSystem> system;
    
    public IActorRef PublicLibrarian { get; private set; } = ActorRefs.Nobody;
    
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
            
        var kmsSystem = serviceProvider.GetRequiredService<IKmsServer>();
        var publicRoot = await kmsSystem.GetHome();
        var publicTags = await kmsSystem.GetTags();

        PublicLibrarian = sys.ActorOf(Props.Create(() => new Librarian(kmsSystem, publicRoot, publicTags)), "public-librarian");
        
        var resolver = DependencyResolver.For(sys);
        var workerProps = resolver.Props<ScheduledWorker>();
        sys.ActorOf(workerProps, "scheduled-worker");
    }

    public Task StopAsync(CancellationToken cancellationToken) => 
        CoordinatedShutdown.Get(system.Value).Run(CoordinatedShutdown.ClrExitReason.Instance);
}