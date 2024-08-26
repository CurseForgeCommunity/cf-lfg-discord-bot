using CF_LFG_Bot;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.Dev.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("CFLFG_")
    .Build();

ulong.TryParse(configuration["AdminGuild"], out var adminGuild);

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddLogging(services => services.AddConsole());
        services.AddSingleton(x => new DiscordSocketConfig
        {
            UseInteractionSnowflakeDate = false
        });
        services.AddSingleton<DiscordShardedClient>();
        services.AddHostedService(x =>
            new DiscordBot(
                x.GetRequiredService<ILogger<DiscordBot>>(),
                x.GetRequiredService<DiscordShardedClient>(),
                configuration["Discord:BotToken"]!,
                adminGuild,
                x
            )
        );
    })
    .Build()
    .RunAsync();