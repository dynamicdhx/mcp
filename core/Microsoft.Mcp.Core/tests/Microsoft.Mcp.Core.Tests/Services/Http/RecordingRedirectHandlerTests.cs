using System.Net;
using Microsoft.Mcp.Core.Services.Http;
using Xunit;

namespace Microsoft.Mcp.Core.Tests.Services.Http;

public class RecordingRedirectHandlerTests
{
    [Theory]
    [InlineData(null, "management.azure.com", "management.azure.com")]
    [InlineData("", "management.azure.com", "management.azure.com")]
    [InlineData("eastus2euap", "management.azure.com", "eastus2euap.management.azure.com")]
    [InlineData("EASTUS2EUAP", "management.azure.com", "eastus2euap.management.azure.com")]
    [InlineData("eastus2euap", "westus.management.azure.com", "westus.management.azure.com")]
    [InlineData("eastus2euap", "management.usgovcloudapi.net", "management.usgovcloudapi.net")]
    [InlineData("eastus2euap", "example.blob.core.windows.net", "example.blob.core.windows.net")]
    public async Task SendAsync_RoutesOnlyPublicArmToRegion(string? region, string host, string expectedHost)
    {
        using var handler = new RecordingRedirectHandler(new Uri("http://localhost:5000"), region)
        {
            InnerHandler = new RecordingTestTransport()
        };
        using var client = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"https://{host}/providers/example/items/item?api-version=2026-04-01-preview");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"https://{expectedHost}/", Assert.Single(request.Headers.GetValues("x-recording-upstream-base-uri")));
        Assert.Equal(new Uri("http://localhost:5000/providers/example/items/item?api-version=2026-04-01-preview"), request.RequestUri);
        Assert.Equal(HttpMethod.Patch, request.Method);
    }

    [Fact]
    public void Send_PreservesExistingUpstreamHeader()
    {
        using var handler = new RecordingRedirectHandler(new Uri("http://localhost:5000"), "eastus2euap")
        {
            InnerHandler = new RecordingTestTransport()
        };
        using var client = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://management.azure.com/providers/example?api-version=1");
        request.Headers.Add("x-recording-upstream-base-uri", "https://westus.management.azure.com/");

        using var response = client.Send(request, TestContext.Current.CancellationToken);

        Assert.Equal("https://westus.management.azure.com/", Assert.Single(request.Headers.GetValues("x-recording-upstream-base-uri")));
        Assert.Equal(new Uri("http://localhost:5000/providers/example?api-version=1"), request.RequestUri);
    }

    [Theory]
    [InlineData("eastus2euap.example.com")]
    [InlineData("eastus2euap/path")]
    [InlineData("eastus2euap:443")]
    [InlineData(" eastus2euap")]
    [InlineData("eastus-2")]
    public void Constructor_RejectsInvalidRegion(string region)
    {
        Assert.Throws<ArgumentException>("armRegion", () => new RecordingRedirectHandler(new Uri("http://localhost:5000"), region));
    }

    [Fact]
    public void Constructor_RejectsRegionLongerThanDnsLabel()
    {
        Assert.Throws<ArgumentException>("armRegion", () => new RecordingRedirectHandler(new Uri("http://localhost:5000"), new string('a', 64)));
    }
}