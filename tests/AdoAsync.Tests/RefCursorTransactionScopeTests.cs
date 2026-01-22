using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Execution;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public sealed class RefCursorTransactionScopeTests
{
    [Fact]
    public async Task WithTransactionScopeAsync_StartsAndCommits_WhenNoActiveTransaction()
    {
        var connection = new FakeDbConnection();
        var executor = CreateExecutor(connection);

        var result = await InvokeWithTransactionScopeAsync(executor, tx =>
        {
            tx.Should().NotBeNull();
            return Task.FromResult(123);
        });

        result.Should().Be(123);
        connection.BeginTransactionCalls.Should().Be(1);
        connection.LastTransaction!.CommitCalls.Should().Be(1);
        connection.LastTransaction.RollbackCalls.Should().Be(0);
    }

    [Fact]
    public async Task WithTransactionScopeAsync_StartsAndRollsBack_OnException_WhenNoActiveTransaction()
    {
        var connection = new FakeDbConnection();
        var executor = CreateExecutor(connection);

        var act = async () => await InvokeWithTransactionScopeAsync(executor, _ => throw new InvalidOperationException("boom"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        connection.BeginTransactionCalls.Should().Be(1);
        connection.LastTransaction!.CommitCalls.Should().Be(0);
        connection.LastTransaction.RollbackCalls.Should().Be(1);
    }

    [Fact]
    public async Task WithTransactionScopeAsync_UsesActiveTransaction_AndDoesNotCommitOrRollbackIt()
    {
        var connection = new FakeDbConnection();
        var executor = CreateExecutor(connection);

        // Pretend the caller already started a transaction on the same connection.
        var activeTx = new FakeDbTransaction(connection);
        SetPrivateField(executor, "_activeTransaction", activeTx);

        var result = await InvokeWithTransactionScopeAsync(executor, tx =>
        {
            tx.Should().BeSameAs(activeTx);
            return Task.FromResult(7);
        });

        result.Should().Be(7);
        connection.BeginTransactionCalls.Should().Be(0);
        activeTx.CommitCalls.Should().Be(0);
        activeTx.RollbackCalls.Should().Be(0);
    }

    private static DbExecutor CreateExecutor(FakeDbConnection connection)
    {
        var executor = DbExecutor.Create(new DbOptions
        {
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Fake",
            CommandTimeoutSeconds = 30,
            EnableValidation = true
        });

        // Force the executor to use our fake connection so the helper can start/commit/rollback without a real DB.
        SetPrivateField(executor, "_connection", connection);
        return executor;
    }

    private static async Task<int> InvokeWithTransactionScopeAsync(DbExecutor executor, Func<DbTransaction, Task<int>> action)
    {
        var method = typeof(DbExecutor)
            .GetMethod("WithTransactionScopeAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(int));

        var task = (ValueTask<int>)method.Invoke(executor, new object?[] { CancellationToken.None, action })!;
        return await task.ConfigureAwait(false);
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        instance.GetType()
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(instance, value);
    }

    private sealed class FakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Open;
        private string? _connectionString;

        public int BeginTransactionCalls { get; private set; }
        public FakeDbTransaction? LastTransaction { get; private set; }

        [AllowNull]
        public override string ConnectionString
        {
            get => _connectionString ?? string.Empty;
            set => _connectionString = value;
        }
        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        public override Task OpenAsync(CancellationToken cancellationToken) { _state = ConnectionState.Open; return Task.CompletedTask; }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            BeginTransactionCalls++;
            LastTransaction = new FakeDbTransaction(this);
            return LastTransaction;
        }

        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public int CommitCalls { get; private set; }
        public int RollbackCalls { get; private set; }

        public FakeDbTransaction(DbConnection connection) => _connection = connection;

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;

        public override void Commit() => CommitCalls++;
        public override void Rollback() => RollbackCalls++;

        public override Task CommitAsync(CancellationToken cancellationToken = default) { Commit(); return Task.CompletedTask; }
        public override Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
    }
}
