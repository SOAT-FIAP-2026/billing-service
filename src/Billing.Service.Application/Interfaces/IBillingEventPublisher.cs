namespace Billing.Service.Application.Interfaces
{
    public interface IBillingEventPublisher
    {
        Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
            where TEvent : class;
    }
}
