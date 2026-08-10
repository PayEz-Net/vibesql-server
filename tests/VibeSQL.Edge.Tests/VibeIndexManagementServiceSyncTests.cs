using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Services;

namespace VibeSQL.Edge.Tests;

/// <summary>
/// Card 209106 / 186214 lane 3: virtual-index sync produces virtual_indexes rows
/// on schema write. virtual_indexes sat at 0 rows ever because
/// IVibeIndexManagementService was registered-but-never-invoked;
/// SchemasController.CreateOrUpdateSchema now invokes it post-commit.
///
/// These pins run at the service seam (the controller's raw Npgsql write path is
/// not unit-testable without a live PostgreSQL - stated on the card):
///  1. A schema with an x-vibe-index field produces a catalog row (CreateAsync)
///     and DDL execution - the "first real schema write produces rows" leg.
///  2. Re-syncing the same schema writes NO second row - idempotent retry, which
///     is what makes the controller's post-commit failure mode (re-PUT to retry)
///     safe.
///  3. A DDL failure surfaces as a failed result, not an escaped exception -
///     the property the controller's post-commit tolerance relies on.
/// </summary>
public class VibeIndexManagementServiceSyncTests
{
    private const string SchemaWithIndexedEmail = """
        {
          "tables": {
            "users": {
              "properties": {
                "email": { "type": "string", "x-vibe-index": true },
                "name":  { "type": "string" }
              }
            }
          }
        }
        """;

    private static Mock<IVirtualIndexRepository> RepositoryForNewSync()
    {
        var repo = new Mock<IVirtualIndexRepository>();
        repo.Setup(r => r.GetPartitionNameAsync(It.IsAny<int>())).ReturnsAsync("documents_default");
        repo.Setup(r => r.GetActiveIndexesAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<VirtualIndex>());
        repo.Setup(r => r.GetActiveIndexCountAsync(It.IsAny<int>())).ReturnsAsync(0);
        repo.Setup(r => r.GetTierLimitAsync(It.IsAny<int>())).ReturnsAsync(10);
        repo.Setup(r => r.GetPartitionInfoAsync(It.IsAny<int>()))
            .ReturnsAsync(new PartitionInfo { PartitionName = "documents_default", IsShared = true });
        repo.Setup(r => r.CreateAsync(It.IsAny<VirtualIndex>()))
            .ReturnsAsync((VirtualIndex v) => { v.VirtualIndexId = 1; return v; });
        return repo;
    }

    private static VibeIndexManagementService CreateService(Mock<IVirtualIndexRepository> repo) =>
        new(repo.Object, NullLogger<VibeIndexManagementService>.Instance);

    [Fact]
    public async Task Sync_SchemaWithIndexedField_CreatesCatalogRowAndExecutesDDL()
    {
        var repo = RepositoryForNewSync();
        var service = CreateService(repo);
        using var schema = JsonDocument.Parse(SchemaWithIndexedEmail);

        var results = await service.SyncIndexesForSchemaAsync(42, "profiles", schema);

        results.Should().ContainSingle(r => r.Success);
        repo.Verify(r => r.ExecuteDDLAsync(It.IsAny<string>(), It.IsAny<int>()), Times.Once);
        repo.Verify(r => r.CreateAsync(It.Is<VirtualIndex>(v =>
            v.ClientId == 42 &&
            v.Collection == "profiles" &&
            v.TableName == "users" &&
            v.PartitionName == "documents_default")), Times.Once);
    }

    [Fact]
    public async Task Sync_ResyncOfSameSchema_WritesNoSecondRow()
    {
        var repo = RepositoryForNewSync();
        var service = CreateService(repo);
        VirtualIndex? created = null;
        repo.Setup(r => r.CreateAsync(It.IsAny<VirtualIndex>()))
            .Callback((VirtualIndex v) => created = v)
            .ReturnsAsync((VirtualIndex v) => { v.VirtualIndexId = 1; return v; });

        using (var first = JsonDocument.Parse(SchemaWithIndexedEmail))
            await service.SyncIndexesForSchemaAsync(42, "profiles", first);
        created.Should().NotBeNull("the first sync must create the catalog row");

        // Second sync sees the created index as already active.
        repo.Setup(r => r.GetActiveIndexesAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync(new List<VirtualIndex> { created! });
        using var second = JsonDocument.Parse(SchemaWithIndexedEmail);
        await service.SyncIndexesForSchemaAsync(42, "profiles", second);

        repo.Verify(r => r.CreateAsync(It.IsAny<VirtualIndex>()), Times.Once,
            "an idempotent re-sync must not write a duplicate row");
    }

    [Fact]
    public async Task Sync_DdlFailure_ReturnsFailedResult_DoesNotThrow()
    {
        var repo = RepositoryForNewSync();
        repo.Setup(r => r.ExecuteDDLAsync(It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("forced DDL failure"));
        var service = CreateService(repo);
        using var schema = JsonDocument.Parse(SchemaWithIndexedEmail);

        var results = await service.SyncIndexesForSchemaAsync(42, "profiles", schema);

        results.Should().ContainSingle(r => !r.Success);
        results[0].ErrorMessage.Should().Contain("forced DDL failure");
    }
}
