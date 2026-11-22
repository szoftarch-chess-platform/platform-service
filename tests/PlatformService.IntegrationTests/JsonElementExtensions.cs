using System.Text.Json;

namespace PlatformService.IntegrationTests;

internal static class JsonElementExtensions
{
    /// <summary>The string of a property, which the test expects to be there.</summary>
    public static string GetRequiredString(this JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName).GetString();
        Assert.NotNull(value);
        return value;
    }

    /// <summary>The id of a property, which the test expects to be a GUID.</summary>
    public static Guid GetRequiredGuid(this JsonElement element, string propertyName) =>
        Guid.Parse(element.GetRequiredString(propertyName));
}
