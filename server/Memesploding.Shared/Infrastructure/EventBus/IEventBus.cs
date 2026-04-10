namespace Memesploding.Shared.Infrastructure.EventBus;

public interface IEventBus
{
    Task PublishAsync<T>(string channel, T @event) where T : class;
    Task SubscribeAsync<T>(string channel, Func<T, Task> handler) where T : class;
}
