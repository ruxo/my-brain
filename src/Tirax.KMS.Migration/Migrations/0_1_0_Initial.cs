using RZ.Database.Neo4J;
using Tirax.KMS.Migration.Core;

namespace Tirax.KMS.Migration.Migrations;

public sealed class Initial : IMigration {
    public async ValueTask SchemaUp(IQueryRunner runner) {
        await runner.CreateUniqueConstraint("Migration", "id");
        await runner.CreateUniqueConstraint("Tag", "name");
        await runner.CreateUniqueConstraint("LinkObject", "uri");
        await runner.CreateIndex("Concept", "name");
        await runner.CreateIndex("Bookmark", Seq("label", "latest"));
        await runner.CreateFullTextIndex("conceptNameIndex", "Concept", "name");
        await runner.CreateFullTextIndex("linkObjectNameIndex", "LinkObject", "name");
    }
    
    public async ValueTask DataUp(IQueryRunner runner) {
        var home = new Neo4JNode("Bookmark", Seq1(("label", "home")));
        var brain = new Neo4JNode("Concept", Seq1(("name", "Brain")));
        await runner.CreateNode(home, new LinkTarget("POINT", brain));
    }

    public async ValueTask DataDown(IQueryRunner runner) {
        var home = new Neo4JNode("Bookmark", Seq1(("label", "home")));
        var brain = new Neo4JNode("Concept", Seq1(("name", "Brain")));
        await runner.DeleteNodes(home, brain);
    }

    public async ValueTask SchemaDown(IQueryRunner runner) {
        await runner.DeleteFullTextIndex("linkObjectNameIndex");
        await runner.DeleteFullTextIndex("conceptNameIndex");
        await runner.DeleteIndex("Bookmark", "label");
        await runner.DeleteIndex("Concept", "name");
        await runner.DeleteUniqueConstraint("LinkObject", "uri");
        await runner.DeleteUniqueConstraint("Tag", "name");
    }

    public string Name => "Initial";
    public Version Version => new(0, 1, 0);
}