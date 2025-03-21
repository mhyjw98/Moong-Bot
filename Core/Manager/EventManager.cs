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
using Discord.Rest;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Google.Apis.Gmail.v1.Data;
using MoongBot.Core.Commands;
using MoongBot.Core.Service;
using System.Text.Json;
using System.Reflection;
using ScottPlot.Renderable;

namespace MoongBot.Core.Manager
{
    public static class EventManager
    {
        private static LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
        private static DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private static CommandService _commandService = ServiceManager.GetService<CommandService>();
        private static CoinMarketManager _coinManager = new CoinMarketManager();
        private static DatabaseManager _dbManager = new DatabaseManager();

        private static Dictionary<ulong, ulong> processedMessages = new();
        private static readonly string AudioFilePath = "AudioFiles";
        private static readonly string filePath = Path.Combine("jsonFiles", "processedMessages.json");
        private static System.Threading.Timer? _moongStatusTimer;
        private static System.Threading.Timer? _lottoStatusTimer;
        private static System.Threading.Timer? _coinStatusTimer;

        private static List<GuildEmote>? cloverEmotes;
        private static List<GuildEmote>? fourLeafClovers;
        private static List<GuildEmote>? threeLeafClovers;

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
            _client.ReactionAdded += OnReactionAdded;
            _client.UserVoiceStateUpdated += OnUserVoiceStateUpdated;
            _lavaNode.OnTrackEnded += OnLavaTrackEnded;
            _lavaNode.OnTrackStarted += OnLavaTrackStarted;

