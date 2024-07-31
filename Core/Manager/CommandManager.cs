using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public static class CommandManager
    {
        private static CommandService _commandService = ServiceManager.GetService<CommandService>();
        private static DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();

        public static async Task LoadCommmandsAsync()
        {
            await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceManager.Provider);
            foreach (var command in _commandService.Commands)
                Console.WriteLine($"Command {command.Name} was loaded.");
        }

        public static async Task HelpCommandAsync(IGuild guild, ITextChannel channel)
        {
            try
            {
                Console.WriteLine($"[{DateTime.Now}] HelpCommandAsync called for guild: {guild.Name}, channel: {channel.Name}");

                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle(_client.CurrentUser.Username);
                embedBuilder.WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());
                embedBuilder.WithColor(255, 145, 200);

                string Description = "TTS 메세지를 출력해주는 " + _client.CurrentUser.Username + "입니다. " + ConfigManager.Config.TtsPrefix
                + " \'메시지\' 로 메시지를 TTS로 만들어 재생시켜줍니다. TTS 명령어 사용시 자동으로 음성채널에 연결해 재생합니다." +
                " 기본 명령어는 " + ConfigManager.Config.Prefix + "입니다.";

                embedBuilder.WithDescription(Description);

                foreach (var c in _commandService.Commands)
                {

                    if (c.Name == "뭉")
                    {
                        embedBuilder.AddField(ConfigManager.Config.TtsPrefix, c.Remarks, false);
                    }
                    else if (Commands.TextCommands.CommandToHide.Contains(c.Name))
                    {
                        continue;
                    }
                    else
                    {
                        string aliases = string.Empty;
                        foreach (string alias in c.Aliases)
                        {
                            aliases += alias;
                            if (alias != c.Aliases.Last())
                                aliases += ", ";
                        }
                        embedBuilder.AddField(aliases, c.Remarks, false);
                    }

                }
                var footer = new EmbedFooterBuilder();
                footer.WithText("\n사용법 예시: 뭉ㅇ 할 말, !tts 할 말 \n -> \'할 말\'이 TTS로 재생됩니다. (먼저 음성 채널에 입장은 필수)");
                embedBuilder.WithFooter(footer);

                Console.WriteLine($"[{DateTime.Now}] Embedding message with title: {_client.CurrentUser.Username}");

                await channel.SendMessageAsync("", false, embedBuilder.Build());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] Error in HelpCommandAsync: {ex.Message}");
            }
        }

        

        public static async Task ListCommandAsync(IGuild guild, ITextChannel channel)
        {           
            try
            {
                var cityList = WeatherManager.CityTranslations.Keys.ToList();
                string cities = string.Join(", ", cityList);

                Console.WriteLine($"[{DateTime.Now}] HelpCommandAsync called for guild: {guild.Name}, channel: {channel.Name}");

                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle($"{ _client.CurrentUser.Username} 날씨 알림");
                embedBuilder.WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());
                embedBuilder.WithColor(255, 145, 200);

                string Description = "날씨 정보를 받을 수 있는 도시명을 알려드립니다.";

                embedBuilder.WithDescription(Description);

                embedBuilder.AddField("도시 목록", cities);

                var footer = new EmbedFooterBuilder();
                footer.WithText($"\n사용법 예시: {ConfigManager.Config.Prefix}날씨 도시명, {ConfigManager.Config.Prefix}등록 도시명");
                embedBuilder.WithFooter(footer);

                Console.WriteLine($"[{DateTime.Now}] Embedding message with title: {_client.CurrentUser.Username}");

                await channel.SendMessageAsync("", false, embedBuilder.Build());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] Error in HelpCommandAsync: {ex.Message}");
            }
        }
    }
}

