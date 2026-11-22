using PlatformService.Application.Abstractions;

namespace PlatformService.UnitTests.Fakes;

internal sealed class FakeUnitOfWork(List<string> log) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        log.Add("commit");
        return Task.CompletedTask;
    }
}
