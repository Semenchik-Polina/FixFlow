using FixFlow.Application.Results;

namespace FixFlow.Application.Tests.Results;

public class ResultTests
{
    [Fact]
    public void Success_NonGeneric_HasNoError()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ImplicitConversion_FromValue_IsSuccessWithValue()
    {
        Result<string> result = "value";

        Assert.True(result.IsSuccess);
        Assert.Equal("value", result.Value);
    }

    [Fact]
    public void ImplicitConversion_FromNotFound_IsErrorWithoutValue()
    {
        Result<string> result = Result.NotFound();

        Assert.False(result.IsSuccess);
        Assert.Equal(new ResultError(ErrorType.NotFound), result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Unprocessable_Detail_IsUnprocessableWithDetail()
    {
        Result result = Result.Unprocessable("Not allowed.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unprocessable, result.Error.Type);
        Assert.Equal("Not allowed.", result.Error.Detail);
    }

    [Fact]
    public void FieldError_FieldAndMessage_IsUnprocessableWithThatFieldOnly()
    {
        Result result = Result.FieldError("TechnicianId", "Unknown technician.");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Unprocessable, result.Error.Type);
        var error = Assert.Single(result.Error.FieldErrors!);
        Assert.Equal("TechnicianId", error.Key);
        Assert.Equal(["Unknown technician."], error.Value);
    }
}
