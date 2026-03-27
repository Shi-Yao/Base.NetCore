using Base.Core.Kafka;
using Base.Core.Kafka.Interface;
using Base.Core.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Base.Core.Extensions;

public static class KafkaServiceRegistrationExtensions
{
    /// <summary>
    /// 自動註冊 appsettings 中所有的 Kafka Services。
    /// 會自動讀取 KafkaSubscribeSettings 和 KafkaPublishSettings 下所有的 topic keys。
    /// </summary>
    /// <param name="services">IServiceCollection 實例。</param>
    /// <param name="configuration">IConfiguration 實例，用於讀取設定檔。</param>
    /// <returns>回傳註冊完成後的 IServiceCollection。</returns>
    public static IServiceCollection RegisterKafkaServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        // 自動讀取所有「訂閱 (Subscribe)」的 topic keys
        var subscribeKeys = configuration.GetSection("KafkaSubscribeSettings")
            .GetChildren()
            .Select(x => x.Key)
            .ToArray();

        // 自動讀取所有「發布 (Publish)」的 topic keys
        var publishKeys = configuration.GetSection("KafkaPublishSettings")
            .GetChildren()
            .Select(x => x.Key)
            .ToArray();

        // 合併所有 keys（使用 Union 並搭配 Distinct 確保 key 不會重複）
        var allKeys = subscribeKeys.Union(publishKeys).Distinct().ToArray();

        // 呼叫另一個重載方法進行實際的註冊動作，並傳入合併後的 allKeys
        return services.RegisterKafkaServices(configuration, allKeys);
    }

    public static IServiceCollection RegisterKafkaServices(this IServiceCollection services,
        IConfiguration configuration,
        params string[] kafkaSettingKeys)
    {
        // 先註冊 Producer Services
        services.AddSingleton(provider =>
        {
            var dictionary = new Dictionary<string, IKafkaProducerService>();

            foreach (var settingKey in kafkaSettingKeys)
            {
                var publishSetting = configuration.GetSection($"KafkaPublishSettings:{settingKey}")
                    .Get<KafkaSetting>();

                if (publishSetting != null)
                {
                    var logger = provider.GetRequiredService<ILogger<KafkaProducerService>>();
                    var service = new KafkaProducerService(logger, publishSetting);
                    dictionary[settingKey] = service;
                }
            }

            return dictionary;
        });

        // 再註冊 Consumer Services
        services.AddSingleton(provider =>
        {
            var dictionary = new Dictionary<string, IKafkaConsumerService>();

            foreach (var settingKey in kafkaSettingKeys)
            {
                var subscribeSetting = configuration.GetSection($"KafkaSubscribeSettings:{settingKey}")
                    .Get<KafkaSetting>();

                if (subscribeSetting != null)
                {
                    var logger = provider.GetRequiredService<ILogger<KafkaConsumerService>>();

                    // 為 DLQ 建立專用的 producer service
                    IKafkaProducerService? producerService = null;
                    if (subscribeSetting.EnableDLQ && !string.IsNullOrEmpty(subscribeSetting.FailedTopic))
                    {
                        var producerLogger = provider.GetRequiredService<ILogger<KafkaProducerService>>();
                        var dlqSetting = new KafkaSetting
                        {
                            BootstrapServers = subscribeSetting.BootstrapServers,
                            Topic = subscribeSetting.FailedTopic
                        };
                        producerService = new KafkaProducerService(producerLogger, dlqSetting);
                    }
                    else if (!subscribeSetting.EnableDLQ)
                    {
                        logger.LogInformation("[Kafka] DLQ disabled for {Topic}", settingKey);
                    }

                    var service = new KafkaConsumerService(logger, subscribeSetting, producerService);
                    dictionary[settingKey] = service;
                }
            }

            return dictionary;
        });

        return services;
    }
}

