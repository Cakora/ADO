using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Helpers;

namespace AdoAsync;

/// <summary>
/// Streaming reader plus deferred output parameters (available after reader is closed).
/// </summary>
public sealed class StreamingReaderResult : IAsyncDisposable, IDisposable
{
    private readonly DbCommand _command;
    private readonly IReadOnlyList<DbParameter>? _declaredParameters;
    private bool _disposed;
    private IReadOnlyDictionary<string, object?>? _outputs;

    /// <summary>Create a streaming reader result wrapper.</summary>
    /// <param name="command">Executed command holding provider parameters.</param>
    /// <param name="reader">Active data reader.</param>
    /// <param name="declaredParameters">Caller-declared parameters used for output extraction.</param>
    public StreamingReaderResult(DbCommand command, DbDataReader reader, IReadOnlyList<DbParameter>? declaredParameters)
    {
        _command = command ?? throw new ArgumentNullException(nameof(command));
        Reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _declaredParameters = declaredParameters;
    }

    /// <summary>Active data reader for streaming rows.</summary>
    public DbDataReader Reader { get; }

    /// <summary>
    /// Closes the reader (if not already closed) and returns output parameters.
    /// Safe to call multiple times; subsequent calls return cached outputs.
    /// </summary>
    public async ValueTask<IReadOnlyDictionary<string, object?>?> GetOutputParametersAsync(CancellationToken cancellationToken = default)
    {
        if (_outputs is not null)
        {
            return _outputs;
        }

        if (!_disposed)
        {
            await Reader.DisposeAsync().ConfigureAwait(false);
        }

        _outputs = ParameterHelper.ExtractOutputParameters(_command, _declaredParameters);
        return _outputs;
    }

    /// <summary>Dispose synchronously.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        Reader.Dispose();
        _command.Dispose();
    }

    /// <summary>Dispose asynchronously.</summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        await Reader.DisposeAsync().ConfigureAwait(false);
        await _command.DisposeAsync().ConfigureAwait(false);
    }
}
