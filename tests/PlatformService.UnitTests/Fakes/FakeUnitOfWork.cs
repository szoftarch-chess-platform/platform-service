using PlatformService.Application.Abstractions;

namespace PlatformService.UnitTests.Fakes;

internal sealed class FakeUnitOfWork(List<string> log) : IUnitOfWork
{
    /// <summary>Makes the next commit fail as if a parallel request had replaced the same token first.</summary>
    public bool ConflictOnNextCommit { get; set; }

    /// <summary>Makes the next commit fail as if a parallel delivery had stored the same game first.</summary>
    public bool RecordedGameOnNextCommit { get; set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (ConflictOnNextCommit)
        {
            ConflictOnNextCommit = false;
            log.Add("conflict");
            throw new ConcurrencyConflictException("A parallel request changed the same data first.", new InvalidOperationException());
        }

        if (RecordedGameOnNextCommit)
        {
            RecordedGameOnNextCommit = false;
            log.Add("recorded game");
            throw new GameAlreadyRecordedException("A parallel delivery stored the same game first.", new InvalidOperationException());
        }

        log.Add("commit");
        return Task.CompletedTask;
    }
}
