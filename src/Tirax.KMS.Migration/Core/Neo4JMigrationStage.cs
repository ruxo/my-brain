using System.Diagnostics;
using Neo4j.Driver;
using RZ.Database.Neo4J;
using RZ.Database.Neo4J.Query;
using Seq = LanguageExt.Seq;
using static RZ.Database.Neo4J.Prelude;

namespace Tirax.KMS.Migration.Core;

public sealed class Neo4JMigrationStage(INeo4JDatabase db, IEnumerable<IMigration> migrations) : IMigrationStage
{
    public async ValueTask Run(Option<Version> targetVersion) {
        Console.WriteLine("Neo4J migration started!");
        Console.WriteLine($"{migrations.Count()} total migrations.");
        Console.WriteLine();

        var migrationHistory = await LoadLatestMigration();

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
                Console.WriteLine($"Latest migration is already at version: {toMigrateVersion}");
            else {
                var isUpgrading = latest.Version < toMigrateVersion;
                var word = isUpgrading? "Upgrade" : "Downgrade";
                Console.WriteLine($"{word} to version: {toMigrateVersion}");
                if (isUpgrading)
                    await db.RunTransaction(async tx => {
                        foreach (var migration in toMigrate.Where(m => m.Version > latest.Version))
                            migrationHistory = await Upgrade(tx, migrationHistory, migration);
                    });
                else
                    await db.RunTransaction(async tx => {
                        foreach (var migration in toMigrate.Where(m => m.Version > toMigrateVersion && m.Version <= latest.Version)
                                                           .OrderByDescending(m => m.Version))
                            migrationHistory = await Downgrading(tx, migrationHistory, migration);
                        await RebuildMigrationHistory(tx, migrationHistory);
                    });
            }
        }
        else {
            Console.WriteLine($"Initialize to version: {toMigrateVersion}");
            await db.RunTransaction(async tx => {
                foreach (var m in toMigrate.Where(m => m.Version <= toMigrateVersion))
                    migrationHistory = await Upgrade(tx, migrationHistory, m);
            });
        }
    }

    async ValueTask<MigrationInfo> LoadLatestMigration() {
        const string ResultVar = "p";
        var bookmark = QueryNode.Of("Bookmark", ("label", "migration"), ("latest", true));
        string query = Cypher.Match(bookmark.LinkTo("MIGRATE", QueryNode.Any, Qualifier.Any).ToPath(ResultVar))
                             .Return(ResultVar)
                             .OrderBy(ResultOrderBy.Desc(ValueTerm.FunctionCall.Of("length", Var(ResultVar))))
                             .Limit(1);
        var record = await db.Read(async runner => {
            var cursor = await runner.Read(query);
            return await cursor.TryFirst();
        });
        return record.Map(r => Extract(r.GetPath(ResultVar))).IfNone(new MigrationInfo());
    }

    static async ValueTask<MigrationInfo> Downgrading(INeo4JTransaction tx, MigrationInfo history, IMigration migration) {
        var migrationNode = history.Migrations.Rev().Single(r => r.Version == migration.Version) with{
            Downgraded = DateTime.UtcNow
        };
        
        string query = Cypher.Match(QueryNode.Of("n", "Migration", ("id", migrationNode.Id.ToString())))
                             .Set((("n", "downgraded"), migrationNode.Downgraded.Value.ToString("O")));
        await tx.Write(migration.SchemaDown);
        await tx.Write(async runner => {
            await migration.DataDown(runner);
            await runner.Write(query);
        });

        return history with{
            Migrations = history.Migrations.Map(m => m.Version == migrationNode.Version ? migrationNode : m)
        };
    }

    static async ValueTask<MigrationInfo> RebuildMigrationHistory(INeo4JTransaction tx, MigrationInfo history) {
        var newHistory = new MigrationInfo(history.Version + 1,
                                           history.Migrations
                                                  .TakeWhile(m => m.Downgraded is null)
                                                  .Map(m => m with { Id = Guid.NewGuid() }));

        string query = Cypher.Match(QueryNode.Of("n", "Bookmark", ("version", history.Version)))
                             .Set((("n", "latest"), false));
        await tx.Write(async runner => {
            await runner.Write(query);
            await CreateMigrationHistory(runner, newHistory);
        });
        return newHistory;
    }

    static async ValueTask<MigrationInfo> Upgrade(INeo4JTransaction tx, MigrationInfo history, IMigration migration) {
        var newMigration = new MigrationRecord(Guid.NewGuid(), migration.Version, migration.Name, DateTime.UtcNow);
        var version = history.Version + 1;
        var newHistory = new MigrationInfo(version, history.Migrations.Add(newMigration));

        var bookmarkExisted = history.Latest.IfSome(out var latest);
        if (bookmarkExisted) {
            string bookmarkUpdate = Cypher.Match(("n", "Bookmark", Props(("version", history.Version))))
                                          .Set((("n", "version"), newHistory.Version));
            
            var target = newMigration.ToQueryNode();
            string query = Cypher.Match(QueryNode.Of("n", "Migration", ("id", latest.Id.ToString())))
                                 .Create(P(N(id: "n"), L("MIGRATE", target)));
            await tx.Write(migration.SchemaUp);
            await tx.Write(async runner => {
                await migration.DataUp(runner);
                await runner.Write(bookmarkUpdate);
                await runner.Write(query);
            });
        }
        else {
            await tx.Write(migration.SchemaUp);
            await tx.Write(async runner => {
                await migration.DataUp(runner);
                await CreateMigrationHistory(runner, newHistory);
            });
        }
        return newHistory;
    }

    static async ValueTask<Unit> CreateMigrationHistory(IQueryRunner runner, MigrationInfo history) {
        var node = N(type: "Bookmark", body: Props(("label", "migration"), ("latest", true), ("version", history.Version)));
        var targets = history.Migrations.Map(m => L("MIGRATE", m.ToQueryNode()));
        var n = new CreateNode(Seq1(P(node, targets)));
        var query = n.ToCommandString(new (128)).ToString();
        await runner.Write(query);
        return Unit.Default;
    }

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
                node.Properties.TryGetValue("downgraded").Map(v => DateTime.Parse((string)v)).ToNullable());

        public Neo4JNode ToNeo4JNode() =>
            Neo4JNode.Of("Migration",
                         ("id", Id.ToString()),
                         ("version", Version.ToString()),
                         ("name", Name),
                         ("updated", Updated.ToString("O")),
                         ("downgraded", Downgraded?.ToString("O")));

        public QueryNode ToQueryNode() =>
            N(id: Id.ToString(),
              type: "Migration",
              body: Props(("version", Version.ToString()),
                          ("name", Name),
                          ("updated", Updated.ToString("O")),
                          ("downgraded", Downgraded?.ToString("O"))));
    }

    sealed record MigrationInfo(int Version, Seq<MigrationRecord> Migrations)
    {
        public Option<MigrationRecord> Latest => Migrations.LastOrNone();
        
        public MigrationInfo() : this(0, Seq.empty<MigrationRecord>()){}
    }
}