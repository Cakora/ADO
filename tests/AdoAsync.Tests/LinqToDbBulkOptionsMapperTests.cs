using System;
using AdoAsync.BulkCopy.LinqToDb.Common;
using FluentAssertions;
using LinqToDB.Data;
using Xunit;

namespace AdoAsync.Tests;

public class LinqToDbBulkOptionsMapperTests
{
    [Fact]
    public void Map_AppliesOverrides()
    {
        Action<BulkCopyRowsCopied> callback = _ => { };

        var options = new LinqToDbBulkOptions
        {
            Enable = true,
            BulkCopyType = BulkCopyType.ProviderSpecific,
            BulkCopyTimeoutSeconds = 15,
            MaxBatchSize = 1000,
            NotifyAfter = 5,
            KeepIdentity = true,
            CheckConstraints = true,
            KeepNulls = true,
            FireTriggers = true,
            TableLock = true,
            UseInternalTransaction = true,
            UseParameters = true,
            MaxParametersForBatch = 250,
            MaxDegreeOfParallelism = 2,
            OnRowsCopied = callback
        };

        var mapped = BulkCopyOptionsMapper.Map(options, "Destination", 30);

        mapped.BulkCopyType.Should().Be(BulkCopyType.ProviderSpecific);
        mapped.BulkCopyTimeout.Should().Be(15);
        mapped.MaxBatchSize.Should().Be(1000);
        mapped.NotifyAfter.Should().Be(5);
        mapped.KeepIdentity.Should().BeTrue();
        mapped.CheckConstraints.Should().BeTrue();
        mapped.KeepNulls.Should().BeTrue();
        mapped.FireTriggers.Should().BeTrue();
        mapped.TableLock.Should().BeTrue();
        mapped.UseInternalTransaction.Should().BeTrue();
        mapped.UseParameters.Should().BeTrue();
        mapped.MaxParametersForBatch.Should().Be(250);
        mapped.MaxDegreeOfParallelism.Should().Be(2);
        mapped.RowsCopiedCallback.Should().Be(callback);
        mapped.TableName.Should().Be("Destination");
    }

    [Fact]
    public void Map_FallsBackToCommandTimeout()
    {
        var options = new LinqToDbBulkOptions();

        var mapped = BulkCopyOptionsMapper.Map(options, "Destination", 45);

        mapped.BulkCopyTimeout.Should().Be(45);
        mapped.MaxBatchSize.Should().BeNull();
        mapped.NotifyAfter.Should().Be(0);
        mapped.MaxDegreeOfParallelism.Should().BeNull();
        mapped.TableName.Should().Be("Destination");
    }
}
