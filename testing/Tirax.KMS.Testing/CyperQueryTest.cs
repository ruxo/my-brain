using FluentAssertions;
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
    public void MatchComplexObject() {
        var bookmark = QueryNode.Of("Bookmark", ("label", "migration"));
        string query = Cypher.Match(bookmark.LinkTo("MIGRATE", QueryNode.Any, Qualifier.Any).ToPath("p"))
                             .Returns("p")
                             .OrderBy(ResultOrderBy.Desc(ValueTerm.FunctionCall.Of("length", "p")))
                             .Limit(1);
        const string Expected = "MATCH p=(:Bookmark{label:'migration'})-[:MIGRATE]->*()\nRETURN p\nORDER BY length(p) DESC\nLIMIT 1;";
        query.Should().Be(Expected, $"value not match: `{query}`");
    }
}