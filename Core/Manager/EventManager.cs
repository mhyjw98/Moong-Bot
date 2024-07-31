using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Victoria;
using Victoria.EventArgs;
using System.Numerics;
using System.Threading.Channels;
using Victoria.Enums;

namespace MoongBot.Core.Manager
{
    public static class EventManager
    {
        private static LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
        private static DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private static CommandService _commandService = ServiceManager.GetService<CommandService>();

        private static bool isMonitoring = true;

        public static Task LoadCommands()
        {
            _client.Log += message =>
            {
                Console.WriteLine($"[{DateTime.Now}]\t({message.Source})\t{message.Message}");
                return Task.CompletedTask;
            };

            _commandService.Log += message =>
            {
                Console.WriteLine($"[{DateTime.Now}]\t({message.Source})\t{message.Message}");
                return Task.CompletedTask;
            };

            _client.Ready += OnReady;
            _client.MessageReceived += OnMessageReceived;
            _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
            _lavaNode.OnTrackEnded += OnLavaTrackEnded;
            _lavaNode.OnTrackStarted += OnLavaTrackStarted;

            return Task.CompletedTask;
        }

        
        private static Task OnLavaTrackStarted(TrackStartEventArgs arg)
        {
            ulong guildId = arg.Player.VoiceChannel.GuildId;
            ConnectionManager.StopTimer(guildId);
            return Task.CompletedTask;
        }

