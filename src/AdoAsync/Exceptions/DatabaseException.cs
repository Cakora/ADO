using System;
using System.Runtime.Serialization;

namespace AdoAsync;

/// <summary>
/// Library-specific exception to avoid leaking framework exception types.
/// </summary>
[Serializable]
public sealed class DatabaseException : Exception
{
    /// <summary>High-level reason for the failure.</summary>
    public ErrorCategory Kind { get; }

    /// <summary>Creates a new database exception with a validation category.</summary>
    public DatabaseException()
        : this(ErrorCategory.Validation, "errors.validation")
    {
    }

    /// <summary>Creates a new database exception with a category and message.</summary>
    public DatabaseException(string message)
        : this(ErrorCategory.Validation, message)
    {
    }

    /// <summary>Creates a new database exception with a category, message, and inner exception.</summary>
    public DatabaseException(string message, Exception? innerException)
        : this(ErrorCategory.Validation, message, innerException)
    {
    }

    /// <summary>Creates a new database exception with a category and message.</summary>
    public DatabaseException(ErrorCategory kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        // Preserve the category so callers can branch without string matching.
        Kind = kind;
    }

    /// <summary>Serialization constructor.</summary>
    #pragma warning disable SYSLIB0051
    #pragma warning disable S1133
    [Obsolete("Formatter-based serialization is obsolete.")]
    private DatabaseException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        if (info is null)
        {
            throw new ArgumentNullException(nameof(info));
        }

        Kind = (ErrorCategory)(info.GetValue(nameof(Kind), typeof(ErrorCategory)) ?? ErrorCategory.Validation);
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
        info.AddValue(nameof(Kind), Kind, typeof(ErrorCategory));
    }
    #pragma warning restore S1133
    #pragma warning restore SYSLIB0051
}

/// <summary>Categories for library exceptions.</summary>
public enum ErrorCategory
{
    /// <summary>Validation or input-related failure.</summary>
    Validation,
    /// <summary>Configuration or setup issue.</summary>
    Configuration,
    /// <summary>Unsupported provider or feature.</summary>
    Unsupported,
    /// <summary>Invalid state or lifecycle usage.</summary>
    State,
    /// <summary>Use-after-dispose or invalid lifetime.</summary>
    Disposed
}
