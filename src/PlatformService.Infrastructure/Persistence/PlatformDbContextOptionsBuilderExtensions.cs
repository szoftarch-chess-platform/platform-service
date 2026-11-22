using Microsoft.EntityFrameworkCore;

namespace PlatformService.Infrastructure.Persistence;

/// <summary>
/// The provider settings of <see cref="PlatformDbContext"/>, shared by the service and the EF Core tools.
/// </summary>
internal static class PlatformDbContextOptionsBuilderExtensions
{
    /// <summary>Sets up Npgsql and the snake case names of PS-ADR-7.</summary>
    /// <param name="connectionString">Left out when the EF Core tools only build the model.</param>
    public static DbContextOptionsBuilder UsePlatformDatabase(this DbContextOptionsBuilder builder, string? connectionString)
    {
        if (connectionString is null)
        {
            builder.UseNpgsql();
        }
        else
        {
            builder.UseNpgsql(connectionString);
        }

        return builder.UseSnakeCaseNamingConvention();
    }
}