            return Task.CompletedTask;
        }
        public static void InitGuildEmote()
        {
            ulong guildId = ConfigManager.Config.EmojiGuildId;// 이모지용 서버 ID 

            var guild = _client.GetGuild(guildId);
            if (guild != null)
            {
                cloverEmotes = guild.Emotes.Where(e => e.Name.Contains("clover", StringComparison.OrdinalIgnoreCase)).ToList();
                fourLeafClovers = cloverEmotes.Where(e => e.Name.Contains("cloverf", StringComparison.OrdinalIgnoreCase)).ToList();
                threeLeafClovers = cloverEmotes.Where(e => !e.Name.Contains("cloverf", StringComparison.OrdinalIgnoreCase)).ToList();

                Console.WriteLine($"[{DateTime.Now}]\t(Init)\tGuildEmoteInit");                
            }
            else
            {
                Console.WriteLine($"서버를 찾을 수 없습니다. Guild ID: {guildId}");
            }           
        }
        public static async Task PenaltyNotification(ulong userId, bool isFirst, int panaltyCount)
        {
            try
            {
                ulong channelId = ConfigManager.Config.BushChannelId;// 풀숲 채널 ID
                var channel = _client.GetChannel(channelId) as ITextChannel;
                if (channel == null)
                {
                    Console.WriteLine($"채널을 찾을 수 없습니다. Channel ID: {channelId}");
                    return;
                }               

                Random rand = new Random();
                GuildEmote? selectedFourLeafClover;
                List<GuildEmote> selectedThreeLeafClovers;

                if (fourLeafClovers != null && threeLeafClovers != null)
                {
                    selectedFourLeafClover = fourLeafClovers.OrderBy(_ => rand.Next()).Take(1).FirstOrDefault();
                    selectedThreeLeafClovers = threeLeafClovers.OrderBy(_ => rand.Next()).Take(19).ToList();
                }
                else
                {
                    Console.WriteLine($"클로버 이모티콘이 null값이였습니다.");
                    return;
                }
                

                if (selectedFourLeafClover == null)
                {
                    Console.WriteLine($"네잎클로버를 가져오지 못했습니다.");
                    return;
                }

                var allEmotes = selectedThreeLeafClovers.ToList();

                allEmotes = allEmotes.OrderBy(_ => rand.Next()).ToList();

                var centralIndex = rand.Next(2, 18);
                allEmotes.Insert(centralIndex, selectedFourLeafClover);

                string description = "슬롯머신에 사용할 클로버를 수집해주세요.\n\n반응으로 달린 클로버들 중 네잎클로버가 포함된 이모티콘이 1개가 있습니다.\n\n네잎클로버가 포함된 이모티콘에 반응을 달면 성공입니다!\n\n세잎클로버에 반응이 달려있으면 네잎클로버에 반응을 달아도 실패로 인식하니 모든 반응을 눌러서 쉽게 찾으려는 꼼수는 쓰지 마세요!\n\n";

                if(panaltyCount != 0)
                {
                    description += $"빚 청산까지 남은 네잎클로버 수 : {panaltyCount}개";
                }

                var embedBuilder = new EmbedBuilder()
                {
                    Title = "네잎클로버 찾기",
                    Description = description,
                    Color = Color.Green
                };

                IUserMessage message;

                if (isFirst)
                {
                    message = await channel.SendMessageAsync($"<@{userId}>님! 대출금을 상환하지 못해 서버의 활동이 차단되었습니다. 네잎클로버를 {panaltyCount}개 찾아야 다시 활동 가능합니다!", embed: embedBuilder.Build());
                }
                else
                {
                    message = await channel.SendMessageAsync(embed: embedBuilder.Build());
                }

                _ = Task.Run(async () =>
                {
                    foreach (var emote in allEmotes)
                    {
                        if (processedMessages.ContainsKey(message.Id))
                        {
                            break;
                        }
                        await message.AddReactionAsync(emote);
                    }
                });
            }
            catch(Exception ex)
            {
                Console.WriteLine($"PenaltyNotification에서 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
            
        }

        public static async Task PenaltyNotification(ulong userId, bool isCoinRepay)
        {
            var user = _client.GetUser(userId);
            if (user == null)
            {
                Console.WriteLine($"유저를 찾을 수 없습니다. User ID: {userId}");
                return;
            }
            ulong channelId;
            if (isCoinRepay)
            {
                channelId = ConfigManager.Config.LottoChannelId;
            }
            else
            {
                channelId = ConfigManager.Config.CoinChannelId;
            }
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                Console.WriteLine($"채널을 찾을 수 없습니다. Channel ID: {channelId}");
                return;
            }
            else
            {
                await channel.SendMessageAsync($"{user.Mention}님 대출금 상환 날짜가 되어서 자동상환 되었습니다.");
            }
        }

        public static async Task NewsNotification(Embed embed, string userMentions = "")
        {
            ulong channelId = ConfigManager.Config.CoinChannelId;
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                Console.WriteLine($"채널을 찾을 수 없습니다. Channel ID: {channelId}");
                return;
            }
            else
            {
                if (!string.IsNullOrEmpty(userMentions))
                {
                    await channel.SendMessageAsync(userMentions, embed: embed);
                }
                else
                {
                    await channel.SendMessageAsync(embed: embed);
                }
            }
        }

