using Discord;
using Discord.Commands;
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
        [Command("help")]
        [Alias("도움")]
        [Remarks("봇의 사용법을 알려드립니다")]
        public async Task HelpCommand()
        {
            await CommandManager.HelpCommandAsync(Context.Guild, Context.Channel as ITextChannel);
        }

        [Command("list")]
        [Alias("목록")]
        [Remarks("날씨 알림을 받을 수 있는 도시명 리스트를 간략하게 보여드립니다")]
        public async Task ListCommand()
        {           
            await CommandManager.ListCommandAsync(Context.Guild, Context.Channel as ITextChannel);
        }

        [Command("move")]
        [Alias("이동")]
        [Remarks("봇을 다른 채널에서 사용중일 때 사용자가 접속중인 음성 채널로 옮겨올 수 있어요.")]
        public async Task MoveCommand()
        {
            await AudioManager.ChangeCannelAsync(Context.Guild, Context.User as IVoiceState, Context.Channel as ITextChannel);
        }
    }
}
