namespace Base.Core.Kafka.Interface;

public interface IKafkaProducerService
{
    Task PublishAsync<T>(T? message) where T : class;
}
public interface IKafkaConsumerService
{
    Task SubscribeAsync<T>(Func<T, Task> ProcessMessageFunc, CancellationToken cancellationToken = default) where T : class;
}