        public static async Task SlotNewsNotification(Embed embed)
        {
            ulong channelId = ConfigManager.Config.LottoChannelId;
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                Console.WriteLine($"채널을 찾을 수 없습니다. Channel ID: {channelId}");
                return;
            }
            else
            {
                await channel.SendMessageAsync(embed: embed);               
            }
        }

        public static async Task AutoTradeNotification(string result)
        {
            ulong channelId = ConfigManager.Config.CoinChannelId;
            var channel = _client.GetChannel(channelId) as IMessageChannel;
            if (channel == null)
            {
                Console.WriteLine($"채널을 찾을 수 없습니다. Channel ID: {channelId}");
                return;
            }
            else
            {
                await channel.SendMessageAsync(result);
            }
        }
        private static async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cacheable, Cacheable<IMessageChannel, ulong> cacheableChannel, SocketReaction reaction)
        {
            var message = await cacheable.GetOrDownloadAsync();
            var channel = await cacheableChannel.GetOrDownloadAsync();           

            // 특정 이모지와 메시지 ID 확인
            if (reaction.Emote is Emote emote && emote.Id == 1277978535679496308)
            {
                var emojiId1 = 1277978535679496308;
                var emojiId2 = 1277978551441686659;
                var emojiId3 = 1277978565182226455;

                var emojiName1 = "jil";
                var emojiName2 = "tu";
                var emojiName3 = "na";

                var emoji1 = Emote.Parse($"<:{emojiName1}:{emojiId1}>");
                var emoji2 = Emote.Parse($"<:{emojiName2}:{emojiId2}>");
                var emoji3 = Emote.Parse($"<:{emojiName3}:{emojiId3}>");

                await message.AddReactionAsync(emoji1);
                await message.AddReactionAsync(emoji2);
                await message.AddReactionAsync(emoji3);
            }
            if (reaction.Emote is Emote emote2) 
            {               
                var user = reaction.User.Value as SocketGuildUser;
                if (user == null || user.IsBot)
                    return;

                ulong userId = user.Id;                
                int panaltyCount = await _dbManager.GetPenaltyCount(userId);

                if (panaltyCount == 0)
                {
                    return;
                }

                var cloverEmoteIds = fourLeafClovers.Where(e => e.Name.StartsWith("cloverf")).Select(e => e.Id).ToList();
                var threeLeafEmoteIds = threeLeafClovers.Where(e => e.Name.StartsWith("clover") && !e.Name.StartsWith("cloverf")).Select(e => e.Id).ToList();

                if (processedMessages.ContainsKey(message.Id))
                {
                    Console.WriteLine("이 메시지에서 이미 패널티가 처리되었습니다.");
                    return;
                }

                ulong guildId = ConfigManager.Config.EmojiGuildId;// 이모지용 서버 ID 

                var guild = _client.GetGuild(guildId);

                bool cloverEmotesReacted = false;
                bool hasThreeLeafReactions = false;
               
                if (cloverEmoteIds.Contains(emote2.Id))
                {                   
                    cloverEmotesReacted = true;
                }

                if (cloverEmotesReacted && !hasThreeLeafReactions)
                {
                    Console.WriteLine("모든 네잎클로버 이모지에 반응함.");
                    await new LoanService().CheckAndRemovePenaltyAsync(user, userId, guild, channel as SocketTextChannel);
                    if(panaltyCount > 1)
                     {
                        panaltyCount--;
                       await PenaltyNotification(userId, false, panaltyCount);
                    }
                        
                    processedMessages[message.Id] = userId;
                    await SaveProcessedMessagesAsync();
                }               
            }
        }
        private static async Task SaveProcessedMessagesAsync()
        {
            
            var jsonString = JsonSerializer.Serialize(processedMessages);
            await File.WriteAllTextAsync(filePath, jsonString);
        }

        public static async Task LoadProcessedMessagesAsync()
        {
            if (File.Exists(filePath))
            {
                var jsonString = await File.ReadAllTextAsync(filePath);
                processedMessages = JsonSerializer.Deserialize<Dictionary<ulong, ulong>>(jsonString);
            }
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
                        Console.WriteLine($"Error message : OnUserVoiceStateUpdated 에서 에러 발생");
                        await ExceptionManager.HandleExceptionAsync(ex);
                    }
                }

            }
            return;
        }
        private static async Task OnMessageReceived(SocketMessage arg)
        {
            _ = Task.Run(async () =>
            {
                SocketCommandContext context = null;

                try
                {
                    if (arg == null)
                    {
                        return;
                    }

                    var message = arg as SocketUserMessage;

                    if (message == null)
                    {
                        return;
                    }

                    context = new SocketCommandContext(_client, message);
                    var argPos = 0;
                    IResult result;

                    ulong channelId = message.Channel.Id;
                    ulong authorId = message.Author.Id;
                    ulong ownerId = ConfigManager.Config.OwnerId;

                    if (message.Embeds.Any(embed => embed.Footer?.Text == "MoongBotStatus"))
                    {
                        return;
                    }

                    if (channelId == ConfigManager.Config.BushChannelId && authorId == _client.CurrentUser.Id)
                    {
                        Console.WriteLine("풀숲 채널에서 봇 메시지 감지");
                        
                        string messageContent = message.Content;
                                                                      
                        var mentionedUsers = message.MentionedUsers;

                        if (mentionedUsers.Any())
                        {
                            var match = Regex.Match(messageContent, @"네잎클로버를 (\d+)개");
                            if (match.Success)
                            {
                                var loanService = new LoanService();
                                int penalty = -1;

                                Console.WriteLine("패널티 메시지 매칭 완료");
                                string panaltyCountString = match.Groups[1].Value;
                                if (int.TryParse(panaltyCountString, out int panaltyCount))
                                {
                                    penalty = panaltyCount;
                                }
                                foreach (SocketGuildUser user in mentionedUsers)
                                {
                                    ulong mentionedUserId = user.Id;

                                    // Call ApplyPenaltyAsync with the mentioned user's ID
                                    await loanService.ApplyPenaltyAsync(user, mentionedUserId, context.Guild, penalty);
                                }
                            }                            
                        }    
                    }

                    if (channelId == ConfigManager.Config.MoongBotChannelId)
                    {
                        _moongStatusTimer?.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                        // 타이머가 없으면 새로 생성
                        if (_moongStatusTimer == null)
                        {
                            _moongStatusTimer = new System.Threading.Timer(async _ =>
                            {
                                await UpdateBotStatusAsync(ConfigManager.Config.MoongBotChannelId, "봇이 동작 중입니다.");
                            }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                        }
                    }
                    if (channelId == ConfigManager.Config.LottoChannelId)
                    {
                        _lottoStatusTimer?.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                        // 타이머가 없으면 새로 생성
                        if (_lottoStatusTimer == null)
                        {
                            _lottoStatusTimer = new System.Threading.Timer(async _ =>
                            {
                                await UpdateBotStatusAsync(ConfigManager.Config.LottoChannelId, "봇이 동작 중입니다.");
                            }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                        }
                    }
                    if (channelId == ConfigManager.Config.CoinChannelId)
                    {
                        _coinStatusTimer?.Change(TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);

                        // 타이머가 없으면 새로 생성
                        if (_coinStatusTimer == null)
                        {
                            _coinStatusTimer = new System.Threading.Timer(async _ =>
                            {
                                await UpdateBotStatusAsync(ConfigManager.Config.CoinChannelId, "봇이 동작 중입니다.");
                            }, null, TimeSpan.FromMinutes(5), Timeout.InfiniteTimeSpan);
                        }
                    }

                    await NotifyOwnerOnMessageInChannel(message, channelId, authorId, ownerId);

                    if (message.Author.IsBot) return;

                    await HandleDMCommands(message, authorId, ownerId);

                    if (message.Channel is IDMChannel || message.HasMentionPrefix(_client.CurrentUser, ref argPos)) return;

                    if (!(message.HasStringPrefix(ConfigManager.Config.Prefix, ref argPos)))
                    {
                        var krLength = ConfigManager.Config.TtsPrefix.Length;
                        if (message.Content.Length > krLength + 1 && message.Content.Substring(0, krLength + 1) == ConfigManager.Config.TtsPrefix + " ")
                        {
                            result = await _commandService.ExecuteAsync(context, "뭉", ServiceManager.Provider);

                            if (!result.IsSuccess)
                            {
                                if (result.Error == CommandError.UnknownCommand) return;
                            }
                        }
                        else if (Bot.SimpleTtsUsers.Contains(context.User.Id))
                        {
                            if (message.Content.Equals("참여", StringComparison.OrdinalIgnoreCase) || message.Content.Equals("join", StringComparison.OrdinalIgnoreCase))
                            {
                                result = await _commandService.ExecuteAsync(context, "참여", ServiceManager.Provider);
                            }
                            else if ((message.Content.Equals("정지", StringComparison.OrdinalIgnoreCase) || message.Content.Equals("stop", StringComparison.OrdinalIgnoreCase)))
                            {
                                result = await _commandService.ExecuteAsync(context, "정지", ServiceManager.Provider);
                            }
                            else if ((message.Content.Equals("퇴장", StringComparison.OrdinalIgnoreCase) || message.Content.Equals("leave", StringComparison.OrdinalIgnoreCase)))
                            {
                                result = await _commandService.ExecuteAsync(context, "퇴장", ServiceManager.Provider);
                            }
                            else
                            {
                                result = await _commandService.ExecuteAsync(context, "채널", ServiceManager.Provider);
                            }

                            if (!result.IsSuccess)
                            {
                                if (result.Error == CommandError.UnknownCommand) return;
                            }
                        }
                        else if (AudioManager.AudioFiles.ContainsKey(message.Content))
                        {
                            bool isOwner = (message.Author.Id == ConfigManager.Config.OwnerId) || (message.Author.Id == 1132210328743202837);

                            if (message.Channel is IVoiceChannel)
                            {
                                await AudioManager.PlayMp3Async(context.Guild, message.Author as IVoiceState, message.Channel as ITextChannel, message.Content, isOwner);
                            }
                        }
                    }
                    else
                    {
                        result = await _commandService.ExecuteAsync(context, argPos, ServiceManager.Provider);

                        if (!result.IsSuccess)
                        {
                            if (result.Error == CommandError.UnknownCommand) return;
                        }
                    };

                    await RegistMp3Async(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"메시지 처리 중 예외 발생: {ex.Message}");
                    await ExceptionManager.HandleExceptionAsync(ex, context?.Guild, context?.Channel, context?.Message?.Content);
                }
            });           
        }
        private static async Task NotifyOwnerOnMessageInChannel(SocketUserMessage message, ulong channelId, ulong authorId, ulong ownerId)
        {
            if (channelId == 1267420529774690380 && authorId == 1267421762497417298)
            {
                if(message.Embeds.Count > 0)
                {
                    foreach (var embed in message.Embeds)
                    {
                        if (embed == null || string.IsNullOrEmpty(embed.Title))
                            return;

                        string titlePattern = @"^\[아이네 뱅온\]\(.*?\)$";
                        bool isIneOnAir = Regex.IsMatch(embed.Title, titlePattern);

                        if (isIneOnAir)
                        {
                            await message.Channel.SendMessageAsync($"<@{ownerId}> 아이네 방송 시작!");
                            return;
                        }
                    }
                }
                await message.Channel.SendMessageAsync($"<@{ownerId}> 아이네 방송 시작!");
                return;
            }
        }
        private static async Task HandleDMCommands(SocketUserMessage message, ulong authorId, ulong ownerId)
        {
            if (message.Channel is IDMChannel && authorId != _client.CurrentUser.Id)
            {
                if (authorId == ownerId)
                {
                    if (message.Content.Equals("종료", StringComparison.OrdinalIgnoreCase))
                    {
                        await message.Channel.SendMessageAsync("봇이 종료됩니다.");
                        await UpdateBotStatusAsync(ConfigManager.Config.MoongBotChannelId,"봇이 꺼져있습니다.");
                        await UpdateBotStatusAsync(ConfigManager.Config.LottoChannelId, "봇이 꺼져있습니다.");
                        await UpdateBotStatusAsync(ConfigManager.Config.CoinChannelId, "봇이 꺼져있습니다.");

                        await _client.LogoutAsync();
                        await _client.StopAsync();
                        Environment.Exit(0);
                    }
                }               
            }
        }       

        private static async Task RegistMp3Async(SocketMessage message)
        {            
            try
            {
                if (message is not IUserMessage userMessage)
                {
                    return;
                }

                if (BotCommands.isRegister && BotCommands._lastRegisteringUserId == message.Author.Id)
                {
                    var attachments = userMessage.Attachments;
                    var mp3File = attachments.FirstOrDefault(a => a.Filename.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase));

                    if (mp3File != null)
                    {
                        var filePath = Path.Combine(AudioFilePath, mp3File.Filename);
                        Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                        using (var httpClient = new System.Net.Http.HttpClient())
                        {
                            var fileBytes = await httpClient.GetByteArrayAsync(mp3File.Url);
                            await File.WriteAllBytesAsync(filePath, fileBytes);
                        }

                        if (BotCommands._lastRegisteringUserId.HasValue)
                        {
                            await _dbManager.RegisterAudioFileAsync(BotCommands._currentWord, filePath, message.Channel, message.Author.Id, BotCommands.isSpecialRegister, BotCommands._currentVolume);              
                            BotCommands._lastRegisteringUserId = null;
                            BotCommands.isRegister = false;
                            BotCommands.isSpecialRegister = false;
                            BotCommands._currentWord = null;
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("업로드된 파일이 mp3 형식이 아닙니다. mp3 파일을 업로드해 주세요.");
                        BotCommands._lastRegisteringUserId = null;
                        BotCommands.isRegister = false;
                        BotCommands.isSpecialRegister = false;
                        BotCommands._currentWord = null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : RegistMp3Async 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
            
        }
            private static async Task OnReady()
            {
            try
            {
                if (_lavaNode.IsConnected)
                {
                    Console.WriteLine("LavaNode가 이미 연결되어 있어서 기존 연결을 끊고 다시 연결");
                    await _lavaNode.DisconnectAsync();
                    await Task.Delay(1000);
                }

                await _lavaNode.ConnectAsync();
                Console.WriteLine("LavaNode 연결 성공");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : _lavaNode.ConnectAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }


            Console.WriteLine($"[{DateTime.Now}]\t(READY)\tBot is Ready");
            InitGuildEmote();
            await _client.SetStatusAsync(UserStatus.Online);
            await UpdateBotStatusAsync(ConfigManager.Config.MoongBotChannelId, "봇이 동작 중입니다.");
            await UpdateBotStatusAsync(ConfigManager.Config.LottoChannelId, "봇이 동작 중입니다.");
            await UpdateBotStatusAsync(ConfigManager.Config.CoinChannelId, "봇이 동작 중입니다.");
            await _client.SetGameAsync($"사용법: {ConfigManager.Config.Prefix}도움 또는 {ConfigManager.Config.Prefix}help",
                streamUrl: null, ActivityType.Playing);

            // 가격 변동 작업 실행
            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Starting price update...");
                    await _coinManager.StartPriceUpdateAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error message : OnReady의 _coinManager.StartPriceUpdateAsync 에서 에러 발생");
                    await ExceptionManager.HandleExceptionAsync(ex);
                }
            });

            // 이벤트 스케줄러 작업 실행
            _ = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Starting event scheduler...");
                    await _coinManager.StartEventSchedulerAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error message : OnReady의 _coinManager.StartEventSchedulerAsync 에서 에러 발생");
                    await ExceptionManager.HandleExceptionAsync(ex);
                }
            });
        }

        public static async Task UpdateBotStatusAsync(ulong channelId, string status)
        {
            var channel = _client.GetChannel(channelId) as IMessageChannel;

            if (channel == null) return;

            string channelDescription = "";

            if (channelId == ConfigManager.Config.MoongBotChannelId)
            {
                channelDescription = $"> **날씨 정보 제공** :white_sun_small_cloud:\n**\'{ConfigManager.Config.Prefix}날씨 지역\'** 명령어를 사용하면 해당 지역의 날씨 정보를 제공해요. 날씨 정보를 제공하는 지역명은 **\'{ConfigManager.Config.Prefix}목록\'** 명령어로 확인할 수 있어요.\n\n> **TTS 기능 지원** :robot:\n**\'{ConfigManager.Config.Prefix}tts 할 말\'** or **\'{ConfigManager.Config.TtsPrefix} 할 말\'** 명령어 뒤에 쓴 텍스트를 음성메시지로 변환해 읽어줘요. 음성채널에 입장한 후 사용할 수 있어요.\n\n> **간편 TTS 기능** :microphone:\n음성 채널에 입장해서 **\'{ConfigManager.Config.Prefix}등록뭉\'** 명령어를 사용한 후에는 **\'{ConfigManager.Config.TtsPrefix}\'** 명령어를 사용하지 않아도 TTS기능이 실행돼요. 참여, 퇴장, 정지 명령어도 **\'{ConfigManager.Config.Prefix}\'**없이 사용 가능해요. 봇과 같은 음성채널에 있어야해요.\n\n> **mp3 파일 재생 기능** :arrow_forward:\n**\'{ConfigManager.Config.Prefix}등록 명령어 볼륨\'** 명령어를 쓰고 mp3 파일을 올리면 명령어와 mp3파일이 등록되어 음성채널 채팅방에서 명령어를 사용했을 때 해당 mp3파일이 설정된 볼륨값으로 재생돼요. 볼륨을 적지 않고 **\'{ConfigManager.Config.Prefix}등록 명령어\'**로 등록하면 기본 볼륨값인 70%로 적용되어요. 볼륨은 0 ~ 100의 값을 넣어주세요! **\'{ConfigManager.Config.Prefix}삭제 명령어\'**로 등록된 mp3파일을 삭제할 수 있어요. \n\n> **룰렛, 로또, 복권** :moneybag:\n<#{ConfigManager.Config.LottoChannelId}> 채널에서 명령어를 통해 룰렛을 돌리고 로또를 구매해보세요! 룰렛으로 상품과 :dollar:를 벌어 로또를 구매할 수 있어요 로또는 자동, 수동 구매가 가능하고 수동은 1~15의 6개의 숫자를 골라 구매할 수 있어요 로또 당첨 보상으로 :mushroom:을 벌어 다양한 상품을 구매해보세요!\n\n> **슬롯머신 기능** 🎰\n**{ConfigManager.Config.Prefix}슬롯머신** 명령어를 사용해 슬롯머신을 사용할 수 있어요.\n\n> **코인시장** :coin:\n<#{ConfigManager.Config.CoinChannelId}>채널에서 코인관련 소식을 듣고 코인을 매매해보세요!\n\n> **봇의 작동 여부** :placard:\n채널의 고정된 메시지를 보면 봇의 상태를 보여주는 임베드를 확인할 수 있어요.\n\n> **건의사항 및 문제 발생시** :postbox:\n<@{ConfigManager.Config.OwnerId}>에게 DM이나 귓속말로 말해주세요!";
            }
            if (channelId == ConfigManager.Config.CoinChannelId)
            {
                channelDescription = 
                     $"> **튜토리얼** :newspaper:\n**\'{ConfigManager.Config.Prefix}도움\'** 명령어로 코인 관련 명령어 사용법과 기능에 대한 설명을 볼 수 있어요.\n\n" +
                     "> **봇의 작동 여부** :placard:\n채널의 고정된 메시지를 보면 봇의 상태를 보여주는 임베드를 확인할 수 있어요.\n\n" + 
                     $"> **건의사항 및 문제 발생시** :postbox:\n<@{ConfigManager.Config.OwnerId}>에게 DM이나 귓속말로 말해주세요!";
            }
            if (channelId == ConfigManager.Config.LottoChannelId)
            {
                channelDescription = 
                     $"> **튜토리얼** :game_die:\n**'{ConfigManager.Config.Prefix}도움'** 명령어로 로또, 룰렛, 슬롯머신에 대한 설명을 볼 수 있어요.\n\n" +
                     $"> **봇의 작동 여부** :placard:\n채널의 고정된 메시지를 보면 봇의 상태를 보여주는 임베드를 확인할 수 있어요.\n\n" + 
                     $"> **건의사항 및 문제 발생시** :postbox:\n<@{ConfigManager.Config.OwnerId}>에게 DM이나 귓속말로 말해주세요!";
            }
            
            var embed = new EmbedBuilder
            {
                Title = status,
                ThumbnailUrl = _client.CurrentUser.GetAvatarUrl(),
                Description = channelDescription,
                Footer = new EmbedFooterBuilder
                {
                    Text = "MoongBotStatus" 
                },
                Color = new Color(255, 145, 200)
            }.Build();

            MessageComponent buttons = null;


            if (status != "봇이 꺼져있습니다.")
            {
                if(channel.Id == ConfigManager.Config.MoongBotChannelId)
                {
                    
                    buttons = new ComponentBuilder()
                        .WithButton("-도움", "help_btn", ButtonStyle.Primary)
                        .WithButton("-로또룰렛도움", "lrhelp_btn", ButtonStyle.Primary)
                        .WithButton("-코인도움", "coinhelp_btn", ButtonStyle.Primary)
                        .WithButton("-날씨", "weather_btn", ButtonStyle.Primary)
                        .WithButton("-날씨목록", "weatherlist_btn", ButtonStyle.Primary)
                        .WithButton("-단어목록", "wordlist_btn", ButtonStyle.Primary)
                        .WithButton("건의 및 버그제보", "feedback_btn", ButtonStyle.Primary)
                        .Build();
                }
                else if(channel.Id == ConfigManager.Config.LottoChannelId)
                {
                    buttons = new ComponentBuilder()
                        .WithButton("-도움", "help_btn", ButtonStyle.Primary)
                        .WithButton("-지원금", "punding_btn", ButtonStyle.Primary)
                        .WithButton("-잔액", "balance_btn", ButtonStyle.Primary)
                        .WithButton("-내로또", "lotto_btn", ButtonStyle.Primary)
                        .WithButton("-상점", "shop_btn", ButtonStyle.Primary)
                        //.WithButton("-대출", "coinloancaution_btn", ButtonStyle.Primary)
                        //.WithButton("-상환", "repay_btn", ButtonStyle.Primary)
                        .WithButton("-룰렛", "roulette_btn", ButtonStyle.Primary)
                        .WithButton("-복권", "spito_btn", ButtonStyle.Primary)
                        .WithButton("-자동", "auto_btn", ButtonStyle.Primary)
                        .WithButton("-수동", "manual_btn", ButtonStyle.Primary)
                        .WithButton("-슬롯", "simpleslot_btn", ButtonStyle.Primary)
                        .WithButton("-도박슬롯", "nethorslot_btn", ButtonStyle.Primary)
                        .WithButton("-스킵", "skipslot_btn", ButtonStyle.Primary)
                        .WithButton("-슬롯정지", "stop_btn", ButtonStyle.Primary)
                        .WithButton("-랭킹", "ranking_btn", ButtonStyle.Primary)
                        .WithButton("-명예의전당", "hof_btn", ButtonStyle.Primary)
                        .WithButton("건의 및 버그제보", "feedback_btn", ButtonStyle.Primary)
                        .Build();
                }
                else if(channel.Id == ConfigManager.Config.CoinChannelId)
                {
                    buttons = new ComponentBuilder()
                        .WithButton("-도움", "help_btn", ButtonStyle.Primary)
                        .WithButton("-지원금", "punding_btn", ButtonStyle.Primary)
                        .WithButton("-룰렛", "roulette_btn", ButtonStyle.Primary)
                        .WithButton("-코인종목", "coinlist_btn", ButtonStyle.Primary)
                        .WithButton("-포트폴리오", "portfolio_btn", ButtonStyle.Primary)
                        .WithButton("-차트", "chart_btn", ButtonStyle.Primary)                        
                        .WithButton("-자동매매", "autotrade_btn", ButtonStyle.Primary)
                        .WithButton("-자동매매현황", "showautotrade_btn", ButtonStyle.Primary)
                        .WithButton("-자동매매삭제", "deleteautotrade_btn", ButtonStyle.Primary)
                        .WithButton("-랭킹", "ranking_btn", ButtonStyle.Primary)
                        //.WithButton("-대출", "dollarloancaution_btn", ButtonStyle.Primary)
                        //.WithButton("-상환", "repay_btn", ButtonStyle.Primary)
                        .WithButton("-뉴스구독", "subscrib_btn", ButtonStyle.Primary)
                        .WithButton("-구독취소", "unsubscrib_btn", ButtonStyle.Primary)
                        .WithButton("건의 및 버그제보", "feedback_btn", ButtonStyle.Primary)
                        .Build();
                }
            }
            

            var pinnedMessages = await channel.GetPinnedMessagesAsync();
            var botPinnedMessage = pinnedMessages.FirstOrDefault(m => m.Author.Id == _client.CurrentUser.Id);

            if (botPinnedMessage != null)
            {
                var messageEmbed = botPinnedMessage.Embeds.FirstOrDefault();
                if (messageEmbed != null && (messageEmbed.Title == "봇이 꺼져있습니다." || messageEmbed.Title == "봇이 동작 중입니다."))
                {
                    await botPinnedMessage.DeleteAsync();
                }
            }


            var sentMessage = await channel.SendMessageAsync(embed: embed, components: buttons);
            await sentMessage.PinAsync();

            // 고정된 메시지 알림을 삭제
            var newMessages = await channel.GetMessagesAsync(10).FlattenAsync();
            var pinNotification = newMessages.FirstOrDefault(m => m.Type == MessageType.ChannelPinnedMessage);

            if (pinNotification != null)
            {
                await pinNotification.DeleteAsync();
            }
        }       
    }

}
