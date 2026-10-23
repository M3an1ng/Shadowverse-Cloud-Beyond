using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ShadowverseBeyond.Server.Controllers;
using ShadowverseBeyond.Server.Dtos.Dsl;
using Xunit;

namespace ShadowverseBeyond.Server.Tests.DSL;

public class DslParseControllerTests
{
    [Fact]
    public void Parse_ReturnsSuccessForValidExpression()
    {
        var controller = new DslController(NullLogger<DslController>.Instance);
        var request = new DslParseRequest { Expression = "1 + 2" };

        var result = controller.Parse(request);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DslParseResultDto>(ok.Value);

        Assert.True(payload.Success);
        Assert.NotNull(payload.Root);
    }

    [Fact]
    public void Parse_ReturnsErrorsForInvalidExpression()
    {
        var controller = new DslController(NullLogger<DslController>.Instance);
        var request = new DslParseRequest { Expression = "1 +" };

        var result = controller.Parse(request);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DslParseResultDto>(ok.Value);

        Assert.False(payload.Success);
        Assert.NotEmpty(payload.Errors);
    }

    [Fact]
    public void Parse_PreservesParenthesizedExpression()
    {
        var controller = new DslController(NullLogger<DslController>.Instance);
        var request = new DslParseRequest { Expression = "(1 + 2)" };

        var result = controller.Parse(request);
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<DslParseResultDto>(ok.Value);

        Assert.True(payload.Success);
        Assert.NotNull(payload.Root);
        Assert.Equal("ParenthesizedExpression", payload.Root!.NodeType);
        Assert.NotNull(payload.Root.Inner);
        Assert.Equal("BinaryOperation", payload.Root.Inner!.NodeType);
    }
}
