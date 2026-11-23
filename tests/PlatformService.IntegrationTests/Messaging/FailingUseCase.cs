using Microsoft.Extensions.DependencyInjection;
using PlatformService.Application.Games;

namespace PlatformService.IntegrationTests.Messaging;

/// <summary>
/// Puts a layer in front of the real <see cref="IFinishedGameService"/> of the app that throws the exception of
/// <paramref name="failure"/> for a call, or passes the call on when it returns <see langword="null"/>.
/// </summary>
/// <param name="failure">Gets the number of the call, starting with 1.</param>
internal sealed class FailingUseCase(Func<int, Exception?> failure)
{
    private int _calls;

    /// <summary>The number of the calls so far, which is the number of the deliveries of the message.</summary>
    public int Calls => Volatile.Read(ref _calls);

    /// <summary>Replaces the use case in the services of the app.</summary>
    public void Register(IServiceCollection services)
    {
        var original = services.Single(service => service.ServiceType == typeof(IFinishedGameService));
        var implementation = original.ImplementationType!;
        services.Remove(original);
        services.Add(new ServiceDescriptor(implementation, implementation, original.Lifetime));
        services.Add(new ServiceDescriptor(
            typeof(IFinishedGameService),
            provider => new Layer(this, (IFinishedGameService)provider.GetRequiredService(implementation)),
            original.Lifetime));
    }

    private sealed class Layer(FailingUseCase failing, IFinishedGameService inner) : IFinishedGameService
    {
        public Task<RecordOutcome> RecordAsync(RecordFinishedGameCommand command, CancellationToken cancellationToken)
        {
            var call = Interlocked.Increment(ref failing._calls);
            return failing.Invoke(call) is { } exception
                ? Task.FromException<RecordOutcome>(exception)
                : inner.RecordAsync(command, cancellationToken);
        }
    }

    private Exception? Invoke(int call) => failure(call);
}
