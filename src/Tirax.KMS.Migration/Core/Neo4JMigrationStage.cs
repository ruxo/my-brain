using System.Diagnostics;
using Neo4j.Driver;
using Tirax.KMS.Migration.Core.Query;
using Seq = LanguageExt.Seq;

namespace Tirax.KMS.Migration.Core;

public sealed class Neo4JMigrationStage(INeo4JDatabase db, IEnumerable<IMigration> migrations) : IMigrationStage
{
    public async ValueTask Run(Option<Version> targetVersion) {
        Console.WriteLine("Neo4J migration started!");
        Console.WriteLine($"{migrations.Count()} total migrations.");
        Console.WriteLine();

        var migrationHistory = await LoadLatestMigration();
        var targetVersionText = targetVersion.Map(v => v.ToString()).IfNone("latest");

        var toMigrate = migrations.OrderBy(m => m.Version);
        
        if (migrationHistory.Latest.IfSome(out var latest)) {
            var word = latest.Version > targetVersion ? "Downgrade" : "Upgrade";
            if (latest.Version == targetVersion)
                Console.WriteLine($"Latest migration is already at version: {targetVersionText}");
            else
                Console.WriteLine($"{word} to version: {targetVersionText}");
        }
        else {
            Console.WriteLine($"Initialize to version: {targetVersionText}");
            foreach (var m in toMigrate)
                migrationHistory = await Upgrade(migrationHistory, m);
        }
    }

    async ValueTask<MigrationInfo> LoadLatestMigration() {
        const string ResultVar = "p";
        var bookmark = QueryNode.Of("Bookmark", ("label", "migration"));
        string query = Cypher.Match(bookmark.LinkTo("MIGRATE", QueryNode.Any, Qualifier.Any).ToPath(ResultVar))
                             .Returns(ResultVar)
                             .OrderBy(ResultOrderBy.Desc(ValueTerm.FunctionCall.Of("length", ResultVar)))
                             .Limit(1);
        var cursor = await db.Query(query);
        var record = await cursor.TryFirst();
        return record.Map(r => Extract(r.GetPath(ResultVar))).IfNone(new MigrationInfo());
    }

    async ValueTask<MigrationInfo> Upgrade(MigrationInfo history, IMigration migration) {
        await migration.Up();

        var newMigration = new MigrationRecord(Guid.NewGuid(), migration.Version, migration.Name, DateTime.UtcNow);
        var target = newMigration.ToNeo4JNode();
        if (history.Latest.IfSome(out var latest)) {
            string query = Cypher.Match(QueryNode.Of("Migration", "n", ("id", latest.Id.ToString())))
                                 .Create(Neo4JNode.Of(id: "n"), new LinkTarget("MIGRATE", target));
            await db.Execute(query);
        }
        else
            await db.CreateNode(Neo4JNode.Of("Bookmark", ("label", "migration")),
                                new LinkTarget("MIGRATE", target));
        return new(history.Migrations.Add(newMigration));
    }

    static MigrationInfo Extract(IPath p) {
        var path = p.EnumerateNodes();
        Debug.Assert(path.Head.Labels.Contains("Bookmark"));
        return new(path.Tail.Map(MigrationRecord.From));
    }

    readonly record struct MigrationRecord(Guid Id, Version Version, string Name, DateTime Updated)
    {
        public static MigrationRecord From(INode node) =>
            new(Guid.Parse(node.As<string>("id")),
                Version.Parse(node.As<string>("version")),
                node.As<string>("name"),
                DateTime.Parse(node.As<string>("updated")));

        public Neo4JNode ToNeo4JNode() =>
            Neo4JNode.Of("Migration",
                         ("id", Id.ToString()),
                         ("version", Version.ToString()),
                         ("name", Name),
                         ("updated", Updated.ToString("O")));
    }

    sealed record MigrationInfo(Seq<MigrationRecord> Migrations)
    {
        public Option<MigrationRecord> Latest => Migrations.LastOrNone();
        
        public MigrationInfo() : this(Seq.empty<MigrationRecord>()){}
    }
}