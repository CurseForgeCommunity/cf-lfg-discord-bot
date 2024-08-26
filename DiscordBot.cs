using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace CF_LFG_Bot
{
    public partial class DiscordBot(ILogger<DiscordBot> logger, DiscordShardedClient client, string botToken, ulong adminGuild, IServiceProvider serviceProvider) : BackgroundService
    {
        private InteractionService _interactionService;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            client.ShardReady += Client_ShardReady;

            logger.LogInformation("Starting the Discord bot service");

            client.Log += Log;

            await client.LoginAsync(TokenType.Bot, botToken);
            await client.StartAsync();

            logger.LogInformation("Discord bot service is running");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    foreach (var shard in client.Shards)
                    {
                        await shard.SetGameAsync("Playing LFG with all the users", type: ActivityType.CustomStatus);
                    }
                }
                catch (Exception err)
                {
                    logger.LogError(err, "An error occurred while checking for new projects");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }

            logger.LogInformation("Discord bot service is stopping");

            await client.StopAsync();
            await client.LogoutAsync();

            logger.LogInformation("Discord bot service has stopped");
        }

        private async Task Log(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                case LogSeverity.Error:
                    logger.LogError(msg.Exception, msg.Message);
                    break;
                case LogSeverity.Warning:
                    logger.LogWarning(msg.Exception, msg.Message);
                    break;
                case LogSeverity.Info:
                    logger.LogInformation(msg.Exception, msg.Message);
                    break;
                case LogSeverity.Verbose:
                case LogSeverity.Debug:
                    logger.LogDebug(msg.Exception, msg.Message);
                    break;
            }
            await Task.CompletedTask;
        }

        private async Task Client_ShardReady(DiscordSocketClient _client)
        {
            client.ShardReady -= Client_ShardReady;
            _interactionService = new InteractionService(_client);

            await _client.SetGameAsync("Playing LFG with all the users", type: ActivityType.CustomStatus);

            _interactionService.Log += Log;

            logger.LogInformation("Registering slash commands");
            await _interactionService.AddModulesAsync(Assembly.GetEntryAssembly(), serviceProvider);
            await _interactionService.RegisterCommandsGloballyAsync(true);

            if (_interactionService.Modules.Any())
            {
                await _interactionService.AddModulesToGuildAsync(adminGuild, true, _interactionService.Modules.First(x => x.Name == "AdminCommands"));
            }

            client.InteractionCreated += Client_InteractionCreated;

            logger.LogInformation("Slash commands registered");
        }

        private async Task Client_InteractionCreated(SocketInteraction interaction)
        {
            logger.LogDebug("Interaction received: {Interaction}", interaction);
            var ctx = new ShardedInteractionContext(client, interaction);
            await _interactionService.ExecuteCommandAsync(ctx, serviceProvider);
            logger.LogDebug("Interaction handled");
        }
    }
}
