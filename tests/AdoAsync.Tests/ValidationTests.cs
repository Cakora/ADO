using System;
using System.Data;
using System.Data.Common;
using AdoAsync.Validation;
using FluentAssertions;
using Xunit;

namespace AdoAsync.Tests;

public class ValidationTests
{
    #region Tests
    [Fact]
    public void DbParameterValidator_RequiresSizeForOutput()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.String,
            Value = null,
            Direction = ParameterDirection.Output,
            Size = null
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("Output parameters must specify Size"));
    }

    [Fact]
    public void DbParameterValidator_RejectsInvalidDirection()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.String,
            Value = "value",
            Direction = (ParameterDirection)999,
            Size = 10
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("Direction is invalid"));
    }

    [Fact]
    public void DbParameterValidator_RejectsInvalidDbDataType()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = (DbDataType)999,
            Value = "value",
            Direction = ParameterDirection.Input,
            Size = 10
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("DbDataType is invalid"));
    }

    [Fact]
    public void DbParameterValidator_RequiresSizeForVariableLengthInput()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.String,
            Value = "value",
            Direction = ParameterDirection.Input
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("String parameters should specify Size"));
    }

    [Fact]
    public void DbParameterValidator_BlocksUnsignedTypes()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.UInt32,
            Value = 1,
            Direction = ParameterDirection.Input
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("Unsigned types are not supported"));
    }

    [Fact]
    public void DbParameterValidator_RequiresPrecisionScaleForDecimal()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.Decimal,
            Value = 1.23m,
            Direction = ParameterDirection.Input
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("Precision and Scale"));
    }

    [Fact]
    public void DbParameterValidator_RequiresPrecisionScaleForCurrency()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.Currency,
            Value = 12.34m,
            Direction = ParameterDirection.Input
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("Precision and Scale"));
    }

    [Fact]
    public void DbParameterValidator_RejectsDateTimeForTimestamp()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.Timestamp,
            Value = DateTime.UtcNow,
            Direction = ParameterDirection.Input
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("Timestamp parameters must not use date/time values"));
    }

    [Fact]
    public void DbParameterValidator_RejectsDateTimeOffsetForTimestamp()
    {
        var validator = new DbParameterValidator();
        var parameter = new DbParameter
        {
            Name = "p",
            DataType = DbDataType.Timestamp,
            Value = DateTimeOffset.UtcNow,
            Direction = ParameterDirection.Input
        };

        var result = validator.Validate(parameter);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorMessage.Contains("Timestamp parameters must not use date/time values"));
    }

    [Fact]
    public void DbOptionsValidator_FailsOnZeroTimeout()
    {
        var validator = new DbOptionsValidator();
        var options = new DbOptions
        {
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = "Server=(local);Database=demo;Trusted_Connection=True;",
            CommandTimeoutSeconds = 0
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(DbOptions.CommandTimeoutSeconds));
    }

    [Fact]
    public void DbOptionsValidator_AllowsDataSourceWithoutConnectionString()
    {
        var validator = new DbOptionsValidator();
        var options = new DbOptions
        {
            DatabaseType = DatabaseType.SqlServer,
            ConnectionString = string.Empty,
            CommandTimeoutSeconds = 30,
            DataSource = new FakeDataSource()
        };

        var result = validator.Validate(options);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void CommandDefinitionValidator_FailsOnEmptyCommandText()
    {
        var validator = new CommandDefinitionValidator();
        var definition = new CommandDefinition
        {
            CommandText = string.Empty
        };

        var result = validator.Validate(definition);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.PropertyName == nameof(CommandDefinition.CommandText));
    }

    [Fact]
    public void CommandDefinitionValidator_RequiresStoredProcedureAllowList()
    {
        var validator = new CommandDefinitionValidator();
        var definition = new CommandDefinition
        {
            CommandText = "usp_DoThing",
            CommandType = CommandType.StoredProcedure,
            Parameters = Array.Empty<DbParameter>()
        };

        var result = validator.Validate(definition);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("allow-list"));
    }

    [Fact]
    public void CommandDefinitionValidator_RejectsStoredProcedureNotInAllowList()
    {
        var validator = new CommandDefinitionValidator();
        var definition = new CommandDefinition
        {
            CommandText = "usp_DoThing",
            CommandType = CommandType.StoredProcedure,
            Parameters = Array.Empty<DbParameter>(),
            AllowedStoredProcedures = new HashSet<string> { "usp_SafeProc" }
        };

        var result = validator.Validate(definition);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CommandDefinitionValidator_ValidatesIdentifiersAgainstAllowList()
    {
        var validator = new CommandDefinitionValidator();
        var definition = new CommandDefinition
        {
            CommandText = "select * from tbl where id = @id",
            IdentifiersToValidate = new[] { "tbl" },
            AllowedIdentifiers = new HashSet<string> { "tbl" }
        };

        var result = validator.Validate(definition);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void BulkImportValidator_RequiresAllowListedDestinationTable()
    {
        var validator = new BulkImportRequestValidator();
        var request = new BulkImportRequest
        {
            DestinationTable = "dbo.Items",
            SourceReader = new FakeDataReader(),
            ColumnMappings = new[] { new BulkImportColumnMapping { SourceColumn = "Id", DestinationColumn = "Id" } }
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("allow-list"));
    }

    [Fact]
    public void BulkImportValidator_RejectsDuplicateDestinationColumns()
    {
        var validator = new BulkImportRequestValidator();
        var request = new BulkImportRequest
        {
            DestinationTable = "dbo.Items",
            AllowedDestinationTables = new HashSet<string> { "dbo.Items" },
            AllowedDestinationColumns = new HashSet<string> { "Id" },
            SourceReader = new FakeDataReader(),
            ColumnMappings = new[]
            {
                new BulkImportColumnMapping { SourceColumn = "Id", DestinationColumn = "Id" },
                new BulkImportColumnMapping { SourceColumn = "Id2", DestinationColumn = "Id" }
            }
        };

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }
    #endregion

    #region Test Doubles
    private sealed class FakeDataSource : DbDataSource
    {
        public override string ConnectionString => "fake";

        protected override DbConnection CreateDbConnection() => throw new NotImplementedException();
    }

    private sealed class FakeDataReader : DbDataReader
    {
        public override int FieldCount => 0;
        public override bool HasRows => false;
        public override bool IsClosed => true;
        public override int RecordsAffected => 0;
        public override int Depth => 0;
        public override object this[int ordinal] => throw new NotSupportedException();
        public override object this[string name] => throw new NotSupportedException();

        public override bool Read() => false;
        public override bool NextResult() => false;
        public override int GetOrdinal(string name) => throw new NotSupportedException();
        public override string GetName(int ordinal) => throw new NotSupportedException();
        public override object GetValue(int ordinal) => throw new NotSupportedException();
        public override int GetValues(object[] values) => throw new NotSupportedException();
        public override bool IsDBNull(int ordinal) => true;

        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();
        public override byte GetByte(int ordinal) => throw new NotSupportedException();
        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override char GetChar(int ordinal) => throw new NotSupportedException();
        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();
        public override string GetDataTypeName(int ordinal) => throw new NotSupportedException();
        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();
        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();
        public override double GetDouble(int ordinal) => throw new NotSupportedException();
        public override System.Collections.IEnumerator GetEnumerator() => throw new NotSupportedException();
        public override Type GetFieldType(int ordinal) => throw new NotSupportedException();
        public override float GetFloat(int ordinal) => throw new NotSupportedException();
        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();
        public override short GetInt16(int ordinal) => throw new NotSupportedException();
        public override int GetInt32(int ordinal) => throw new NotSupportedException();
        public override long GetInt64(int ordinal) => throw new NotSupportedException();
        public override string GetString(int ordinal) => throw new NotSupportedException();
    }
    #endregion
}
