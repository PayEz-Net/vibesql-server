using Xunit;
using FluentAssertions;
using VibeSQL.Edge.Data.Entities;

namespace VibeSQL.Edge.Tests;

public class EntityConstructionTests
{
    [Fact]
    public void OidcProvider_DefaultValues_AreCorrect()
    {
        var provider = new OidcProvider();

        provider.IsActive.Should().BeTrue();
        provider.IsBootstrap.Should().BeFalse();
        provider.AutoProvision.Should().BeFalse();
        provider.SubjectClaimPath.Should().Be("sub");
        provider.RoleClaimPath.Should().Be("roles");
        provider.EmailClaimPath.Should().Be("email");
        provider.ClockSkewSeconds.Should().Be(60);
        provider.DisableGraceMinutes.Should().Be(0);
        provider.DisabledAt.Should().BeNull();
        provider.ProvisionDefaultRole.Should().BeNull();
    }

    [Fact]
    public void OidcProviderRoleMapping_GetPermissionLevel_ParsesCorrectly()
    {
        var mapping = new OidcProviderRoleMapping { VibePermission = "write" };
        mapping.GetPermissionLevel().Should().Be(VibePermissionLevel.Write);
    }

    [Fact]
    public void OidcProviderClientMapping_GetMaxPermissionLevel_ParsesCorrectly()
    {
        var mapping = new OidcProviderClientMapping { MaxPermission = "schema" };
        mapping.GetMaxPermissionLevel().Should().Be(VibePermissionLevel.Schema);
    }

    [Fact]
    public void OidcProviderClientMapping_DefaultMaxPermission_IsWrite()
    {
        var mapping = new OidcProviderClientMapping();
        mapping.MaxPermission.Should().Be("write");
        mapping.GetMaxPermissionLevel().Should().Be(VibePermissionLevel.Write);
    }

    [Fact]
    public void FederatedIdentity_DefaultValues_AreCorrect()
    {
        var identity = new FederatedIdentity();

        identity.IsActive.Should().BeTrue();
        identity.Email.Should().BeNull();
        identity.DisplayName.Should().BeNull();
        identity.Metadata.Should().BeNull();
    }

    [Fact]
    public void OidcProvider_Collections_AreInitialized()
    {
        var provider = new OidcProvider();

        provider.RoleMappings.Should().NotBeNull().And.BeEmpty();
        provider.ClientMappings.Should().NotBeNull().And.BeEmpty();
        provider.FederatedIdentities.Should().NotBeNull().And.BeEmpty();
    }
}