        private static Task OnLavaTrackEnded(TrackEndedEventArgs arg)
        {
            ulong guildId = arg.Player.VoiceChannel.GuildId;
            System.Timers.Timer ConnectionTimer = new System.Timers.Timer();
            ConnectionTimer.Interval = 600 * 1000;
            ConnectionTimer.Enabled = false;
            ConnectionTimer.Elapsed += async (s, e) =>
            {
                ConnectionTimer.Stop();
                var voiceChannel = arg.Player.VoiceChannel;
                if (voiceChannel != null)
                {
                    await _lavaNode.LeaveAsync(voiceChannel);
                }
                else
                {
                    ConnectionTimer.Dispose();
                }

            };
            if (!ConnectionManager.guildConnecionTimer.ContainsKey(guildId))
            {
                ConnectionManager.guildConnecionTimer.Add(guildId, ConnectionTimer);
            }
            else
            {
                ConnectionManager.guildConnecionTimer[guildId] = ConnectionTimer;
            }
            ConnectionManager.StartTimer(guildId);
            return Task.CompletedTask;
        }
        private static async Task OnUserVoiceStateUpdated(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            if (user.IsBot) return;
            if (before.VoiceChannel is null) return;
            else if (!_lavaNode.HasPlayer(before.VoiceChannel.Guild)) return;
            else if (_lavaNode.HasPlayer(before.VoiceChannel.Guild))
            {
                var player = _lavaNode.GetPlayer(before.VoiceChannel.Guild);
                var vc = player.VoiceChannel as SocketVoiceChannel;
                if (vc.ConnectedUsers.Count == 1)
                {
                    try
                    {
                        if (player.PlayerState is Victoria.Enums.PlayerState.Playing) await player.StopAsync();
                        Console.WriteLine("Left by voicestateupdated");
                        await _lavaNode.LeaveAsync(player.VoiceChannel);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ERROR {ex.Message}");
                    }
                }

            }
            return;
        }
        private static async Task OnMessageReceived(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;
            var context = new SocketCommandContext(_client, message);
            var argPos = 0;
            IResult result;

            if (message.Author.IsBot) return;

            if (message.Channel is IDMChannel && message.Author.Id != _client.CurrentUser.Id)
            {
                if (message.Author.Id == ConfigManager.Config.OwnerId)
                {
                    if (message.Content.Equals("실행", StringComparison.OrdinalIgnoreCase))
                    {
                        isMonitoring = true;
                        await message.Channel.SendMessageAsync("모니터링이 활성화되었습니다.");
                    }
                    else if (message.Content.Equals("멈춰", StringComparison.OrdinalIgnoreCase))
                    {
                        isMonitoring = false;
                        await message.Channel.SendMessageAsync("모니터링이 비활성화되었습니다.");
                    }
                    if(message.Content.Equals("종료", StringComparison.OrdinalIgnoreCase))
                    {                  
                        await message.Channel.SendMessageAsync("봇이 종료됩니다.");
                        await UpdateBotStatusAsync("봇이 꺼져있습니다.");
                        await _client.StopAsync(); 
                        Environment.Exit(0); 
                    }
                }
                else
                {
                    await message.Channel.SendMessageAsync("이 명령을 실행할 권한이 없습니다.");
                }
                return;
            }
            if (message.Channel is IDMChannel || message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;

            var targetUserIds = ConfigManager.Config.TargetUserIds;

            bool isTargetUser = targetUserIds.Contains(message.Author.Id);           

            if (isTargetUser && isMonitoring)
            {
                foreach (var word in ConfigManager.Config.TargetWords)
                {
                    if (message.Content.Contains(word, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"단어 '{word}'를 감지했습니다. 사용한 사람의 아이디 : {message.Author.Id}");

                        // 이모티콘 추가
                        ulong serverEmojiId = 755624486887358476;
                        string serverEmojiName = ":070:";

                        var guild = (message.Channel as SocketGuildChannel)?.Guild;
                        var emoji = guild?.Emotes.FirstOrDefault(e => e.Id == serverEmojiId);

                        if (emoji != null)
                        {
                            await message.AddReactionAsync(emoji);
                        }
                        else
                        {
                            Console.WriteLine("이모지를 서버에서 찾을 수 없습니다.");
                        }
                        Console.WriteLine($"Added reaction to message: {message.Content}");
                        break;
                    }
                }
            }
                
            if (!(message.HasStringPrefix(ConfigManager.Config.Prefix, ref argPos)))
            {
                var krLength = ConfigManager.Config.TtsPrefix.Length;
                if (message.Content.Substring(0, krLength + 1) == ConfigManager.Config.TtsPrefix + " ")
                {
                    result = await _commandService.ExecuteAsync(context, "뭉", ServiceManager.Provider);
                }
                else return;

            }
            else
            {
                result = await _commandService.ExecuteAsync(context, argPos, ServiceManager.Provider);
            };

            if (!result.IsSuccess)
            {
                if (result.Error == CommandError.UnknownCommand) return;
            }
        }

        private static async Task OnReady()
        {
            try
            {
                await _lavaNode.ConnectAsync();               
            }
            catch (Exception ex)
            {
                throw ex;
            }


            Console.WriteLine($"[{DateTime.Now}]\t(READY)\tBot is Ready");
            await _client.SetStatusAsync(UserStatus.Online);
            await UpdateBotStatusAsync("봇이 동작 중입니다.");
            await _client.SetGameAsync($"사용법: {ConfigManager.Config.Prefix}도움 또는 {ConfigManager.Config.Prefix}help",
                streamUrl: null, ActivityType.Playing);
        }

        private static async Task UpdateBotStatusAsync(string status)
        {
            var channel = _client.GetChannel(1267466762790764676) as IMessageChannel;
            if (channel == null) return;

            var embed = new EmbedBuilder
            {
                Title = status,
                Description = $"!날씨 : 날씨 정보를 제공합니다.\n{ConfigManager.Config.Prefix}tts or {ConfigManager.Config.TtsPrefix} : 명령어 뒤에 쓴 텍스트를 음성메시지로 변환해 읽어줍니다.",
                Color = Color.Blue
            }.Build();

            var messages = await channel.GetMessagesAsync(100).FlattenAsync();
            var botMessages = messages.Where(m => m.Author.Id == _client.CurrentUser.Id).ToList();

            foreach (var msg in botMessages)
            {
                // 메시지의 임베드를 가져와서 상태 메시지와 비교
                var messageEmbed = msg.Embeds.FirstOrDefault();
                if (messageEmbed != null && (messageEmbed.Title == "봇이 꺼져있습니다." || messageEmbed.Title == "봇이 동작 중입니다."))
                {
                    await msg.DeleteAsync();
                }
            }

            await channel.SendMessageAsync(embed: embed);
        }
    }
}
