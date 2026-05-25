using CloudNativePaymentsLab.Api.Modules.Payments.Domain;

namespace CloudNativePaymentsLab.Api.Modules.Payments.Application;

public interface IPaymentAttemptRepository
{
    Task AddAsync(PaymentAttempt attempt, CancellationToken cancellationToken);
}
