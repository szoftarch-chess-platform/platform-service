using Microsoft.Extensions.DependencyInjection;

// FluentValidation.DependencyInjectionExtensions brings Microsoft.Extensions.DependencyInjection.Abstractions 2.1.0,
// which has no nullable annotations. Without this directive List<T> and IServiceCollection disagree
// on nullability, and the build fails with CS8644.
#nullable disable

namespace PlatformService.UnitTests.Fakes;

/// <summary>A bare <see cref="IServiceCollection"/>, enough to call the registration extension methods.</summary>
internal sealed class FakeServiceCollection : List<ServiceDescriptor>, IServiceCollection
{
}
