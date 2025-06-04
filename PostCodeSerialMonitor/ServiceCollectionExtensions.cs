using Microsoft.Extensions.DependencyInjection;
using PostCodeSerialMonitor.ViewModels;
using PostCodeSerialMonitor.Services;
using System.Text.Json;
using System.Text.Json.Serialization;
using PostCodeSerialMonitor.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Console;

namespace PostCodeSerialMonitor;

public static class ServiceCollectionExtensions
{
    const int SUPPORTED_CONFIG_FORMAT_VERSION = 1;
    public static void AddCommonServices(this IServiceCollection collection)
    {
        // Configure logging
        collection.AddLogging(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddConsole(options =>
                {
                    options.FormatterName = ConsoleFormatterNames.Simple;
                });
        });

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("config.json", optional: true, reloadOnChange: true)
            .Build();

        // Configure options with validation
        collection.Configure<AppConfiguration>(configuration.GetSection(nameof(AppConfiguration)));
        collection.AddOptions<AppConfiguration>()
            .Bind(configuration.GetSection(nameof(AppConfiguration)))
            .Validate(x => {
                return x.FormatVersion == SUPPORTED_CONFIG_FORMAT_VERSION;
            })
            .ValidateOnStart();

        // Register JSON serialization options
        var jsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            AllowTrailingCommas = true,
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter<MetaType>()
            }
        };
        collection.AddSingleton(jsonOptions);

        // Register services
        collection.AddSingleton<ConfigurationService>();
        collection.AddSingleton<SerialService>();
        collection.AddSingleton<MetaUpdateService>();
        collection.AddSingleton<MetaDefinitionService>();
        collection.AddSingleton<SerialLineDecoder>();

        // Register ViewModels
        collection.AddTransient<MainWindowViewModel>();
    }
}