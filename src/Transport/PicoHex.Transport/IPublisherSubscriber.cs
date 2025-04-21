namespace PicoHex.Transport;

public interface IPublisherSubscriber
{
    // 主题管理
    Task CreateTopicAsync(string topic, TopicProperties properties);
    Task DeleteTopicAsync(string topic);

    // 消息操作
    Task PublishAsync(string topic, byte[] message, CancellationToken ct = default);
    Task SubscribeAsync(string topic, Func<byte[], Task> messageHandler);
    Task UnsubscribeAsync(string topic);
}

public record TopicProperties(RetentionPolicy Retention, int MaxSubscribers);

public enum RetentionPolicy
{
    Volatile,
    Persistent
}
