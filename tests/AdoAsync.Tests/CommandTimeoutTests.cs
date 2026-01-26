using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public sealed class CommandTimeoutTests
{
    [Fact]
    public async Task ExecuteAsync_UsesOptionsCommandTimeout_WhenCommandOverrideNotSet()
    {
        await using var dataSource = new FakeDbDataSource();
        var options = new AdoAsync.DbOptions
        {
            DatabaseType = AdoAsync.DatabaseType.SqlServer,
            ConnectionString = string.Empty,
            CommandTimeoutSeconds = 123,
            DataSource = dataSource,
            EnableValidation = true,
            EnableRetry = false
        };

        await using var executor = AdoAsync.Execution.DbExecutor.Create(options);

        await executor.ExecuteAsync(new AdoAsync.CommandDefinition
        {
            CommandText = "select 1",
            CommandType = CommandType.Text,
            CommandTimeoutSeconds = null
        });

        dataSource.Connection.LastCommand.Should().NotBeNull();
        dataSource.Connection.LastCommand!.CommandTimeout.Should().Be(123);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCommandTimeoutOverride_WhenSet()
    {
        await using var dataSource = new FakeDbDataSource();
        var options = new AdoAsync.DbOptions
        {
            DatabaseType = AdoAsync.DatabaseType.SqlServer,
            ConnectionString = string.Empty,
            CommandTimeoutSeconds = 123,
            DataSource = dataSource,
            EnableValidation = true,
            EnableRetry = false
        };

        await using var executor = AdoAsync.Execution.DbExecutor.Create(options);

        await executor.ExecuteAsync(new AdoAsync.CommandDefinition
        {
            CommandText = "select 1",
            CommandType = CommandType.Text,
            CommandTimeoutSeconds = 7
        });

        dataSource.Connection.LastCommand.Should().NotBeNull();
        dataSource.Connection.LastCommand!.CommandTimeout.Should().Be(7);
    }

    private sealed class FakeDbDataSource : DbDataSource
    {
        public FakeDbConnection Connection { get; } = new();

        public override string ConnectionString => string.Empty;

        protected override DbConnection CreateDbConnection() => Connection;
    }

    private sealed class FakeDbConnection : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        private string _connectionString = string.Empty;

        public FakeDbCommand? LastCommand { get; private set; }

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

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();

        protected override DbCommand CreateDbCommand()
        {
            LastCommand = new FakeDbCommand(this);
            return LastCommand;
        }
    }

    private sealed class FakeDbCommand : DbCommand
    {
        private readonly DbConnection _connection;

        public FakeDbCommand(DbConnection connection)
        {
            _connection = connection;
        }

        [AllowNull]
        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; } = CommandType.Text;

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; } = UpdateRowSource.None;

        [AllowNull]
        protected override DbConnection DbConnection
        {
            get => _connection;
            set => throw new NotSupportedException();
        }

        protected override DbParameterCollection DbParameterCollection { get; } = new FakeDbParameterCollection();

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }

        public override int ExecuteNonQuery() => 1;

        public override object? ExecuteScalar() => 1;

        public override Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        public override Task<object?> ExecuteScalarAsync(CancellationToken cancellationToken) => Task.FromResult<object?>(1);

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();

        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public override void Prepare() { }

        protected override global::System.Data.Common.DbParameter CreateDbParameter() => new FakeProviderDbParameter();
    }

    private sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly System.Collections.Generic.List<global::System.Data.Common.DbParameter> _items = new();

        public override int Count => _items.Count;
        public override object SyncRoot { get; } = new();

        public override int Add(object value)
        {
            _items.Add((global::System.Data.Common.DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value!);
            }
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((global::System.Data.Common.DbParameter)value);

        public override bool Contains(string value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index) => _items.ToArray().CopyTo(array, index);

        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((global::System.Data.Common.DbParameter)value);

        public override int IndexOf(string parameterName)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].ParameterName, parameterName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        public override void Insert(int index, object value) => _items.Insert(index, (global::System.Data.Common.DbParameter)value);

        public override void Remove(object value) => _items.Remove((global::System.Data.Common.DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0)
            {
                _items.RemoveAt(index);
            }
        }

        protected override global::System.Data.Common.DbParameter GetParameter(int index) => _items[index];

        protected override global::System.Data.Common.DbParameter GetParameter(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                throw new IndexOutOfRangeException($"Parameter '{parameterName}' not found.");
            }
            return _items[index];
        }

        protected override void SetParameter(int index, global::System.Data.Common.DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, global::System.Data.Common.DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                _items.Add(value);
                return;
            }
            _items[index] = value;
        }
    }

    private sealed class FakeProviderDbParameter : global::System.Data.Common.DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;
        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = string.Empty;

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;
        public override object? Value { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override int Size { get; set; }

        public override void ResetDbType() { }
    }
}
