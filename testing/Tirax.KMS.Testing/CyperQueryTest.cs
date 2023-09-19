using FluentAssertions;
using RZ.Database.Neo4J.Query;
using Tirax.KMS.Database;
using static RZ.Database.Neo4J.Query.ValueTerm;
using static RZ.Database.Neo4J.Prelude;

namespace Tirax.KMS.Testing;

public sealed class CyperQueryTest
{
    [Fact]
    public void MatchSimpleObject() {
        string query = Cypher.Match(QueryNode.Of("n", "Bookmark", ("id", "1234"))).Return("n");

        query.Should().Be("MATCH (n:Bookmark{id:'1234'})\nRETURN n;", $"value not match: `{query}`");
    }

    [Fact]
    public void MigrationQuery() {
        var bookmark = QueryNode.Of("Bookmark", ("label", "migration"));
        string query = Cypher.Match(bookmark.LinkTo("MIGRATE", QueryNode.Any, Qualifier.Any).ToPath("p"))
                             .Return("p")
                             .OrderBy(ResultOrderBy.Desc(FunctionCall.Of("length", Var("p"))))
                             .Limit(1);
        const string Expected = "MATCH p=(:Bookmark{label:'migration'})-[:MIGRATE]->*()\nRETURN p\nORDER BY length(p) DESC\nLIMIT 1;";
        query.Should().Be(Expected, $"value not match: `{query}`");
    }

    [Fact]
    public void QueryExistingMigration() {
        var target = N(type: "Migration", body: ("id", "5678"));
        string query = Cypher.Match(QueryNode.Of("n", "Migration", ("id", "1234")))
                             .Create(P(N(id: "n"), L("MIGRATE", target)));

        const string Expected = "MATCH (n:Migration{id:'1234'})\nCREATE (n)-[:MIGRATE]->(:Migration{id:'5678'});";
        query.Should().Be(Expected, $"value not match: `{query}`");
    }

    [Fact]
    public void SimpleMatchAndSet() {
        string query = Cypher.Match(("n", "Migration")).Set((("n", "version"), 123));

        query.Should().Be("MATCH (n:Migration)\nSET n.version=123;", $"value not match: `{query}`");
    }

    [Fact]
    public void TestProjections() {
        string query =
            Cypher.Match(("concept", "Concept"))
                  .Where(FunctionCall.Of("elementId", Var("concept")) == Param("conceptId"))
                  .Return(Direct(Var("concept")),
                          Alias("contains",
                                Select(QueryNode.AnyWithId("concept").LinkTo("CONTAINS", QueryNode.AnyWithId("sub")), Call("elementId", Var("sub")))),
                          Alias("links",
                                Select(QueryNode.AnyWithId("concept").LinkTo("REFERS", QueryNode.AnyWithId("links")), Call("elementId", Var("links")))),
                          Alias("tags", Select(QueryNode.AnyWithId("concept").LinkTo("TAGS", QueryNode.AnyWithId("tags")), Call("elementId", Var("tags")))));
        query.Should()
             .Be("MATCH (concept:Concept)\nWHERE elementId(concept)=$conceptId\n" +
                 "RETURN concept,[(concept)-[:CONTAINS]->(sub)|elementId(sub)] AS contains,[(concept)-[:REFERS]->(links)|elementId(links)] AS links," +
                 "[(concept)-[:TAGS]->(tags)|elementId(tags)] AS tags;");
    }

    [Fact]
    public void CreateLinkQueryTest() {
        KmsDatabaseOperations.CreateLinkQuery.Should()
                             .Be("MATCH (owner:Concept)\nWHERE elementId(owner)=$oid\nCREATE (link:LinkObject{name:$name,uri:$uri}),(owner)-[:REFERS]->(link)\n" +
                                 "RETURN link;");
    }
}