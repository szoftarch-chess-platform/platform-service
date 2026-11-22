namespace PlatformService.IntegrationTests;

/// <summary>New names for the data of a test, because the tests share one database.</summary>
internal static class TestNames
{
    /// <summary>A user name of 13 characters that matches the rules of ADR-4 and no other test.</summary>
    public static string NewUserName() => $"u{Guid.NewGuid():N}"[..13];
}
