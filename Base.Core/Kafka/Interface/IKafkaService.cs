namespace Base.Core.Kafka.Interface;

public interface IKafkaPublishService
{
    Task PublishAsync<T>(T? message) where T : class;
}
public interface IKafkaSubscribeService
{
    Task SubscribeAsync<T>(Func<T, Task> ProcessMessageFunc, CancellationToken cancellationToken = default) where T : class;
}
