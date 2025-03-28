using Base.Core.Kafka;
using Base.Core.Kafka.Interface;
using Base.Core.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Base.Core.Extensions
{
    public static class KafkaServiceRegistrationExtensions
    {
        public static IServiceCollection RegisterKafkaServices(this IServiceCollection services,
            IConfiguration configuration,
            params string[] kafkaSettingKeys)
        {
            var subscribeSections = configuration.GetSection("KafkaSubscribeSettings").GetChildren();
            foreach (var section in subscribeSections)
            {
                var key = section.Key;
                var setting = section.Get<KafkaSetting>()
                              ?? throw new ArgumentNullException($"KafkaSubscribeSetting '{key}' not found");

                services.AddSingleton(setting);
                services.AddTransient<IKafkaSubscribeService>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<KafkaSubscribeService>>();
                    return new KafkaSubscribeService(logger, setting);
                });
            }

            var publishSections = configuration.GetSection("KafkaPublishSettings").GetChildren();
            foreach (var section in publishSections)
            {
                var key = section.Key;
                var setting = section.Get<KafkaSetting>()
                              ?? throw new ArgumentNullException($"KafkaPublishSetting '{key}' not found");

                services.AddSingleton(setting);
                services.AddTransient<IKafkaPublishService>(provider =>
                {
                    var logger = provider.GetRequiredService<ILogger<KafkaPublishService>>();
                    return new KafkaPublishService(logger, setting);
                });
            }

            return services;
        }
    }
}
