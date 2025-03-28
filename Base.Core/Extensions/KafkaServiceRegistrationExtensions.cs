using Base.Core.Kafka;
using Base.Core.Kafka.Interface;
using Base.Core.Model;
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
                    var service = new KafkaConsumerService(logger, subscribeSetting);
                    dictionary[settingKey] = service;
                }
            }

            return dictionary;
        });

        return services;
    }

}

