using System;
using System.Runtime.Serialization;

namespace AdoAsync;

/// <summary>
/// Unified exception that wraps provider errors into a stable, cross-provider shape.
/// </summary>
[Serializable]
public sealed class DbClientException : Exception
{
    /// <summary>Structured error payload for client decisions.</summary>
    public DbError Error { get; }

    /// <summary>Creates a new client exception with an unknown error.</summary>
    public DbClientException()
        : this(CreateDefaultError("errors.unknown"))
    {
    }

    /// <summary>Creates a new client exception from a message.</summary>
    public DbClientException(string message)
        : this(CreateDefaultError(message ?? "errors.unknown"))
    {
    }

    /// <summary>Creates a new client exception from a message and inner exception.</summary>
    public DbClientException(string message, Exception? innerException)
        : this(CreateDefaultError(message ?? "errors.unknown"), innerException)
    {
    }

    /// <summary>Creates a new client exception from a structured error.</summary>
    public DbClientException(DbError error, Exception? innerException = null)
        : base(error.MessageKey, innerException)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>Serialization constructor.</summary>
    #pragma warning disable SYSLIB0051
    #pragma warning disable S1133
    [Obsolete("Formatter-based serialization is obsolete.")]
    private DbClientException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        if (info is null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        Error = (DbError?)info.GetValue(nameof(Error), typeof(DbError)) ?? CreateDefaultError("errors.unknown");
    }

    /// <inheritdoc />
    [Obsolete("Formatter-based serialization is obsolete.")]
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        if (info is null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        base.GetObjectData(info, context);
        info.AddValue(nameof(Error), Error, typeof(DbError));
    }
    #pragma warning restore S1133
    #pragma warning restore SYSLIB0051

    private static DbError CreateDefaultError(string message)
    {
        var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "errors.unknown" : message;
        return new DbError
        {
            Type = DbErrorType.Unknown,
            Code = DbErrorCode.Unknown,
            MessageKey = normalizedMessage,
            MessageParameters = string.IsNullOrWhiteSpace(message) ? Array.Empty<string>() : new[] { normalizedMessage },
            IsTransient = false
        };
    }
}
