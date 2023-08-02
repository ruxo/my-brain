using Tirax.KMS.Migration.Core;

namespace Tirax.KMS.Migration.Migrations;

public sealed class Initial : IMigration {
    readonly INeo4JDatabase database;
    public Initial(INeo4JDatabase database) {
        this.database = database;
    }
    
    public async ValueTask Up() {
        await database.CreateUniqueConstraint("Tag", "name");
        await database.CreateUniqueConstraint("LinkObject", "uri");
        await database.CreateIndex("Concept", "name");
        await database.CreateIndex("Bookmark", "label");
        await database.CreateFullTextIndex("conceptNameIndex", "Concept", "name");
        await database.CreateFullTextIndex("linkObjectNameIndex", "LinkObject", "name");

        var home = new Neo4JNode("Bookmark", Seq1(("label", "home")));
        var brain = new Neo4JNode("Concept", Seq1(("name", "Brain")));
        await database.CreateNode(home, new LinkTarget("POINT", brain));
    }

    public async ValueTask Down() {
        var home = new Neo4JNode("Bookmark", Seq1(("label", "home")));
        var brain = new Neo4JNode("Concept", Seq1(("name", "Brain")));
        await database.DeleteNodes(home, brain);
        await database.DeleteFullTextIndex("linkObjectNameIndex");
        await database.DeleteFullTextIndex("conceptNameIndex");
        await database.DeleteIndex("Bookmark", "label");
        await database.DeleteIndex("Concept", "name");
        await database.DeleteUniqueConstraint("LinkObject", "uri");
        await database.DeleteUniqueConstraint("Tag", "name");
    }

    public string Name => "Initial";
    public Version Version => new(0, 1, 0);
}