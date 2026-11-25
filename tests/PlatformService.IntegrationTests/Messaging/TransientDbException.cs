using System.Data.Common;

namespace PlatformService.IntegrationTests.Messaging;

/// <summary>An error of the database that reports itself as transient, as Npgsql does when the server is not reachable.</summary>
internal sealed class TransientDbException() : DbException("The database is not reachable.")
{
    public override bool IsTransient => true;
}
