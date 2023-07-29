using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Tirax.KMS.Migration.Utils;

public sealed class Migration
{
    public static ValueTask Start(IEnumerable<string> args) => Start(Seq(args));
    public static async ValueTask Start(Seq<string> args) {
        var serviceCollection = new ServiceCollection();
        GetMigrationTypes().Iter(t => serviceCollection.AddSingleton(typeof(IMigration), t));
        var serviceProvider = serviceCollection.BuildServiceProvider();

        var migrations = serviceProvider.GetServices<IMigration>().OrderBy(m => m.Version, SemVer.Comparer).ToSeq();
        
        Console.WriteLine($"{migrations.Count()} total migrations.");
        Console.WriteLine();
        Console.WriteLine("Migrating to version: ...");

        foreach (var m in migrations) await m.Up();
    }

    static Seq<Type> GetMigrationTypes() {
        var migrationType = typeof(IMigration);
        return Seq(from t in Assembly.GetCallingAssembly().GetTypes()
               where t != migrationType && t.IsAssignableTo(migrationType)
               select t);
    }
}