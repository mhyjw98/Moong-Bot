using Discord;
using Discord.Commands;
using Discord.WebSocket;
using MoongBot.Core.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Commands
{
    public class ConvCommands : ModuleBase<SocketCommandContext>
    {
        ulong lottoChannelId = ConfigManager.Config.LottoChannelId;
        ulong coinChannelId = ConfigManager.Config.CoinChannelId;

        [Command("help")]
        [Alias("도움")]
        [Remarks("봇의 사용법을 알려드립니다")]
        public async Task HelpCommand()
        {
            ulong channelId = Context.Channel.Id;

            if (channelId == lottoChannelId)
            {
                await CommandManager.LottoRouletteHelpCommandAsync(Context.Guild, Context.Channel as ITextChannel);
            }
            else if (channelId == coinChannelId)
            {
                await CommandManager.CoinHelpCommandAsync(Context.Channel as ITextChannel);
            }
            else
            {
                await CommandManager.HelpCommandAsync(Context.Guild, Context.Channel as ITextChannel);
            }
        }
        public async Task HelpCommand(SocketMessageComponent component, IGuild guild, ITextChannel channel)
        {
            ulong channelId = channel.Id;

            if (channelId == lottoChannelId)
            {
                await CommandManager.LottoRouletteHelpCommandAsync(component, guild, channel);
            }
            else if (channelId == coinChannelId)
            {
                await CommandManager.CoinHelpCommandAsync(component, channel);
            }
            else
            {
                await CommandManager.HelpCommandAsync(guild, channel);
            }
        }

        [Command("날씨목록")]
        [Alias("weatherlist")]
        [Remarks("날씨 정보를 받을 수 있는 도시명 리스트를 간략하게 보여드립니다")]
        public async Task ListCommand()
        {           
            await CommandManager.ListCommandAsync(Context.Guild, Context.Channel as ITextChannel);
        }

        public async Task ListCommand(SocketGuild guild, IMessageChannel channel)
        {
            await CommandManager.ListCommandAsync(guild, channel as ITextChannel);
        }

        [Command("move")]
        [Alias("이동")]
        [Remarks("봇을 다른 채널에서 사용중일 때 사용자가 접속중인 음성 채널로 옮겨올 수 있습니다")]
        public async Task MoveCommand()
        {
            await AudioManager.ChangeCannelAsync(Context.Guild, Context.User as IVoiceState, Context.Channel as ITextChannel);
        }
    }
}
