using System.Text;

namespace Tirax.KMS.Migration.Core.Query;

public sealed class OrderByNode(Seq<ResultOrderBy> orders) : ICypherNode
{
    public StringBuilder ToCommandString(StringBuilder sb) => 
        orders.IsEmpty ? sb : sb.Append("ORDER BY ").Join(',', orders, (inner, order) => order.ToCommandString(inner)).AppendLine();
}