using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VibeSQL.Core.Data;
using VibeSQL.Core.Data.Repositories;
using VibeSQL.Core.Entities.IdentityReference;
using VibeSQL.Core.Exceptions;
using VibeSQL.Core.Services;

namespace VibeSQL.Core.Tests;

/// <summary>
/// Card 186212 / Sentinel M-201: the cross-schema reference-constraint (Mode B, "virtual
/// FK") that gates vibe.documents.client_id to a real identity.clients row. Before this,
/// M-201 was documentation only -- these tests exercise the actual write path
/// (VibeDocumentRepository.CreateAsync + ClientReferenceValidator) against real branching
/// logic, not a mock that always agrees with the code under test.
///
/// PRODUCTION CALL SITE: VibeDocumentRepository.CreateAsync is the single write path for
/// vibe.documents rows (also reachable via IVibeDocumentRepository from any consumer that
/// registers it). Not latent -- this is the exact path the card's 128-orphaned-row evidence
/// (client_id=0 in vibe.documents_default on 93) came through.
/// </summary>
public class VibeDocumentRepositoryClientReferenceTests
{
    private static VibeDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<VibeDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new VibeDbContext(options);
    }

    private static async Task SeedClientAsync(VibeDbContext context, int clientId)
    {
        context.Set<IdentityClientReference>().Add(new IdentityClientReference { ClientId = clientId });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ClientExistsAsync_KnownClient_ReturnsTrue()
    {
        await using var context = CreateContext(nameof(ClientExistsAsync_KnownClient_ReturnsTrue));
        await SeedClientAsync(context, 42);
        var validator = new ClientReferenceValidator(context);

        var exists = await validator.ClientExistsAsync(42);

        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ClientExistsAsync_UnknownClient_ReturnsFalse()
    {
        await using var context = CreateContext(nameof(ClientExistsAsync_UnknownClient_ReturnsFalse));
        var validator = new ClientReferenceValidator(context);

        var exists = await validator.ClientExistsAsync(999);

        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_KnownClient_Succeeds()
    {
        await using var context = CreateContext(nameof(CreateAsync_KnownClient_Succeeds));
        await SeedClientAsync(context, 42);
        var repo = new VibeDocumentRepository(context, NullLogger<VibeDocumentRepository>.Instance,
            new ClientReferenceValidator(context));

        var (document, _) = await repo.CreateAsync(42, userId: 7, "profiles", "users", "{}", createdBy: 7);

        document.ClientId.Should().Be(42);
        document.DocumentId.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CreateAsync_UnknownClient_ThrowsUnknownClientReferenceException_AndDoesNotPersist()
    {
        // This is the defect the card documents: before this fix, a write naming a
        // non-existent client_id succeeded silently and orphaned the row.
        await using var context = CreateContext(nameof(CreateAsync_UnknownClient_ThrowsUnknownClientReferenceException_AndDoesNotPersist));
        var repo = new VibeDocumentRepository(context, NullLogger<VibeDocumentRepository>.Instance,
            new ClientReferenceValidator(context));

        var act = () => repo.CreateAsync(999, userId: 7, "profiles", "users", "{}", createdBy: 7);

        var thrown = await act.Should().ThrowAsync<UnknownClientReferenceException>();
        thrown.Which.ClientId.Should().Be(999);
        (await context.Documents.CountAsync()).Should().Be(0, "the refused write must not reach the table");
    }

    [Fact]
    public async Task CreateAsync_LegacyGlobalSentinelZero_IsExemptAndSucceeds()
    {
        // TEMPORARY carve-out (see VibeDocumentRepository.LegacyGlobalSentinelClientId):
        // client_id=0 predates this validator and has live rows in production. Rejecting
        // it here, before the documented 0-to-NULL migration runs, would break every
        // caller still relying on the current sentinel -- this test locks that in as a
        // deliberate exception, not an oversight, so removing it later is a visible,
        // intentional test change rather than a silent behavior regression.
        await using var context = CreateContext(nameof(CreateAsync_LegacyGlobalSentinelZero_IsExemptAndSucceeds));
        // Deliberately NOT seeding client_id 0 -- it must succeed with zero real clients
        // in the reference table, proving the exemption bypasses the lookup entirely.
        var repo = new VibeDocumentRepository(context, NullLogger<VibeDocumentRepository>.Instance,
            new ClientReferenceValidator(context));

        var (document, _) = await repo.CreateAsync(0, userId: 7, "profiles", "users", "{}", createdBy: 7);

        document.ClientId.Should().Be(0);
    }
}
