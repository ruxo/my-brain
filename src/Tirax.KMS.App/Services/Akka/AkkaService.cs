using Akka.Actor;
using Akka.DependencyInjection;
using Tirax.KMS.Akka;

namespace Tirax.KMS.App.Services.Akka;

public sealed class AkkaService : IHostedService, IActorFacade
{
    readonly Lazy<ActorSystem> system;
    
    public AkkaService(IServiceProvider serviceProvider, IHostApplicationLifetime appLifetime) {
        system = new(() => {
            var bootstrap = BootstrapSetup.Create();
            var diSetup = DependencyResolverSetup.Create(serviceProvider);
            var systemOptions = bootstrap.And(diSetup);
            var sys = ActorSystem.Create("TiraxKMS", systemOptions);
            sys.WhenTerminated.ContinueWith(_ => appLifetime.StopApplication());
            return sys;
        });
    }
    
    public Task StartAsync(CancellationToken cancellationToken) {
        var workerProps = DependencyResolver.For(system.Value).Props<ScheduledWorker>();
        system.Value.ActorOf(workerProps, "scheduled-worker");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => 
        CoordinatedShutdown.Get(system.Value).Run(CoordinatedShutdown.ClrExitReason.Instance);
}