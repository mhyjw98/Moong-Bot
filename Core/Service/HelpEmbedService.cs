using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MoongBot.Core.Commands;
using MoongBot.Core.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.NewFolder
{
    public static class HelpEmbedService
    {
        private static CommandService _commandService = ServiceManager.GetService<CommandService>();
        private static List<EmbedBuilder> _embedPages = new List<EmbedBuilder>();

        public static void LoadEmbed()
        {
            _embedPages = GenerateHelpEmbeds();
        }

        public static int PageCount => _embedPages.Count;

        public static List<EmbedBuilder> GenerateHelpEmbeds()
        {
            Console.WriteLine("GenerateHelpEmbeds 실행");
            var embedBuilders = new List<EmbedBuilder>();

            try
            {
                var commands = _commandService.Commands
                    .Where(c => !c.Attributes.Any(attr => attr.GetType() == typeof(HiddenAttribute)))
                    .ToList();

                int commandsPerPage = 10;
                int pageCount = (int)Math.Ceiling(commands.Count / (double)commandsPerPage);

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var embedBuilder = new EmbedBuilder()
                        .WithTitle("Moong Bot")
                        .WithThumbnailUrl("https://cdn.discordapp.com/avatars/1260478160135520266/81fc022fcaf088dd035ccb56d26a47c0.png?size=128") 
                        .WithColor(new Color(255, 145, 200))
                        .WithDescription("TTS 메세지를 출력해주는 Moong Bot입니다. " + ConfigManager.Config.TtsPrefix
                        + " \'메시지\' 로 메시지를 TTS로 만들어 재생시켜줍니다. TTS 명령어 사용시 자동으로 음성채널에 연결해 재생합니다." +
                        " 기본 명령어는 " + ConfigManager.Config.Prefix + "입니다.");

                    var commandsToShow = commands.Skip(pageIndex * commandsPerPage).Take(commandsPerPage).ToList();

                    foreach (var c in commandsToShow)
                    {
                        string aliases = string.Join(", ", c.Aliases);
                        embedBuilder.AddField(aliases, c.Remarks, false);
                    }

                    var footer = new EmbedFooterBuilder()
                        .WithText($"Page {pageIndex + 1} / {pageCount}");
                    embedBuilder.WithFooter(footer);

                    embedBuilders.Add(embedBuilder);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating help embeds: {ex.Message}");
            }

            return embedBuilders;
        }

        public static EmbedBuilder GetEmbedForPage(int pageIndex)
        {
            if (pageIndex >= 0 && pageIndex < _embedPages.Count)
            {
                return _embedPages[pageIndex];
            }
            return _embedPages[0];
        }
    }
}
