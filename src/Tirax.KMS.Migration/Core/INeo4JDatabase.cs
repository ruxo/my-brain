namespace Tirax.KMS.Migration.Core;

public interface INeo4JDatabase
{
    ValueTask CreateUniqueConstraint(string nodeType, string field);
    ValueTask DeleteUniqueConstraint(string nodeType, string field);

    ValueTask CreateIndex(string nodeType, NodeFields fields);
    ValueTask DeleteIndex(string nodeType, NodeFields fields);
    
    ValueTask CreateFullTextIndex(string indexName, string nodeType, NodeFields fields);
    ValueTask DeleteFullTextIndex(string indexName);

    ValueTask CreateNode(Neo4JNode node, params LinkTarget[] targets);
    ValueTask DeleteNodes(params Neo4JNode[] nodes);
}