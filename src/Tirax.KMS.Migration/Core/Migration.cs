using System.Configuration;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RZ.Database;
using RZ.Database.Neo4J;
using TCRB.CoreApp.Helpers;
using Neo4JDatabase = RZ.Database.Neo4J.Neo4JDatabase;

namespace Tirax.KMS.Migration.Core;

public sealed class Migration
{
    public static ValueTask Start(IEnumerable<string> args) => Start(Seq(args));
    public static async ValueTask Start(Seq<string> args) {
        var version = args.HeadOrNone().Map(ParseVersion);
        var config = TCRBConfiguration.CreateDefault();
        
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<INeo4JDatabase>(_ => CreateNeo4JDatabase(config));
        serviceCollection.AddSingleton<IMigrationStage, Neo4JMigrationStage>();
        
        GetMigrationTypes().Iter(t => serviceCollection.AddSingleton(typeof(IMigration), t));
        
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var migrations = serviceProvider.GetServices<IMigrationStage>();

        await Task.WhenAll(migrations.Map(m => m.Run(version).AsTask()));
    }

    static Seq<Type> GetMigrationTypes() {
        var migrationType = typeof(IMigration);
        return Seq(from t in Assembly.GetCallingAssembly().GetTypes()
               where t != migrationType && t.IsAssignableTo(migrationType)
               select t);
    }
    
    static Version ParseVersion(string s) {
        var parts = s.Split('.');
        return new(numAt(0), numAt(1), numAt(2));
        
        int numAt(int pos) => int.Parse(parts[pos]);
    }

    static Neo4JDatabase CreateNeo4JDatabase(IConfiguration configuration) {
        var connection = GenericDbConnection.From(configuration.GetConnectionString("Neo4j")
                                               ?? throw new ConfigurationErrorsException("Missing connection string `Neo4j`"));
        return new(connection);
    }
}