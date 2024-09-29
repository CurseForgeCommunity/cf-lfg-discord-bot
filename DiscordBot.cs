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

        public List<ulong> ValidCreateChannels = new List<ulong>()
        {
            1277688413713334342,
            1281901450406002751,
            1281901486611234816,
            1281901522933776395,
            1281902255448264707,
            1289870525794881628
        };

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

            client.UserVoiceStateUpdated += async (user, oldState, newState) =>
            {
                if (oldState.VoiceChannel != null)
                {
                    if (!ValidCreateChannels.Contains(oldState.VoiceChannel.Id))
                    {
                        var voiceChannel = client.GetChannel(oldState.VoiceChannel.Id) as SocketVoiceChannel;

                        if (voiceChannel!.ConnectedUsers.Count == 0)
                        {
                            logger.LogInformation("User {User} left voice channel {Channel}, no users left. Removing channel.", user.Username, oldState.VoiceChannel.Name);

                            await oldState.VoiceChannel.DeleteAsync();
                        }
                    }
                }

                if (newState.VoiceChannel != null)
                {
                    if (ValidCreateChannels.Contains(newState.VoiceChannel.Id))
                    {
                        logger.LogInformation("User {User} entered voice channel {Channel}, creating channel", user.Username, newState.VoiceChannel.Name);
                        var guild = client.GetGuild(newState.VoiceChannel.Guild.Id);

                        if (guild != null)
                        {
                            var category = guild.CategoryChannels.First(x => x.Id == newState.VoiceChannel.CategoryId);

                            var voiceChannelCount = category.Channels.Count(x => x.GetChannelType() == ChannelType.Voice && x.Id != newState.VoiceChannel.Id);

                            var channel = await guild.CreateVoiceChannelAsync($"LFG {voiceChannelCount.ToString().PadLeft(2, '0')}", x =>
                            {
                                x.CategoryId = newState.VoiceChannel.CategoryId;
                                x.UserLimit = 8;
                                x.ChannelType = ChannelType.Voice;
                            });

                            var guildUser = user as SocketGuildUser;

                            await guildUser!.ModifyAsync(x =>
                            {
                                x.Channel = channel;
                            });

                            var newPermissions = new OverwritePermissions(
                                connect: PermValue.Allow,
                                speak: PermValue.Allow,
                                stream: PermValue.Allow,
                                viewChannel: PermValue.Allow,
                                moveMembers: PermValue.Allow,
                                useVoiceActivation: PermValue.Allow
                            );

                            await channel.AddPermissionOverwriteAsync(guildUser, newPermissions);
                        }
                    }
                }
            };

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
