using Neo4j.Driver;

namespace RZ.Database.Neo4J;

public interface IQueryRunner
{
    ValueTask<IResultCursor> Read(string query, object? parameters = null);
    ValueTask<IResultSummary> Write(string query, object? parameters = null);
}

public interface INeo4JTransaction
{
    ValueTask<T> Read<T>(Func<IQueryRunner, ValueTask<T>> handler);
    ValueTask<T> Write<T>(Func<IQueryRunner, ValueTask<T>> handler);
    ValueTask<Unit> Write(Func<IQueryRunner, ValueTask> handler);
}

public interface INeo4JDatabase : INeo4JTransaction
{
    ValueTask<T> RunTransaction<T>(Func<INeo4JTransaction, ValueTask<T>> handler);
    ValueTask<Unit> RunTransaction(Func<INeo4JTransaction, ValueTask> handler);
}