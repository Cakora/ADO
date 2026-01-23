using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using AdoAsync.Abstractions;
using AdoAsync.BulkCopy.LinqToDb.Common;
using AdoAsync.BulkCopy.LinqToDb.Typed;
using AdoAsync.Resilience;
using AdoAsync.Validation;
using FluentValidation;
using Polly;

namespace AdoAsync.Execution;

/// <summary>
/// Orchestrates validation → resilience → provider → ADO.NET. Not thread-safe. Streaming by default; materialization is explicit and allocation-heavy. Retries are Polly-based and opt-in; cancellation is honored on all async calls; transactions are explicit via the transaction manager.
/// </summary>
public sealed partial class DbExecutor : IDbExecutor
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyOutputParameters =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    private readonly DbOptions _options;
    private readonly IDbProvider _provider;
    private readonly IAsyncPolicy _retryPolicy;
    private readonly IValidator<CommandDefinition> _commandValidator;
    private readonly IValidator<DbParameter> _parameterValidator;
    private readonly IValidator<BulkImportRequest> _bulkImportValidator;
    private readonly ILinqToDbTypedBulkImporter _linqToDbBulkImporter;
    private DbTransaction? _activeTransaction;
    private DbConnection? _connection;
    private bool _disposed;

    private DbExecutor(
        DbOptions options,
        IDbProvider provider,
        IAsyncPolicy retryPolicy,
        IValidator<CommandDefinition> commandValidator,
        IValidator<DbParameter> parameterValidator,
        IValidator<BulkImportRequest> bulkImportValidator,
        ILinqToDbTypedBulkImporter linqToDbBulkImporter)
    {
        _options = options;
        _provider = provider;
        _retryPolicy = retryPolicy;
        _commandValidator = commandValidator;
        _parameterValidator = parameterValidator;
        _bulkImportValidator = bulkImportValidator;
        _linqToDbBulkImporter = linqToDbBulkImporter;
    }

    /// <summary>Creates a new executor for the specified options.</summary>
    public static DbExecutor Create(DbOptions options, bool isInUserTransaction = false)
    {
        Validate.Required(options, nameof(options));

        var provider = ResolveProvider(options.DatabaseType);
        var optionsValidator = new DbOptionsValidator();
        var commandValidator = new CommandDefinitionValidator();
        var parameterValidator = new DbParameterValidator();
        var bulkImportValidator = new BulkImportRequestValidator();
        var linqToDbConnectionFactory = new LinqToDbConnectionFactory(options.DatabaseType);
        var linqToDbBulkImporter = new LinqToDbTypedBulkImporter(linqToDbConnectionFactory);

        var retryPolicy = RetryPolicyFactory.Create(
            options,
            exception => MapProviderError(options.DatabaseType, exception).IsTransient,
            isInUserTransaction);

        var validationError = ValidationRunner.ValidateOptions(options, options.EnableValidation, optionsValidator);
        if (validationError is not null)
        {
            throw new DatabaseException(ErrorCategory.Configuration, $"Invalid DbOptions: {validationError.MessageKey}");
        }

        return new DbExecutor(options, provider, retryPolicy, commandValidator, parameterValidator, bulkImportValidator, linqToDbBulkImporter);
    }
}

