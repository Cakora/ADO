using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class TransactionBehaviorTests
{
    [Fact]
    public async Task TransactionManager_RollsBackOnDispose_WhenNotCommitted()
    {
        await using var connection = new FakeDbConnection();
        var manager = new AdoAsync.Transactions.TransactionManager(connection);

        var handle = await manager.BeginAsync(connection);
        await handle.DisposeAsync();

        connection.Transaction!.RollbackCalled.Should().BeTrue();
        connection.Transaction!.CommitCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TransactionManager_DoesNotRollbackOnDispose_AfterCommit()
    {
        await using var connection = new FakeDbConnection();
        var manager = new AdoAsync.Transactions.TransactionManager(connection);

        await using var handle = await manager.BeginAsync(connection);
        await handle.CommitAsync();

        connection.Transaction!.CommitCalled.Should().BeTrue();
        connection.Transaction!.RollbackCalled.Should().BeFalse();
    }

    [Fact]
    public async Task TransactionManager_BeginTwice_Throws()
    {
        await using var connection = new FakeDbConnection();
        var manager = new AdoAsync.Transactions.TransactionManager(connection);

        await using var first = await manager.BeginAsync(connection);

        var ex = await Assert.ThrowsAsync<AdoAsync.DbCallerException>(() => manager.BeginAsync(connection).AsTask());
        ex.MessageKey.Should().Be("errors.state");
    }

    [Fact]
    public async Task TransactionManager_CommitWithoutBegin_Throws()
    {
        await using var connection = new FakeDbConnection();
        var manager = new AdoAsync.Transactions.TransactionManager(connection);

        var ex = await Assert.ThrowsAsync<AdoAsync.DbCallerException>(() => manager.CommitAsync().AsTask());
        ex.MessageKey.Should().Be("errors.state");
    }

    [Fact]
    public async Task DbExecutor_BeginTransactionAsync_IsExclusive_AndCanRestartAfterDispose()
    {
        await using var dataSource = new FakeDbDataSource();
        var options = new AdoAsync.DbOptions
        {
            DatabaseType = AdoAsync.DatabaseType.SqlServer,
            ConnectionString = string.Empty,
            CommandTimeoutSeconds = 30,
            DataSource = dataSource,
            EnableValidation = true,
            EnableRetry = true
        };

        await using var executor = AdoAsync.Execution.DbExecutor.Create(options);

        var tx1 = await executor.BeginTransactionAsync();
        try
        {
            var ex = await Assert.ThrowsAsync<AdoAsync.DbCallerException>(() => executor.BeginTransactionAsync().AsTask());
            ex.MessageKey.Should().Be("errors.state");
        }
        finally
        {
            await tx1.DisposeAsync();
        }

        var tx2 = await executor.BeginTransactionAsync();
        await tx2.DisposeAsync();
    }

    private sealed class FakeDbDataSource : DbDataSource
    {
        public override string ConnectionString => string.Empty;

        protected override DbConnection CreateDbConnection() => new FakeDbConnection();
    }

    private sealed class FakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        private string _connectionString = string.Empty;

        public FakeDbTransaction? Transaction { get; private set; }

        [AllowNull]
        public override string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value ?? string.Empty;
        }

        public override string Database => "Fake";
        public override string DataSource => "Fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

        public override void Open() => _state = ConnectionState.Open;

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        public override void Close() => _state = ConnectionState.Closed;

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            Transaction = new FakeDbTransaction(this);
            return Transaction;
        }

        protected override DbCommand CreateDbCommand() => new FakeDbCommand(this);
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public FakeDbTransaction(DbConnection connection)
        {
            _connection = connection;
        }

        public bool CommitCalled { get; private set; }
        public bool RollbackCalled { get; private set; }

        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        protected override DbConnection DbConnection => _connection;

        public override void Commit() => CommitCalled = true;
        public override void Rollback() => RollbackCalled = true;

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCalled = true;
            return Task.CompletedTask;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RollbackCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDbCommand : DbCommand
    {
        private readonly DbConnection _connection;
        private string _commandText = string.Empty;

        public FakeDbCommand(DbConnection connection)
        {
            _connection = connection;
        }

        [AllowNull]
        public override string CommandText
        {
            get => _commandText;
            set => _commandText = value ?? string.Empty;
        }

        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }

        [AllowNull]
        protected override DbConnection DbConnection
        {
            get => _connection;
            set => _ = value;
        }

        protected override DbParameterCollection DbParameterCollection => throw new NotSupportedException();
        protected override DbTransaction? DbTransaction { get; set; }
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel() => throw new NotSupportedException();
        protected override System.Data.Common.DbParameter CreateDbParameter() => throw new NotSupportedException();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
        public override int ExecuteNonQuery() => throw new NotSupportedException();
        public override object? ExecuteScalar() => throw new NotSupportedException();
        public override void Prepare() => throw new NotSupportedException();
    }
}
