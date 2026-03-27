using Base.Core.Model;

namespace Base.Core.Kafka.Interface;

public interface IKafkaProducerService
{
    Task PublishAsync<T>(
        T message,
        string? key = null,
        string? topic = null)
        where T : class;
}
public interface IKafkaConsumerService
{
    Task SubscribeAsync<T>(
        Func<T, Task> ProcessMessageFunc,
        CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>
    /// 獲取Kafka配置
    /// </summary>
    KafkaSetting Settings { get; }
}
