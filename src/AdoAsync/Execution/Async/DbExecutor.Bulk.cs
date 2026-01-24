using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AdoAsync.Abstractions;
using AdoAsync.BulkCopy.LinqToDb.Common;
using AdoAsync.Validation;

namespace AdoAsync.Execution;

public sealed partial class DbExecutor
{
    #region Public API - Bulk
    /// <summary>Bulk import data using provider-specific fast paths.</summary>
    public async ValueTask<BulkImportResult> BulkImportAsync(BulkImportRequest request, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var validationError = ValidationRunner.ValidateBulkImport(request, _options.EnableValidation, _bulkImportValidator);
        if (validationError is not null)
        {
            return new BulkImportResult { Success = false, Error = validationError };
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var connection = _connection ?? throw new DatabaseException(ErrorCategory.State, "Connection was not initialized.");
                var started = Stopwatch.StartNew();
                var normalizedRequest = request;
                if (_options.DatabaseType == DatabaseType.Oracle)
                {
                    normalizedRequest = request with
                    {
                        DestinationTable = IdentifierNormalization.NormalizeTableName(_options.DatabaseType, request.DestinationTable)
                    };
                }

                var rows = await _provider.BulkImportAsync(connection, _activeTransaction, normalizedRequest, ct).ConfigureAwait(false);
                started.Stop();
                return new BulkImportResult
                {
                    Success = true,
                    RowsInserted = rows,
                    Duration = started.Elapsed
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new BulkImportResult { Success = false, Error = error };
        }
    }

    /// <summary>Bulk import typed rows using linq2db. Requires linq2db bulk copy to be enabled.</summary>
    public async ValueTask<BulkImportResult> BulkImportAsync<T>(
        IEnumerable<T> items,
        string? tableName = null,
        LinqToDbBulkOptions? bulkOptions = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfDisposed();
        Validate.Required(items, nameof(items));

        var resolvedOptions = ResolveLinqToDbOptions(bulkOptions);
        if (!resolvedOptions.Enable)
        {
            var error = DbErrorMapper.Map(new DatabaseException(ErrorCategory.Configuration, "LinqToDB bulk copy is disabled. Enable DbOptions.LinqToDb.Enable to use typed bulk imports."));
            return new BulkImportResult { Success = false, Error = error };
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var connection = _connection ?? throw new DatabaseException(ErrorCategory.State, "Connection was not initialized.");
                var started = Stopwatch.StartNew();
                var rows = await _linqToDbBulkImporter.BulkImportAsync(connection, _activeTransaction, items, resolvedOptions, _options.CommandTimeoutSeconds, tableName, ct).ConfigureAwait(false);
                started.Stop();
                return new BulkImportResult
                {
                    Success = true,
                    RowsInserted = rows,
                    Duration = started.Elapsed
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new BulkImportResult { Success = false, Error = error };
        }
    }

    /// <summary>Bulk import async-typed rows using linq2db. Requires linq2db bulk copy to be enabled.</summary>
    public async ValueTask<BulkImportResult> BulkImportAsync<T>(
        IAsyncEnumerable<T> items,
        string? tableName = null,
        LinqToDbBulkOptions? bulkOptions = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ThrowIfDisposed();
        Validate.Required(items, nameof(items));

        var resolvedOptions = ResolveLinqToDbOptions(bulkOptions);
        if (!resolvedOptions.Enable)
        {
            var error = DbErrorMapper.Map(new DatabaseException(ErrorCategory.Configuration, "LinqToDB bulk copy is disabled. Enable DbOptions.LinqToDb.Enable to use typed bulk imports."));
            return new BulkImportResult { Success = false, Error = error };
        }

        try
        {
            return await ExecuteWithRetryIfAllowedAsync(async ct =>
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var connection = _connection ?? throw new DatabaseException(ErrorCategory.State, "Connection was not initialized.");
                var started = Stopwatch.StartNew();
                var rows = await _linqToDbBulkImporter.BulkImportAsync(connection, _activeTransaction, items, resolvedOptions, _options.CommandTimeoutSeconds, tableName, ct).ConfigureAwait(false);
                started.Stop();
                return new BulkImportResult
                {
                    Success = true,
                    RowsInserted = rows,
                    Duration = started.Elapsed
                };
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var error = MapError(ex);
            return new BulkImportResult { Success = false, Error = error };
        }
    }
    #endregion
}
