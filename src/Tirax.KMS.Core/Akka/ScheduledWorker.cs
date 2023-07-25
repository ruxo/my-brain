using Akka.Actor;
using LanguageExt.UnitsOfMeasure;
using Tirax.KMS.Server;

namespace Tirax.KMS.Akka;

public sealed class ScheduledWorker : ReceiveActor, IWithTimers, IActorFacade
{
    /// <summary>
    /// Internal use!
    /// </summary>
    public ITimerScheduler Timers { get; set; } = null!;
    
    public ScheduledWorker(IKmsServer kmsServer) {
        var now = DateTime.UtcNow;
        var nextHour = now + 1.Hours();
        var nextStartHour = new DateTime(nextHour.Year, nextHour.Month, nextHour.Day, nextHour.Hour, 0, 0, DateTimeKind.Utc);
        var dueTime = nextStartHour - now;
        Timers.StartPeriodicTimer("uptime", Uptime.Default, dueTime, 1.Hours());

        ReceiveAsync<Uptime>(async _ => await kmsServer.RecordUpTime());
    }

    sealed record Uptime
    {
        public static readonly Uptime Default = new();
    }
}