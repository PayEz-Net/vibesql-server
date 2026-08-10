using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Models;
using VibeSQL.Core.Query;
using VibeSQL.Server.Controllers.V1;

namespace VibeSQL.Server.Tests;

/// <summary>
/// Card 209104 / 186214 lane 2: usage metering at the controller layer.
/// feature_usage_logs sat at 0 rows ever because IVibeUsageRepository was
/// registered-but-never-called; QueryController.ExecuteQuery is the first
/// instrumented call site, per-endpoint feature key "queries".
///
/// ACCEPTANCE PINS (from the card):
///  1. A successful query writes a metering row - IncrementUsageAsync(clientId,
///     null, "queries") is called exactly once after execution succeeds.
///  2. A forced metering failure still returns 200 with the query result -
///     metering never breaks the request (HARD RULE on the card).
///  3. Unresolved ClientId is unmetered - no row can be attributed, none written.
/// </summary>
public class QueryControllerUsageMeteringTests
{
    private static Mock<IQueryExecutor> SuccessfulExecutor()
    {
        var executor = new Mock<IQueryExecutor>();
        executor
            .Setup(e => e.ExecuteAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueryExecutionResult
            {
                Rows = new List<Dictionary<string, object?>> { new() { ["n"] = 1 } },
                RowCount = 1,
                ExecutionTimeMs = 0.5
            });
        return executor;
    }

    private static QueryController CreateController(
        Mock<IQueryExecutor> executor,
        Mock<IVibeUsageRepository> usage,
        int? clientId)
    {
        var controller = new QueryController(
            executor.Object, usage.Object, NullLogger<QueryController>.Instance);
        var httpContext = new DefaultHttpContext();
        if (clientId.HasValue)
            httpContext.Items["ClientId"] = clientId.Value;
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task ExecuteQuery_Success_WritesMeteringRow()
    {
        var executor = SuccessfulExecutor();
        var usage = new Mock<IVibeUsageRepository>();
        var controller = CreateController(executor, usage, clientId: 42);

        var actionResult = await controller.ExecuteQuery(new QueryRequest { Sql = "SELECT 1" });

        actionResult.Should().BeOfType<OkObjectResult>();
        usage.Verify(u => u.IncrementUsageAsync(42, null, "queries"), Times.Once);
    }

    [Fact]
    public async Task ExecuteQuery_MeteringThrows_StillReturns200WithResult()
    {
        var executor = SuccessfulExecutor();
        var usage = new Mock<IVibeUsageRepository>();
        usage
            .Setup(u => u.IncrementUsageAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("forced metering failure"));
        var controller = CreateController(executor, usage, clientId: 42);

        var actionResult = await controller.ExecuteQuery(new QueryRequest { Sql = "SELECT 1" });

        var ok = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<QueryResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Data.Should().HaveCount(1, "the query result is returned unaffected by the metering failure");
    }

    [Fact]
    public async Task ExecuteQuery_UnresolvedClientId_SkipsMetering()
    {
        var executor = SuccessfulExecutor();
        var usage = new Mock<IVibeUsageRepository>();
        var controller = CreateController(executor, usage, clientId: null);

        var actionResult = await controller.ExecuteQuery(new QueryRequest { Sql = "SELECT 1" });

        actionResult.Should().BeOfType<OkObjectResult>();
        usage.Verify(
            u => u.IncrementUsageAsync(It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<string>()),
            Times.Never);
    }
}
