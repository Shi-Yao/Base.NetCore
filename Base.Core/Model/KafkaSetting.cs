using Confluent.Kafka;

namespace Base.Core.Model
{
    public class KafkaSetting
    {
        /// <summary>
        /// Kafka 伺服器位址
        /// </summary>
        public string BootstrapServers { get; set; }

        /// <summary>
        /// 消費者群組 ID
        /// </summary>
        public string GroupId { get; set; }

        /// <summary>
        /// Topic
        /// </summary>
        public string Topic { get; set; }

        /// <summary>
        /// 是否自動提交 Offset (位移量)
        /// </summary>
        public bool? EnableAutoCommit { get; set; }

        /// <summary>
        /// 當沒有初始 Offset 或 Offset 過期時的處理策略
        /// </summary>
        public AutoOffsetReset? AutoOffsetReset { get; set; }

        /// <summary>
        /// 批次commit
        /// </summary>
        public int CommitBatchSize { get; set; } = 50;
    }
}
