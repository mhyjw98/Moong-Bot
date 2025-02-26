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
using Microsoft.Extensions.Logging;
using System.Reflection.Metadata;
using MoongBot.Core.Commands;
using System.Reflection;
using MoongBot.Core.Service;
using MoongBot.Core.NewFolder;
using Discord.Net.Rest;
using Newtonsoft.Json;

namespace MoongBot.Core
{
    public class Bot
    {
        private DiscordSocketClient _client;
        private CommandService _commandService;

        private InteractionManager _interactionManager = new InteractionManager();
        private GmailManager _gmailManager = new GmailManager();
        private LottoManager _lottoManager = new LottoManager();
        private LoanService _loanService = new LoanService();
        private BotCommands _commandModule = new BotCommands();
        private ConvCommands _convCommandModule = new ConvCommands();
        private DatabaseManager _dbManager = new DatabaseManager();

        private Timer _lottoTimer;
        private Timer _resetTimer;

        private List<Timer> _timers = new List<Timer>();
        public static List<ulong> SimpleTtsUsers = new List<ulong>();
        public static Dictionary<ulong, (DateTime LoanTime, int Amount)> loanDataDictionary = new Dictionary<ulong, (DateTime LoanTime, int Amount)>();

        public Bot()
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                LogLevel = LogSeverity.Debug,
                UseInteractionSnowflakeDate = false
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
            collection.AddLogging(configure => configure.AddConsole());

