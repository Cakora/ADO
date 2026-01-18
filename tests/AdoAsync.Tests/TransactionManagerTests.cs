using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Transactions;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class TransactionManagerTests
{
    #region Tests
    [Fact]
    public async Task DisposeWithoutCommit_RollsBack()
    {
        var connection = new FakeDbConnection();
        var manager = new TransactionManager(connection);

        await using (await manager.BeginAsync(connection))
        {
            // no commit
        }

        connection.Transaction.Should().NotBeNull();
        connection.Transaction!.RolledBack.Should().BeTrue();
        connection.Transaction.Committed.Should().BeFalse();
    }

    [Fact]
    public async Task Commit_SkipsRollbackOnDispose()
    {
        var connection = new FakeDbConnection();
        var manager = new TransactionManager(connection);

        await using (var handle = await manager.BeginAsync(connection))
        {
            await handle.CommitAsync();
        }

        connection.Transaction.Should().NotBeNull();
        connection.Transaction!.Committed.Should().BeTrue();
        connection.Transaction.RolledBack.Should().BeFalse();
    }

    [Fact]
    public async Task BeginAsync_OpensConnectionIfClosed()
    {
        var connection = new FakeDbConnection();
        var manager = new TransactionManager(connection);

        await using var _ = await manager.BeginAsync(connection);

        connection.State.Should().Be(ConnectionState.Open);
    }
    #endregion

    #region Test Doubles
    private sealed class FakeDbConnection : DbConnection
    {
        // Minimal fake to validate transaction lifecycle behavior without a real DB.
        private ConnectionState _state = ConnectionState.Closed;
        private string _connectionString = string.Empty;

        public FakeDbTransaction? Transaction { get; private set; }

        [System.Diagnostics.CodeAnalysis.AllowNull]
        public override string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value ?? string.Empty;
        }

        public override string Database => "fake";
        public override string DataSource => "fake";
        public override string ServerVersion => "1.0";
        public override ConnectionState State => _state;

        public override Task OpenAsync(CancellationToken cancellationToken)
        {
            _state = ConnectionState.Open;
            return Task.CompletedTask;
        }

        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        public override void ChangeDatabase(string databaseName) { }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            Transaction = new FakeDbTransaction(this, isolationLevel);
            return Transaction;
        }

        protected override DbCommand CreateDbCommand() => throw new System.NotImplementedException();
    }

    private sealed class FakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;
        private readonly IsolationLevel _isolationLevel;

        public FakeDbTransaction(DbConnection connection, IsolationLevel isolationLevel)
        {
            _connection = connection;
            _isolationLevel = isolationLevel;
        }

        public bool Committed { get; private set; }
        public bool RolledBack { get; private set; }

        public override IsolationLevel IsolationLevel => _isolationLevel;
        protected override DbConnection DbConnection => _connection;

        public override Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Committed = true;
            return Task.CompletedTask;
        }

        public override Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }

        public override void Commit() => CommitAsync().GetAwaiter().GetResult();
        public override void Rollback() => RollbackAsync().GetAwaiter().GetResult();
    }
    #endregion
}
