using Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Linq;
using System.Reflection;

namespace Common
{
    public static class CommonModuleRegister
    {
        public static IServiceCollection AddCommonModule(this IServiceCollection services)
        {
            services.AddSingleton<IConfigManager, ConfigManager>();
            services.AddSingleton<IConfigManagerFactory, ConfigManagerFactory>();

            // services.AddSingleton<ILoggerFactory>(_ => new LoggerFactory(name => new SerilogManager(name)));
            return services;
        }
    }
}