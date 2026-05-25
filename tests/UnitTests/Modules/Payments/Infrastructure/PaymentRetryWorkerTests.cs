using System.Reflection;
using CloudNativePaymentsLab.Api.Modules.Payments.Application;
using CloudNativePaymentsLab.Api.Modules.Payments.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CloudNativePaymentsLab.UnitTests.Modules.Payments.Infrastructure;

public sealed class PaymentRetryWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCancellationIsAlreadyRequested_ShouldCompleteWithoutCreatingScope()
    {
        var scopeFactory = new ThrowingScopeFactory();
        var worker = new PaymentRetryWorker(
            scopeFactory,
            Options.Create(new PaymentsOptions { RetryPollingIntervalSeconds = 1 }),
            NullLogger<PaymentRetryWorker>.Instance);
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        var executeAsync = typeof(PaymentRetryWorker)
            .GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)executeAsync.Invoke(worker, [cancellationTokenSource.Token])!;

        await task;
        scopeFactory.CreateScopeCalls.Should().Be(0);
    }

    private sealed class ThrowingScopeFactory : IServiceScopeFactory
    {
        public int CreateScopeCalls { get; private set; }

        public IServiceScope CreateScope()
        {
            CreateScopeCalls++;
            throw new InvalidOperationException("A cancelled retry worker should not create a scope.");
        }
    }
}
