namespace Tirax.KMS.Migration.Core;

public class Neo4JMigrationStage(IEnumerable<IMigration> migrations) : IMigrationStage
{
    public async ValueTask Run(Option<Version> targetVersion) {
        Console.WriteLine("Neo4J migration started!");
        Console.WriteLine($"{migrations.Count()} total migrations.");
        Console.WriteLine();
        Console.WriteLine("Migrating to version: {0}", targetVersion.Map(v => v.ToString()).IfNone("latest"));

        foreach (var m in migrations) await m.Up();
    }
}