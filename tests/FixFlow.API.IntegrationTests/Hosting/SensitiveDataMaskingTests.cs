using System.Text.Json;

namespace FixFlow.API.IntegrationTests.Hosting;

public class SensitiveDataMaskingTests
{
    private const string Mask = "***MASKED***";
    private const string EmailValue = "jane.doe@example.test";
    private const string TokenValue = "fake-token-value";
    private const string PasswordValue = "fake-password-value";

    [Fact]
    public void Log_DestructuredObjectWithSensitiveProperties_MasksThemAndKeepsTheId()
    {
        var id = Guid.CreateVersion7();

        var output = LogCapture.Render(log => log.Information(
            "Signed up {@User}", new { Id = id, Email = EmailValue, Token = TokenValue, Password = PasswordValue }));

        var user = output.GetProperty("User");
        Assert.Equal(Mask, user.GetProperty("Email").GetString());
        Assert.Equal(Mask, user.GetProperty("Token").GetString());
        Assert.Equal(Mask, user.GetProperty("Password").GetString());
        Assert.Equal(id.ToString(), user.GetProperty("Id").GetString());
        AssertAbsent(output, EmailValue, TokenValue, PasswordValue);
    }

    [Fact]
    public void Log_ScalarSensitiveProperty_MasksValueAndRenderedMessage()
    {
        var output = LogCapture.Render(log => log.Information("Password reset requested for {Email}", EmailValue));

        Assert.Equal(Mask, output.GetProperty("Email").GetString());
        Assert.Contains(Mask, output.GetProperty("@m").GetString(), StringComparison.Ordinal);
        AssertAbsent(output, EmailValue);
    }

    [Fact]
    public void Log_NestedObjectListAndDictionary_MasksSensitivePropertiesAtAnyDepth()
    {
        var output = LogCapture.Render(log => log.Information("Request {@Request}", new
        {
            Account = new { PhoneNumber = "+1 555 0100", Role = "Tenant" },
            Invites = new[] { new { InviteToken = TokenValue } },
            Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer fake-bearer", ["Accept"] = "text/plain" },
        }));

        var request = output.GetProperty("Request");
        Assert.Equal(Mask, request.GetProperty("Account").GetProperty("PhoneNumber").GetString());
        Assert.Equal("Tenant", request.GetProperty("Account").GetProperty("Role").GetString());
        Assert.Equal(Mask, request.GetProperty("Invites")[0].GetProperty("InviteToken").GetString());
        Assert.Equal(Mask, request.GetProperty("Headers").GetProperty("Authorization").GetString());
        Assert.Equal("text/plain", request.GetProperty("Headers").GetProperty("Accept").GetString());
        AssertAbsent(output, "+1 555 0100", TokenValue, "fake-bearer");
    }

    [Theory]
    [InlineData("Password")]
    [InlineData("password")]
    [InlineData("NewPassword")]
    [InlineData("PasswordHash")]
    [InlineData("ResetToken")]
    [InlineData("PasswordResetToken")]
    [InlineData("SecurityStamp")]
    [InlineData("NormalizedEmail")]
    [InlineData("UserName")]
    [InlineData("ApiKey")]
    [InlineData("Cookie")]
    [InlineData("ConnectionString")]
    [InlineData("SasUrl")]
    [InlineData("FirstName")]
    [InlineData("StreetAddress")]
    public void Log_SensitivePropertyName_MasksValue(string name)
    {
        var output = LogCapture.Render(log => log.Information("Value {" + name + "}", "value-to-hide"));

        Assert.Equal(Mask, output.GetProperty(name).GetString());
        AssertAbsent(output, "value-to-hide");
    }

    [Fact]
    public void Log_SensitivePropertyWithNonStringValue_MasksIt()
    {
        var token = Guid.NewGuid();

        var output = LogCapture.Render(log => log.Information("Issued {Token}", token));

        Assert.Equal(Mask, output.GetProperty("Token").GetString());
        AssertAbsent(output, token.ToString());
    }

    [Fact]
    public void Log_SasUrlUnderNeutralName_MasksOnlyTheSignature()
    {
        const string signature = "c2lnbmF0dXJlLXZhbHVl";
        const string url = "http://127.0.0.1:10000/devstoreaccount1/attachments/a/b?sv=2025-01-05&sp=cw&sig=" + signature;

        var output = LogCapture.Render(log => log.Information("Upload slot {Url} {Uri}", url, new Uri(url)));

        Assert.EndsWith("&sig=" + Mask, output.GetProperty("Url").GetString(), StringComparison.Ordinal);
        Assert.StartsWith(
            "http://127.0.0.1:10000/devstoreaccount1/attachments/a/b?", output.GetProperty("Uri").GetString(),
            StringComparison.Ordinal);
        AssertAbsent(output, signature);
    }

    [Fact]
    public void Log_EmailUnderNeutralNameOrInTemplate_MasksIt()
    {
        var output = LogCapture.Render(log => log.Information("Mail to " + EmailValue + " for {Recipient}", EmailValue));

        Assert.Equal(Mask, output.GetProperty("Recipient").GetString());
        AssertAbsent(output, EmailValue);
    }

    [Fact]
    public void Log_IdsAndOrdinaryValues_AreNotMasked()
    {
        // Fixed id: a random one could, rarely, look like a card number to the credit-card operator
        var workOrderId = Guid.Parse("0199e1f2-7b3c-7d4e-8f90-a1b2c3d4e5f6");
        var path = $"/api/v1/work-orders/{workOrderId}";

        var output = LogCapture.Render(log => log.Information(
            "{RequestPath} {WorkOrderId} {Status} {StatusCode}", path, workOrderId, "InProgress", 200));

        Assert.Equal(path, output.GetProperty("RequestPath").GetString());
        Assert.Equal(workOrderId.ToString(), output.GetProperty("WorkOrderId").GetString());
        Assert.Equal("InProgress", output.GetProperty("Status").GetString());
        Assert.Equal(200, output.GetProperty("StatusCode").GetInt32());
    }

    private static void AssertAbsent(JsonElement output, params string[] values)
    {
        var json = output.GetRawText();
        foreach (var value in values)
        {
            Assert.DoesNotContain(value, json, StringComparison.Ordinal);
        }
    }
}
