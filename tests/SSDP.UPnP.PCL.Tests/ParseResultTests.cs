using SSDP.UPnP.PCL.Model;
using Xunit;

namespace SSDP.UPnP.PCL.Tests;

public class ParseResultTests
{
    [Fact]
    public void Success_CarriesValue()
    {
        var result = ParseResult<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_CarriesError()
    {
        var result = ParseResult<int>.Failure("bad input");

        Assert.False(result.IsSuccess);
        Assert.Equal("bad input", result.Error);
    }

    [Fact]
    public void Match_DispatchesOnOutcome()
    {
        Assert.Equal("42", ParseResult<int>.Success(42).Match(v => v.ToString(), e => e));
        Assert.Equal("bad", ParseResult<int>.Failure("bad").Match(v => v.ToString(), e => e));
    }
}
