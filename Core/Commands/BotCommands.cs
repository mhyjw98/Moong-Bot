using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using MoongBot.Core.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MoongBot.Core.Commands
{
    [Name("Bot")]
    public class BotCommands : ModuleBase<SocketCommandContext>
    {
        [Command("뭉")]
        [Remarks("TTS를 재생하는 한글 명령어입니다. 한/영 전환 없이 한글로 간편하게 사용해요.")]
        public async Task KrCommand()
        {
            await AudioManager.PlayAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel, Context.Message.Content, ConfigManager.Config.TtsPrefix.Length + 1);
        }

        [Command("tts")]
        [Remarks("TTS 명령어입니다.")]
        public async Task PlayCommand([Remainder] string text)
        {
            await AudioManager.PlayAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel, text);
        }

        [Command("join")]
        [Alias("참여")]
        [Remarks("봇을 해당 음성채팅방에 입장시킬 수 있습니다")]
        public async Task JoinCommand()
        {
            await AudioManager.JoinAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel);
        }

        [Command("leave")]
        [Alias("퇴장")]
        [Remarks("봇을 해당 음성채팅방에서 퇴장시킬 수 있습니다")]
        public async Task LeaveCommand()
        {
            await AudioManager.LeaveAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel);
        }

        [Command("stop")]
        [Alias("정지")]
        [Remarks("TTS 재생을 중단합니다.")]
        public async Task StopCommand()
        {
            await AudioManager.StopAsnyc(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel);
        }

        [Command("register")]
        [Alias("알림등록")]
        [Remarks("등록하면 비 오는 날 알림을 받을 수 있습니다.")]
        public async Task RegisterCommand([Remainder] string city)
        {
            var userId = Context.User.Id;

            await NotificationManager.RegisterNotification(userId, city, Context.Channel as ITextChannel);
        }

        [Command("update")]
        [Alias("알림수정")]
        [Remarks("등록한 알림의 도시명을 수정할 수 있습니다.")]
        public async Task TurnOffCommand([Remainder] string city)
        {
            var userId = Context.User.Id;

            await NotificationManager.UpdateNotification(userId, city, Context.Channel as ITextChannel);
        }

        [Command("turnoff")]
        [Alias("알림해제")]
        [Remarks("날씨 알림을 해제할 수 있습니다.")]
        public async Task TurnOffCommand()
        {
            var userId = Context.User.Id;

            await NotificationManager.TurnOffNotification(userId, Context.Channel as ITextChannel);
        }

        [Command("weather")]
        [Alias("날씨")]
        [Remarks("날씨 정보를 알려줍니다.")]
        public async Task WeatherCommand([Remainder] string city)
        {
            await WeatherManager.WeatherAsync(Context.Channel as ITextChannel, city);
        }
    }
}
