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

        var toMigrate = Seq(migrations.OrderBy(m => m.Version));

        var invalidVersions = toMigrate.GroupBy(m => m.Version).Where(g => g.Count() > 1).Select(g => g.Key).ToSeq();
        if (invalidVersions.Any()) {
            Console.WriteLine("Following migration version is duplicated:");
            invalidVersions.Iter(v => Console.WriteLine("- {0}", v));
            return;
        }
        
        var toMigrateVersion = targetVersion.IfNone(toMigrate.Last().Version);

        if (toMigrate.All(m => m.Version != toMigrateVersion)) {
            Console.WriteLine($"Target version {toMigrateVersion} is not valid");
            Console.Write("Valid versions:");
            toMigrate.Iter(m => Console.WriteLine($"\t{m.Version}"));
            return;
        }
        
        if (migrationHistory.Latest.IfSome(out var latest)) {
            if (latest.Version == toMigrateVersion)
                Console.WriteLine($"Latest migration is already at version: {targetVersionText}");
            else {
                var isUpgrading = latest.Version < toMigrateVersion;
                var word = isUpgrading? "Upgrade" : "Downgrade";
                Console.WriteLine($"{word} to version: {targetVersionText}");
                if (isUpgrading)
                    foreach(var migration in toMigrate.Where(m => m.Version > latest.Version))
                        migrationHistory = await Upgrade(migrationHistory, migration);
                else {
                    foreach(var migration in toMigrate.Where(m => m.Version > toMigrateVersion && m.Version <= latest.Version)
                                                      .OrderByDescending(m => m.Version))
                        migrationHistory = await Downgrading(migrationHistory, migration);
                    await RebuildMigrationHistory(migrationHistory);
                }
            }
        }
        else {
            Console.WriteLine($"Initialize to version: {targetVersionText}");
            foreach (var m in toMigrate)
                migrationHistory = await Upgrade(migrationHistory, m);
        }
    }

    async ValueTask<MigrationInfo> LoadLatestMigration() {
        const string ResultVar = "p";
        var bookmark = QueryNode.Of("Bookmark", ("label", "migration"), ("latest", true));
        string query = Cypher.Match(bookmark.LinkTo("MIGRATE", QueryNode.Any, Qualifier.Any).ToPath(ResultVar))
                             .Returns(ResultVar)
                             .OrderBy(ResultOrderBy.Desc(ValueTerm.FunctionCall.Of("length", ResultVar)))
                             .Limit(1);
        var cursor = await db.Query(query);
        var record = await cursor.TryFirst();
        return record.Map(r => Extract(r.GetPath(ResultVar))).IfNone(new MigrationInfo());
    }

    async ValueTask<MigrationInfo> Downgrading(MigrationInfo history, IMigration migration) {
        var migrationNode = history.Migrations.Rev().Single(r => r.Version == migration.Version) with{
            Downgraded = DateTime.UtcNow
        };
        await migration.Down();

        string query = Cypher.Match(QueryNode.Of("Migration", "n", ("id", migrationNode.Id.ToString())))
                             .Set((("n", "downgraded"), migrationNode.Downgraded.Value.ToString("O")));
        await db.Execute(query);

        return history with{
            Migrations = history.Migrations.Map(m => m.Version == migrationNode.Version ? migrationNode : m)
        };
    }

    async ValueTask<MigrationInfo> RebuildMigrationHistory(MigrationInfo history) {
        var newHistory = new MigrationInfo(Version: history.Version + 1, Migrations: history.Migrations.TakeWhile(m => m.Downgraded is null));

        string query = Cypher.Match(QueryNode.Of("Bookmark", "n", ("version", history.Version)))
                             .Set((("n", "latest"), false));
        await db.Execute(query);
        await db.CreateNode(Neo4JNode.Of("Bookmark", ("label", "migration"), ("latest", true), ("version", newHistory.Version)),
                            newHistory.Migrations.Map(m => new LinkTarget("MIGRATE", m.ToNeo4JNode())).ToArray());
        return newHistory;
    }

    async ValueTask<MigrationInfo> Upgrade(MigrationInfo history, IMigration migration) {
        await migration.Up();

        var newMigration = new MigrationRecord(Guid.NewGuid(), migration.Version, migration.Name, DateTime.UtcNow);
        var target = newMigration.ToNeo4JNode();
        var version = history.Version + 1;
        var newHistory = new MigrationInfo(version, history.Migrations.Add(newMigration));
        
        if (newHistory.Latest.IfSome(out var latest)) {
            string bookmarkUpdate = Cypher.Match(QueryNode.Of("Bookmark", "n", ("version", history.Version)))
                                          .Set((("n", "version"), newHistory.Version));
            string query = Cypher.Match(QueryNode.Of("Migration", "n", ("id", latest.Id.ToString()), ("latest", true)))
                                 .Create(Neo4JNode.Of(id: "n"), new LinkTarget("MIGRATE", target));
            await db.Execute(Seq(bookmarkUpdate, query).Join('\n'));
        }
        else
            await CreateMigrationHistory(newHistory);
        return newHistory;
    }

    ValueTask CreateMigrationHistory(MigrationInfo history) =>
        db.CreateNode(Neo4JNode.Of("Bookmark", ("label", "migration"), ("latest", true), ("version", history.Version)),
                      history.Migrations.Map(m => new LinkTarget("MIGRATE", m.ToNeo4JNode())).ToArray());

    static MigrationInfo Extract(IPath p) {
        var path = p.EnumerateNodes();
        Debug.Assert(path.Head.Labels.Contains("Bookmark"));
        var version = path.Head["version"].As<int>();
        return new(version, path.Tail.Map(MigrationRecord.From));
    }

    readonly record struct MigrationRecord(Guid Id, Version Version, string Name, DateTime Updated, DateTime? Downgraded = null)
    {
        public static MigrationRecord From(INode node) =>
            new(Guid.Parse((string)node["id"]),
                Version.Parse(node["version"].As<string>()),
                node["name"].As<string>(),
                DateTime.Parse(node["updated"].As<string>()),
                Optional(node["downgraded"].As<string?>()).Map(DateTime.Parse).ToNullable());

        public Neo4JNode ToNeo4JNode() =>
            Neo4JNode.Of("Migration",
                         ("id", Id.ToString()),
                         ("version", Version.ToString()),
                         ("name", Name),
                         ("updated", Updated.ToString("O")),
                         ("downgraded", Downgraded?.ToString("O")));
    }

    sealed record MigrationInfo(int Version, Seq<MigrationRecord> Migrations)
    {
        public Option<MigrationRecord> Latest => Migrations.LastOrNone();
        
        public MigrationInfo() : this(0, Seq.empty<MigrationRecord>()){}
    }
}