using System.Text.Json;

namespace PlatformService.IntegrationTests;

internal static class HttpResponseMessageExtensions
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
