using System.Security.Claims;
using FluentAssertions;
using VibeSQL.Edge.Data.Entities;
using VibeSQL.Edge.Identity;

namespace VibeSQL.Edge.Tests;

public class ClaimExtractorTests
{
    private readonly ClaimExtractor _extractor = new();

    private static OidcProvider CreateProvider(
        string subjectPath = "sub",
        string rolePath = "roles",
        string emailPath = "email") => new()
    {
        ProviderKey = "test-provider",
        DisplayName = "Test",
        Issuer = "https://test.example.com",
        DiscoveryUrl = "https://test.example.com/.well-known/openid-configuration",
        Audience = "test-api",
        SubjectClaimPath = subjectPath,
        RoleClaimPath = rolePath,
        EmailClaimPath = emailPath
    };

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "Bearer");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Extract_StandardClaims_ReturnsAll()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"),
            new Claim("roles", "admin"),
            new Claim("roles", "reader"),
            new Claim("email", "user@example.com"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Subject.Should().Be("user-123");
        result.Roles.Should().BeEquivalentTo("admin", "reader");
        result.Email.Should().Be("user@example.com");
    }

    [Fact]
    public void Extract_MissingSubject_Throws()
    {
        var principal = CreatePrincipal(
            new Claim("email", "user@example.com"));

        var act = () => _extractor.Extract(principal, CreateProvider());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Subject claim*not found*");
    }

    [Fact]
    public void Extract_MissingRoles_ReturnsEmptyList()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Roles.Should().BeEmpty();
    }

    [Fact]
    public void Extract_MissingEmail_ReturnsNull()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Email.Should().BeNull();
    }

    [Fact]
    public void Extract_AzureAD_OidClaim_FallsBackForSubject()
    {
        var principal = CreatePrincipal(
            new Claim("oid", "azure-obj-id"),
            new Claim("preferred_username", "user@contoso.com"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Subject.Should().Be("azure-obj-id");
        result.Email.Should().Be("user@contoso.com");
    }

    [Fact]
    public void Extract_AzureAD_GroupsClaim_FallsBackForRoles()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"),
            new Claim("groups", "group-a"),
            new Claim("groups", "group-b"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Roles.Should().BeEquivalentTo("group-a", "group-b");
    }

    [Fact]
    public void Extract_CustomClaimPaths_UsesProviderConfig()
    {
        var provider = CreateProvider(
            subjectPath: "user_id",
            rolePath: "permissions",
            emailPath: "mail");

        var principal = CreatePrincipal(
            new Claim("user_id", "custom-id"),
            new Claim("permissions", "perm-a"),
            new Claim("mail", "custom@example.com"));

        var result = _extractor.Extract(principal, provider);

        result.Subject.Should().Be("custom-id");
        result.Roles.Should().BeEquivalentTo("perm-a");
        result.Email.Should().Be("custom@example.com");
    }

    [Fact]
    public void Extract_CaseInsensitiveClaimMatch()
    {
        var principal = CreatePrincipal(
            new Claim("SUB", "user-123"),
            new Claim("ROLES", "admin"),
            new Claim("EMAIL", "user@example.com"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Subject.Should().Be("user-123");
        result.Roles.Should().BeEquivalentTo("admin");
        result.Email.Should().Be("user@example.com");
    }

    [Fact]
    public void Extract_NameIdentifierClaim_FallsBackForSub()
    {
        var principal = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, "nameid-user"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Subject.Should().Be("nameid-user");
    }

    [Fact]
    public void Extract_RoleClaim_FallsBackForRoles()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"),
            new Claim(ClaimTypes.Role, "role-a"),
            new Claim(ClaimTypes.Role, "role-b"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Roles.Should().BeEquivalentTo("role-a", "role-b");
    }

    [Fact]
    public void Extract_EmailClaim_FallsBackForEmail()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"),
            new Claim(ClaimTypes.Email, "email@example.com"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Email.Should().Be("email@example.com");
    }

    [Fact]
    public void Extract_CognitoGroups_FallsBackForRoles()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"),
            new Claim("cognito:groups", "cog-group-1"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Roles.Should().BeEquivalentTo("cog-group-1");
    }

    [Fact]
    public void Extract_UpnClaim_FallsBackForEmail()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"),
            new Claim("upn", "user@contoso.onmicrosoft.com"));

        var result = _extractor.Extract(principal, CreateProvider());

        result.Email.Should().Be("user@contoso.onmicrosoft.com");
    }

    [Fact]
    public void FindClaim_DirectMatch_TakesPriorityOverFallback()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "direct-match"),
            new Claim(ClaimTypes.NameIdentifier, "fallback-match"));

        var result = ClaimExtractor.FindClaim(principal, "sub");

        result.Should().Be("direct-match");
    }

    [Fact]
    public void FindAllClaims_DirectMatch_TakesPriorityOverFallback()
    {
        var principal = CreatePrincipal(
            new Claim("roles", "direct-role"),
            new Claim(ClaimTypes.Role, "fallback-role"));

        var result = ClaimExtractor.FindAllClaims(principal, "roles");

        result.Should().BeEquivalentTo("direct-role");
    }

    [Fact]
    public void FindAllClaims_UnknownClaimPath_ReturnsEmpty()
    {
        var principal = CreatePrincipal(
            new Claim("sub", "user-123"));

        var result = ClaimExtractor.FindAllClaims(principal, "nonexistent");

        result.Should().BeEmpty();
    }
}
