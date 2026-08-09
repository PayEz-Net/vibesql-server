using Microsoft.EntityFrameworkCore;
using VibeSQL.Core.Data;
using VibeSQL.Core.Entities.IdentityReference;
using VibeSQL.Core.Interfaces;

namespace VibeSQL.Core.Services;

/// <summary>
/// EF-backed implementation of <see cref="IClientReferenceValidator"/>. Queries the
/// read-only <see cref="IdentityClientReference"/> shadow mapping of identity.clients.
/// See docs/cross-schema-reference-constraints.md, Mode B.
/// </summary>
public class ClientReferenceValidator : IClientReferenceValidator
{
    private readonly VibeDbContext _context;

    public ClientReferenceValidator(VibeDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ClientExistsAsync(int clientId)
    {
        return await _context.Set<IdentityClientReference>()
            .AnyAsync(c => c.ClientId == clientId);
    }
}
