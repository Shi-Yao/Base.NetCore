using Base.Core.Kafka;
using Base.Core.Kafka.Interface;
using Base.Core.Model;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Base.Core.Extensions;

public static class KafkaServiceRegistrationExtensions
{
    public static IServiceCollection RegisterKafkaServices(this IServiceCollection services,
        IConfiguration configuration,
        params string[] kafkaSettingKeys)
    {

        services.AddSingleton<IProducer<Null, string>>(sp =>
        {
            var bootstrapServers = configuration.GetValue<string>("Kafka:BootstrapServers");

            var config = new ProducerConfig { BootstrapServers = bootstrapServers };
            return new ProducerBuilder<Null, string>(config).Build();
        });

        services.AddSingleton(provider =>
        {
            var dictionary = new Dictionary<string, IKafkaConsumerService>();
            var producer = provider.GetRequiredService<IProducer<Null, string>>(); // 取得共用的 Producer

            foreach (var settingKey in kafkaSettingKeys)
            {
                var subscribeSetting = configuration.GetSection($"KafkaSubscribeSettings:{settingKey}")
                    .Get<KafkaSetting>();

                if (subscribeSetting != null)
                {
                    var logger = provider.GetRequiredService<ILogger<KafkaConsumerService>>();
                    var service = new KafkaConsumerService(logger, subscribeSetting, producer);
                    dictionary[settingKey] = service;
                }
            }

            return dictionary;
        });

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


        return services;
    }

}

