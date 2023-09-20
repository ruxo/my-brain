using System.Diagnostics;
using Neo4j.Driver;
using Tirax.KMS.Domain;

namespace Tirax.KMS.Database;

public static class NodeLabels
{
    public const string Tag = "Tag";
    public const string LinkObject = "LinkObject";
    public const string Concept = "Concept";
    public const string Bookmark = "Bookmark";
}

static class Materialization
{
    public static ConceptTag ToConceptTag(this IRecord record) {
        var node = record["tag"].As<INode>();
        Debug.Assert(node.Labels.Single() == NodeLabels.Tag);
        return new(node.ElementId, node["name"].As<string>());
    }

    public static Concept ToConcept(this IRecord record) {
        var node = record["concept"].As<INode>();
        var contains = record.ReadConceptIdList("contains");
        var links = record.ReadConceptIdList("links");
        var tags = record.ReadConceptIdList("tags");
        
        Debug.Assert(node.Labels.Single() == NodeLabels.Concept);
        return new(node.ElementId, node["name"].As<string>()){
            Contains = toHashSet(contains),
            Links = toHashSet(links),
            Tags = toHashSet(tags)
        };
    }

    public static LinkObject ToLinkObject(this IRecord record) {
        var node = record["link"].As<INode>();
        return new(node.ElementId, Optional(node["name"].As<string>()), new(node["uri"].As<string>()));
    }

    public static (ConceptId, float) ToSearchConceptResult(this IRecord record) {
        var score = record["score"].As<float>();
        var conceptId = record["conceptId"].As<string>();
        return (conceptId, score);
    }

    static Seq<ConceptId> ReadConceptIdList(this IRecord record, string fieldName) =>
        record[fieldName].As<IEnumerable<object>>().ToSeq().Map(o => new ConceptId(o.As<string>()));
}