using FluentAssertions;
using Tirax.KMS.Migration.Core;
using Tirax.KMS.Migration.Core.Query;

namespace Tirax.KMS.Testing;

public sealed class CyperQueryTest
{
    [Fact]
    public void MatchSimpleObject() {
        string query = Cypher.Match(QueryNode.Of("Bookmark", "n", ("id", "1234"))).Returns("n");

        query.Should().Be("MATCH (n:Bookmark{id:'1234'})\nRETURN n;", $"value not match: `{query}`");
    }

    [Fact]
    public void MigrationQuery() {
        var bookmark = QueryNode.Of("Bookmark", ("label", "migration"));
        string query = Cypher.Match(bookmark.LinkTo("MIGRATE", QueryNode.Any, Qualifier.Any).ToPath("p"))
                             .Returns("p")
                             .OrderBy(ResultOrderBy.Desc(ValueTerm.FunctionCall.Of("length", ValueTerm.Var("p"))))
                             .Limit(1);
        const string Expected = "MATCH p=(:Bookmark{label:'migration'})-[:MIGRATE]->*()\nRETURN p\nORDER BY length(p) DESC\nLIMIT 1;";
        query.Should().Be(Expected, $"value not match: `{query}`");
    }

    [Fact]
    public void QueryExistingMigration() {
        var target = Neo4JNode.Of("Migration", ("id", "5678"));
        string query = Cypher.Match(QueryNode.Of("Migration", "n", ("id", "1234")))
                             .Create(Neo4JNode.Of(id: "n"), new LinkTarget("MIGRATE", target));

        const string Expected = "MATCH (n:Migration{id:'1234'})\nCREATE (n)-[:MIGRATE]->(:Migration{id:'5678'});";
        query.Should().Be(Expected, $"value not match: `{query}`");
    }

    [Fact]
    public void SimpleMatchAndSet() {
        string query = Cypher.Match(QueryNode.Of("Migration", "n")).Set((("n", "version"), 123));

        query.Should().Be("MATCH (n:Migration)\nSET n.version=123;", $"value not match: `{query}`");
    }
}