using System.Diagnostics;
using System.Runtime.CompilerServices;
using Neo4j.Driver;

namespace RZ.Database.Neo4J;

public static class Neo4JRecordExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPath GetPath(this IRecord record, string key) => (IPath)record[key];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Seq<INode> EnumerateNodes(this IPath path) => Seq(InternalEnumerateNodes(path));

    static IEnumerable<INode> InternalEnumerateNodes(IPath path) {
        var nodeLookup = path.Nodes.Map(n => (n.ElementId, n)).ToMap();
        var node = path.Start;
        yield return node;
        foreach (var rel in path.Relationships) {
            Debug.Assert(node.ElementId == rel.StartNodeElementId);
            node = nodeLookup[rel.EndNodeElementId];
            yield return node;
        }
    }
}