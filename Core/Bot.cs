using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using MoongBot.Core.Manager;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Victoria;

namespace MoongBot.Core
{
    public class Bot
    {
        private DiscordSocketClient _client;
        private CommandService _commandService;

        public Bot()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Debug
            });

            _commandService = new CommandService(new CommandServiceConfig()
            {
                LogLevel = LogSeverity.Debug,
                CaseSensitiveCommands = true,
                DefaultRunMode = Discord.Commands.RunMode.Async,
                IgnoreExtraArgs = true
            });

            var collection = new ServiceCollection();

            collection.AddSingleton(_client);
            collection.AddSingleton(_commandService);
            collection.AddLavaNode();

            ServiceManager.SetProvider(collection);

            //NotificationManager.ScheduleDailyWeatherCheck();
        }       

        public async Task MainAsync()
        {
            if (string.IsNullOrWhiteSpace(ConfigManager.Config.Token)) return;

            await CommandManager.LoadCommmandsAsync();
            await EventManager.LoadCommands();
            await _client.LoginAsync(TokenType.Bot, ConfigManager.Config.Token);
            await _client.StartAsync();

            await Task.Delay(-1);
        }       
    }
}