            ServiceManager.SetProvider(collection);           
        }       
        public async Task MainAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ConfigManager.Config.Token)) return;

                await CommandManager.LoadCommmandsAsync();
                await EventManager.LoadCommands();
                await EventManager.LoadProcessedMessagesAsync();
                HelpEmbedService.LoadEmbed();

                await InitializeAudioFilesAsync();
                await InitializeLottoAsync();
                await InitializeRouletteAsync();
                await InitializePundingAsync();
                await InitializeLoanDataAsync();
                await InitializeLoadGmailAsync();

                await _client.LoginAsync(TokenType.Bot, ConfigManager.Config.Token);
                await _client.StartAsync();

                _client.ButtonExecuted += async component =>
                {
                    try
                    {
                        await _interactionManager.HandleButtonClickAsync(component, _commandModule, _convCommandModule);
                    }
                    catch (Exception ex)
                    {
                        // 3초 제한 응답 실패 예외 처리
                        if (ex.Message.Contains("3 seconds"))
                        {
                            Console.WriteLine("3초 응답 제한으로 인해 상호작용 실패.");
                        }
                        // error 10062 예외 처리
                        else if (ex.Message.Contains("error 10062"))
                        {
                            Console.WriteLine("error 10062 발생");
                        }
                        // 다른 모든 예외 처리
                        else
                        {
                            Console.WriteLine($"Error in ButtonExecuted: {ex.Message}");
                        }
                    }                    
                };

                _client.ModalSubmitted += async modal =>
                {
                    try
                    {
                        await _interactionManager.HandleModalSubmittedAsync(modal);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ModalSubmitted: {ex.Message}");
                    }
                };

                SetResetTimer();
                SetLottoTimer();
                //SetLoanTimers();                

                _ = Task.Run(CheckGmailAsync);               

                await Task.Delay(-1);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error message : MainAsync에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }
        
        private async Task CheckGmailAsync()
        {
            try
            {
                while (true)
                {
                    Console.WriteLine("CheckGmailAsync 실행");
                    //var messagesWithImages = await _gmailManager.GetMessagesWithImagesAsync();
                    var messagesWithScreenshots = await _gmailManager.GetMessagesWithScreenshotsAsync();

                    Console.WriteLine($"messagesWithImages : {messagesWithScreenshots.Count} , {messagesWithScreenshots.ToArray()}");

                    if (messagesWithScreenshots.Count > 0)
                    {
                        var channel = _client.GetChannel(ConfigManager.Config.NotificationChannelId) as IMessageChannel;
                        if (channel != null)
                        {
                            foreach (var (headerContent, screenshotPath) in messagesWithScreenshots)
                            {
                                // 메일 헤더 전송
                                await channel.SendMessageAsync($"<@{ConfigManager.Config.OwnerId}> 새로운 델타룬 개발 소식!! :\n```\n{headerContent}\n```");

                                if (File.Exists(screenshotPath))
                                {
                                    await channel.SendFileAsync(screenshotPath);
                                }
                                else
                                {
                                    Console.WriteLine($"Screenshot file not found: {screenshotPath}");
                                }
                            }
                        }
                    }

                    // 30분마다 이메일 확인
                    await Task.Delay(TimeSpan.FromMinutes(30));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : CheckGmailAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }          
        }
        private void SetResetTimer()
        {
            var now = DateTime.Now;
            var midnight = now.Date.AddDays(1); // 다음 자정 시간
            var timeUntilMidnight = midnight - now;

            _resetTimer = new Timer(async _ =>
            {
                await RouletteManager.ResetDailySpins();
                Console.WriteLine("자정이 되어 룰렛 사용 기록이 초기화되었습니다.");

                SlotMachineManager.ResetDailyUsage();
                Console.WriteLine("자정이 되어 슬롯머신 사용 기록이 초기화되었습니다.");

                await _loanService.IncreaseInterestDailyAsync();
                Console.WriteLine("자정이 되어 대출금의 이자가 증가되었습니다.");

                if (DateTime.Now.Day == 1)
                {
                    await SlotMachineManager.ResetSlotRecord();
                    await CoinMarketManager.ResetCoinRecord();
                }

                SetResetTimer(); // 다음 자정에 다시 실행되도록 타이머 재설정
            }, null, timeUntilMidnight, Timeout.InfiniteTimeSpan);
        }

        private void SetLottoTimer()
        {
            var now = DateTime.Now;
            var nextFriday = now.AddDays((5 - (int)now.DayOfWeek + 7) % 7).Date.AddHours(20);

            if (nextFriday <= now)
            {
                nextFriday = nextFriday.AddDays(7); // 이미 지나갔다면 다음 주 금요일로 설정
            }

            var timeUntilNextDraw = nextFriday - now;

            _lottoTimer = new Timer(async _ =>
            {
                await AnnounceLottoResultAsync();
                SetLottoTimer(); // 다음 주 금요일을 위해 타이머 재설정
            }, null, timeUntilNextDraw, Timeout.InfiniteTimeSpan);
        }

        //public void SetLoanTimers()
        //{
        //    foreach (var entry in loanDataDictionary)
        //    {
        //        ulong userId = entry.Key;
        //        DateTime loanTime = entry.Value.LoanTime;
        //        bool isCoin = entry.Value.Amount == 1 ? true : false;

        //        // 현재 날짜를 기준으로 loanTime의 시와 분만을 사용하여 실행 시간을 설정
        //        DateTime now = DateTime.Now;
        //        DateTime scheduledTime = new DateTime(loanTime.Year, loanTime.Month, loanTime.Day, loanTime.Hour, loanTime.Minute, loanTime.Second);

        //        TimeSpan timeUntilExecution = scheduledTime - now;

        //        if (timeUntilExecution <= TimeSpan.Zero)
        //        {
        //            // 상환 시간이 이미 지난 경우: 즉시 실행
        //            Console.WriteLine($"{userId}에 해당하는 유저의 대출 타이머가 이미 지났으므로 즉시 실행");

        //            // 즉시 대출 상환 체크 로직 실행
        //            Task.Run(async () => await ExecuteLoanRepaymentCheckAsync(userId, isCoin));
        //        }
        //        else
        //        {
        //            // 지정된 시간에 한 번만 실행되는 타이머 설정
        //            Console.WriteLine($"{userId}에 해당하는 유저의 대출 타이머 {timeUntilExecution.Days}일 {timeUntilExecution.Hours}시간 {timeUntilExecution.Minutes}분 {timeUntilExecution.Seconds}초 후 실행");

        //            Timer timer = new Timer(async _ =>
        //            {
        //                await ExecuteLoanRepaymentCheckAsync(userId, isCoin);
        //                // 타이머는 한 번만 실행되므로 더 이상 SetLoanTimers 호출 안 함
        //            }, null, timeUntilExecution, Timeout.InfiniteTimeSpan);

        //            _timers.Add(timer);
        //        }
        //    }
        //}

        public async Task ExecuteLoanRepaymentCheckAsync(ulong userId, bool isCoinRepay)
        {
            try
            { 
                var (loanAmount, interest, isCoin, date) = await _dbManager.GetTotalRepaymentAmountAsync(userId, isCoinRepay);
                var (isSuccess, isClear, result) = await _dbManager.RepayLoanAsync(userId, isCoinRepay);
                if (isSuccess)
                {
                    int isCoinValue = isCoinRepay ? 1 : 0;
                    if(result < 1)
                    {
                        result = 0;
                    }

                    if (!isClear && result > 0)
                    {

                        int penalty = 0;

                        if (isCoin)
                        {
                            penalty = Math.Min(10 + ((int)result / 10000), 30);
                        }
                        else
                        {
                            if(loanAmount > 20000000)
                            {
                                penalty = 100;
                            }
                            else
                            {
                                penalty = Math.Min(10 + ((int)result / 50000), 40);
                            }                        
                        }

                        await _dbManager.RecordUserProfitAsync(userId, -result);
                        await _loanService.SendChannelMessage(userId, penalty, isCoinRepay);
                    }
                    else if(isClear || result == 0)
                    {
                        await _loanService.SendChannelMessage(userId, isCoinRepay);
                    }
                    else
                    {
                        Console.WriteLine($"에러 발생");
                    }
                }
                else
                {
                    if (result == -2)
                    {
                        Console.WriteLine($"대출을 하지 않았습니다.");
                    }
                    else
                    {
                        Console.WriteLine($"에러 발생");
                    }
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"ExecuteLoanRepaymentCheckAsync에서 오류 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
            

        }

        public async Task AnnounceLottoResultAsync()
        {
            var channel = _client.GetChannel(ConfigManager.Config.LottoChannelId) as IMessageChannel;

            _lottoManager.GenerateWinningNumbers();
            var winningNumbers = _lottoManager.GetWinningNumbers();
            var (firstPrizeWinners, secondPrizeWinners, thirdPrizeWinners) = await _lottoManager.CheckWinners(channel);          

            if (channel != null)
            {
                var embedBuilder = new EmbedBuilder()
                    .WithTitle("이번 주 로또 결과 발표!")
                    .WithColor(new Color(255, 145, 200))
                    .WithDescription($"당첨 번호: {string.Join(", ", winningNumbers)}")
                    .WithTimestamp(DateTimeOffset.Now);

                ulong CalculatePrizeAmount(ulong totalAmount, int winnerCount)
                {
                    double percentage = winnerCount switch
                    {
                        1 => 1.0,   // 1명이면 100%
                        2 => 0.7,   // 2명이면 70%
                        3 => 0.6,   // 3명이면 60%
                        _ => 0.5    // 4명 이상이면 50%
                    };
                    return (ulong)(totalAmount * percentage);
                }

                ulong firstPrizeTotal = 1000000;
                ulong firstPrizeAmount = CalculatePrizeAmount(firstPrizeTotal, firstPrizeWinners.Count);

                if (firstPrizeWinners.Count > 0)
                {
                    var firstPrizeMentions = new List<string>();

                    foreach (var winnerId in firstPrizeWinners)
                    {
                        await _dbManager.AddDdingAsync(winnerId, firstPrizeAmount);
                        var user = _client.GetUser(winnerId);
                        firstPrizeMentions.Add(user != null ? user.Mention : $"<@{winnerId}>");
                    }

                    embedBuilder.AddField("1등 당첨자", string.Join("\n", firstPrizeMentions));
                    embedBuilder.AddField("1등 상금", $"당첨자들에게 {firstPrizeAmount:N0}:mushroom:이 지급되었습니다.");
                }
                else
                {
                    embedBuilder.AddField("1등 당첨자 없음", "다음 기회를 노려보세요!");
                }

                ulong secondPrizeTotal = 200000;
                ulong secondPrizeAmount = CalculatePrizeAmount(secondPrizeTotal, secondPrizeWinners.Count);


                if (secondPrizeWinners.Count > 0)
                {
                    var secondPrizeMentions = new List<string>();

                    foreach (var winnerId in secondPrizeWinners)
                    {
                        await _dbManager.AddDdingAsync(winnerId, secondPrizeAmount);
                        var user = _client.GetUser(winnerId);
                        secondPrizeMentions.Add(user != null ? user.Mention : $"<@{winnerId}>");
                    }

                    embedBuilder.AddField("2등 당첨자", string.Join("\n", secondPrizeMentions));
                    embedBuilder.AddField("2등 상금", $"당첨자들에게 {secondPrizeAmount:N0}:mushroom:이 지급되었습니다.");
                }
                else
                {
                    embedBuilder.AddField("2등 당첨자 없음", "다음 기회를 노려보세요!");
                }

                ulong thirdPrizeAmount = 25000;

                if (thirdPrizeWinners.Count > 0)
                {
                    var thirdPrizeMentions = new List<string>();

                foreach (var winnerId in thirdPrizeWinners)
                {
                    await _dbManager.AddDdingAsync(winnerId, thirdPrizeAmount);
                    var user = _client.GetUser(winnerId);
                    thirdPrizeMentions.Add(user != null ? user.Mention : $"<@{winnerId}>");
                }

                embedBuilder.AddField("3등 당첨자", string.Join("\n", thirdPrizeMentions));
                embedBuilder.AddField("3등 상금", $"당첨자들에게 {thirdPrizeAmount:N0}:mushroom:이 지급되었습니다.");
                }
                else
                {
                    embedBuilder.AddField("3등 당첨자 없음", "다음 기회를 노려보세요!");
                }

                // 저번주 로또 결과 발표 고정 메시지에서 삭제
                var pinnedMessages = await channel.GetPinnedMessagesAsync();
                var botPinnedMessage = pinnedMessages.FirstOrDefault(m => m.Author.Id == _client.CurrentUser.Id);

                if (botPinnedMessage != null)
                {
                    var messageEmbed = botPinnedMessage.Embeds.FirstOrDefault();
                    if (messageEmbed != null && (messageEmbed.Title == "이번 주 로또 결과 발표!"))
                    {
                        await botPinnedMessage.DeleteAsync();
                    }
                }

                await SaveLottoResultsToFileAsync(winningNumbers, firstPrizeWinners, secondPrizeWinners, thirdPrizeWinners);               

                // 임베드 발송 및 메시지 고정
                var sentMessage = await channel.SendMessageAsync(embed: embedBuilder.Build());
                await sentMessage.PinAsync();

                // 고정된 메시지 알림을 삭제
                var newMessages = await channel.GetMessagesAsync(10).FlattenAsync();
                var pinNotification = newMessages.FirstOrDefault(m => m.Type == MessageType.ChannelPinnedMessage);

                if (pinNotification != null)
                {
                    await pinNotification.DeleteAsync();
                }
            }
            else
            {
                Console.WriteLine("channel이 null값이었습니다.");
            }
        }

        public static async Task SaveLottoResultsToFileAsync(List<int> winningNumbers, List<ulong> firstPrizeWinners, List<ulong> secondPrizeWinners, List<ulong> thirdPrizeWinners)
        {
            var lottoResults = new
            {
                WinningNumbers = winningNumbers,
                FirstPrizeWinners = firstPrizeWinners,
                SecondPrizeWinners = secondPrizeWinners,
                ThirdPrizeWinners = thirdPrizeWinners,
                Timestamp = DateTime.Now
            };

            string filePath = Path.Combine("jsonFiles", "lotto_results.json");

            var json = JsonConvert.SerializeObject(lottoResults, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json);          
        }
        private async Task InitializeAudioFilesAsync()
        {
            var audioFiles = await _dbManager.LoadAudioFilesAsync();

            foreach (var kvp in audioFiles)
            {
                if (!AudioManager.AudioFiles.ContainsKey(kvp.Key))
                {
                    AudioManager.AudioFiles.Add(kvp.Key, kvp.Value);
                }
            }

            Console.WriteLine("Audio files have been successfully loaded from the database.");
        }

        private async Task InitializeLottoAsync()
        {
            await _dbManager.LoadLottoTicketsAsync();
        }        
        private async Task InitializeRouletteAsync()
        {
            await Task.Run(() =>
            {
                RouletteManager.LoadDailyRouletteUsers();
            });
        }

        private async Task InitializePundingAsync()
        {
            await Task.Run(() =>
            {
                SlotMachineManager.LoadPundingUsers();
            });
        }
       
        private async Task InitializeLoanDataAsync()
        {
            await Task.Run(() =>
            {
                _interactionManager.LoadFromJson();
            });
        }

        private async Task InitializeLoadGmailAsync()
        {
            await Task.Run(() =>
            {
                _gmailManager.LoadLastEmailId();
            });
        }
    }
}