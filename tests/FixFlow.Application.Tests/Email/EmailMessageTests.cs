using FixFlow.Application.Email;

namespace FixFlow.Application.Tests.Email;

public class EmailMessageTests
{
    [Fact]
    public void ToString_LeavesOutTheAddressAndTheBodies()
    {
        var message = new EmailMessage(
            "jane.doe@example.test",
            "Reset your password",
            "https://fixflow.example.test/reset-password#token=secret-token",
            "<a href=\"https://fixflow.example.test/reset-password#token=secret-token\">Reset</a>");

        Assert.Equal("EmailMessage { Subject = Reset your password }", message.ToString());
    }
}
