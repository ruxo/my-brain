using System.Text;

namespace Tirax.KMS.Migration.Core.Query;

public sealed class CreateNode(Neo4JNode node, Seq<LinkTarget> targets) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) => 
        sb.Append("CREATE ").Add(node).Add(targets);
}