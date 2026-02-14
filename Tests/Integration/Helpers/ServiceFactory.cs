using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TruLoad.Backend.Services.Interfaces;

namespace TruLoad.Backend.Tests.Integration.Helpers;

/// <summary>
/// Factory for creating common service instances and mocks used across integration tests.
/// Centralises boilerplate so that test classes stay focused on behaviour under test.
/// </summary>
public static class ServiceFactory
{
    /// <summary>
    /// Creates a Mock&lt;ICacheService&gt; that accepts all calls without throwing.
    /// GetStringAsync returns null (cache miss), SetStringAsync and RemoveAsync complete immediately.
    /// Tests that need specific cache behaviour can add further Setup calls on the returned mock.
    /// </summary>
    public static Mock<ICacheService> CreateMockCacheService()
    {
        var mock = new Mock<ICacheService>();

        mock.Setup(c => c.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        mock.Setup(c => c.SetStringAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return mock;
    }

    /// <summary>
    /// Returns a no-op logger that discards all log output.
    /// Suitable for services that require an ILogger&lt;T&gt; but whose log output is irrelevant to the test.
    /// </summary>
    public static ILogger<T> CreateNullLogger<T>()
    {
        return NullLogger<T>.Instance;
    }
}