using Confluent.Kafka;

namespace Base.Core.Model
{
    public class KafkaSetting
    {
        public string BootstrapServers { get; set; }
        public string GroupId { get; set; }
        public string Topic { get; set; }
        public string EnableAutoCommit { get; set; }
        public AutoOffsetReset AutoOffsetReset { get; set; }
    }
}
