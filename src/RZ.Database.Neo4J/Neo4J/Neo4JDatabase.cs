using System.Runtime.CompilerServices;
using Neo4j.Driver;

namespace RZ.Database.Neo4J;

public sealed class Neo4JDatabase : INeo4JDatabase, IAsyncDisposable, IDisposable
{
    readonly IDriver db;
    
    public Neo4JDatabase(GenericDbConnection connection) {
        var auth = from user in connection.User
                   from pass in connection.Password
                   select AuthTokens.Basic(user, pass);
        db = auth.IfSome(out var a)? GraphDatabase.Driver(connection.Host, a) : GraphDatabase.Driver(connection.Host);
    }

    public async ValueTask<T> RunTransaction<T>(Func<INeo4JTransaction, ValueTask<T>> handler) {
        await using var session = db.AsyncSession();
        return await handler(new Transaction(session));
    }

    public async ValueTask<Unit> RunTransaction(Func<INeo4JTransaction, ValueTask> handler) {
        await using var session = db.AsyncSession();
        await handler(new Transaction(session));
        return Unit.Default;
    }

    public async ValueTask<T> Read<T>(Func<IQueryRunner, ValueTask<T>> handler) {
        await using var session = db.AsyncSession();
        return await handler(new TransactionRunner(session));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<T> Write<T>(Func<IQueryRunner, ValueTask<T>> handler) => 
        Read(handler);

    public ValueTask<Unit> Write(Func<IQueryRunner, ValueTask> handler) =>
        Write(async runner => {
            await handler(runner);
            return Unit.Default;
        });

    public async ValueTask DisposeAsync() {
        await db.DisposeAsync();
    }

    public void Dispose() {
        db.Dispose();
    }
    
    sealed class Transaction(IAsyncSession session) : INeo4JTransaction {
        public async ValueTask<T> Read<T>(Func<IQueryRunner, ValueTask<T>> handler) {
            return await session.ExecuteReadAsync(async tx => await handler(new TransactionRunner(tx)));
        }

        public async ValueTask<T> Write<T>(Func<IQueryRunner, ValueTask<T>> handler) {
            return await session.ExecuteWriteAsync(async tx => await handler(new TransactionRunner(tx)));
        }

        public ValueTask<Unit> Write(Func<IQueryRunner, ValueTask> handler) =>
            Write(async runner => {
                await handler(runner);
                return Unit.Default;
            });
    }
    
    sealed class TransactionRunner(IAsyncQueryRunner inquiry) : IQueryRunner {
        public async ValueTask<IResultCursor> Read(string query, object? parameters = null) {
            return await inquiry.RunAsync(query, parameters);
        }

        public async ValueTask<IResultSummary> Write(string query, object? parameters = null) {
            var cursor = await inquiry.RunAsync(query, parameters);
            return await cursor.ConsumeAsync();
        }
    }
}