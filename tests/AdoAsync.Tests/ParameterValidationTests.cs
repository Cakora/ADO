using System.Data;
using AdoAsync.Validation;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public sealed class ParameterValidationTests
{
    private readonly DbParameterValidator _validator = new();

    [Fact]
    public void DbParameterValidator_Structured_MissingStructuredTypeName_IsInvalid()
    {
        var result = _validator.Validate(new DbParameter
        {
            Name = "@Rows",
            DataType = DbDataType.Structured,
            Direction = ParameterDirection.Input,
            Value = new object()
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DbParameterValidator_Structured_OutputDirection_IsInvalid()
    {
        var result = _validator.Validate(new DbParameter
        {
            Name = "@Rows",
            DataType = DbDataType.Structured,
            Direction = ParameterDirection.Output,
            StructuredTypeName = "dbo.MyRowType",
            Value = new object()
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DbParameterValidator_Structured_Valid_IsValid()
    {
        var result = _validator.Validate(new DbParameter
        {
            Name = "@Rows",
            DataType = DbDataType.Structured,
            Direction = ParameterDirection.Input,
            StructuredTypeName = "dbo.MyRowType",
            Value = new object()
        });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void DbParameterValidator_ArrayBinding_NonArrayValue_IsInvalid()
    {
        var result = _validator.Validate(new DbParameter
        {
            Name = ":p_ids",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Input,
            IsArrayBinding = true,
            Value = 123
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DbParameterValidator_ArrayBinding_EmptyArray_IsInvalid()
    {
        var result = _validator.Validate(new DbParameter
        {
            Name = ":p_ids",
            DataType = DbDataType.Int32,
            Direction = ParameterDirection.Input,
            IsArrayBinding = true,
            Value = Array.Empty<int>()
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DbParameterValidator_ArrayBinding_StringMissingSize_IsInvalid()
    {
        var result = _validator.Validate(new DbParameter
        {
            Name = ":p_state",
            DataType = DbDataType.String,
            Direction = ParameterDirection.Input,
            IsArrayBinding = true,
            Value = new[] { "READY", "DONE" }
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DbParameterValidator_ArrayBinding_StringWithSize_IsValid()
    {
        var result = _validator.Validate(new DbParameter
        {
            Name = ":p_state",
            DataType = DbDataType.String,
            Direction = ParameterDirection.Input,
            IsArrayBinding = true,
            Size = 50,
            Value = new[] { "READY", "DONE" }
        });

        result.IsValid.Should().BeTrue();
    }
}

