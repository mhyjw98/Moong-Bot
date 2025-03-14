using AngleSharp.Text;
using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using Google.Apis.Gmail.v1.Data;
using Microsoft.Extensions.DependencyInjection;
using MoongBot.Core.Manager;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MoongBot.Core.Commands
{
    [Name("Bot")]
    public class BotCommands : ModuleBase<SocketCommandContext>
    {
        private static CoinMarketManager _coinManager = new CoinMarketManager();
        private static DatabaseManager _dbManager = new DatabaseManager();
        private static SlotMachineManager _slotManager = new SlotMachineManager();
        private static RouletteManager _rouletManager = new RouletteManager();
        private static ShopManager _shopManager = new ShopManager();

        public static ulong? _lastRegisteringUserId;
        public static string _currentWord = "";
        public static bool isRegister;
        public static bool isSpecialRegister;
        public static int _currentVolume = 70;

        private readonly ulong lottochannel = ConfigManager.Config.LottoChannelId;
        private readonly ulong coinchannel = ConfigManager.Config.CoinChannelId;
        private readonly ulong bushchannel = ConfigManager.Config.BushChannelId;


        [Command("뭉")]
        [Remarks("TTS를 재생하는 한글 명령어입니다. 한/영 전환 없이 한글로 간편하게 사용할 수 있습니다. 사용법 : 뭉ㅇ [할 말]")]
        public async Task KrCommand()
        {
            await AudioManager.PlayAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel, Context.Message.Content, ConfigManager.Config.TtsPrefix.Length + 1);
        }

        [Command("채널")]
        [Remarks("음성채널 입장후 간편 TTS 기능을 활성화(-간편활성) 한 후 봇이 있는 음성채널 채팅방에 채팅을 치면 TTS 재생을 해줍니다.")]
        public async Task KrChCommand()
        {
            await AudioManager.PlayAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel, Context.Message.Content, 0);

        }
        [Command("tts")]
        [Remarks("TTS 명령어입니다. 사용법 : -tts [할 말]")]
        public async Task PlayCommand([Remainder] string text)
        {
            await AudioManager.PlayAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel, text);
        }

        [Command("간편활성")]
        [Alias("등록뭉")]
        [Remarks($"간편 TTS 기능을 활성화합니다. 음성 채널에서 이 명령어를 사용하면 뭉ㅇ을 사용하지않아도 TTS 기능을 사용할 수 있고, 참여, 퇴장, 정지 명령어를 접두사 없이 간편하게 사용할 수 있어요.")]
        public async Task EnableSimpleTtsCommand()
        {
            var channel = Context.Channel;
            var user = Context.User;

            if (!(channel is IVoiceChannel))
            {
                await channel.SendMessageAsync($"{user.Mention}, 이 명령어는 음성 채널의 텍스트 채널에서만 사용할 수 있습니다.");
                return;
            }

            if (!Bot.SimpleTtsUsers.Contains(user.Id))
            {
                Bot.SimpleTtsUsers.Add(user.Id);
                await channel.SendMessageAsync($"{user.Mention}, 간편 TTS 기능이 활성화되었습니다. 이제부터 봇이 있는 채널에서 입력하는 메시지가 TTS로 재생됩니다.");
            }
            else
            {
                await channel.SendMessageAsync($"{user.Mention}, 간편 TTS 기능이 이미 활성화되어 있습니다.");
            }
        }

        [Command("간편해제")]
        [Alias("해제뭉")]
        [Remarks("간편 TTS 기능을 비활성화합니다.")]
        public async Task DisableSimpleTtsCommand()
        {
            var user = Context.User;

            if (Bot.SimpleTtsUsers.Contains(user.Id))
            {
                Bot.SimpleTtsUsers.Remove(user.Id);
                await ReplyAsync($"{user.Mention}, 간편 TTS 기능이 비활성화되었습니다.");
            }
            else
            {
                await ReplyAsync($"{user.Mention}, 간편 TTS 기능이 활성화되어 있지 않습니다.");
            }
        }

        [Command("입장")]
        [Alias("참여")]
        [Remarks("봇을 해당 음성채팅방에 입장시킬 수 있습니다")]
        public async Task JoinCommand()
        {
            await AudioManager.JoinAsync(Context.Guild, Context.User as IVoiceState,
                Context.Channel as ITextChannel);
        }

        [Command("퇴장")]
        [Alias("나가")]
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


        [Command("weather")]
        [Alias("날씨")]
        [Remarks("날씨 정보를 알려줍니다. 명령어 : -날씨 [도시명]")]
        public async Task WeatherCommand([Remainder] string city = null)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                await Context.Channel.SendMessageAsync($"도시명을 입력해주세요. Ex : {ConfigManager.Config.Prefix}날씨 인천");
                return;
            }

            await WeatherManager.WeatherAsync(Context.Channel as ITextChannel, city);
        }


        [Command("roulette")]
        [Alias("룰렛")]
        [Remarks("룰렛을 돌려 확률에 따라 보상을 받을 수 있어요")]
        public async Task RouletteCommand()
        {
            await RouletteCommand(Context.User, Context.Channel);
        }

        public async Task<bool> RouletteCommand(SocketUser user, IMessageChannel channel)
        {
            if (channel.Id != lottochannel && channel.Id != coinchannel)
            {
                var guildChannel = channel as SocketGuildChannel;
                var lottoChannel = guildChannel.Guild.GetChannel(lottochannel);
                var coinChannel = guildChannel.Guild.GetChannel(coinchannel);
                await channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널이나 \"{coinChannel.Name}\"에서 이용해주세요!");
                return false;
            }
          
            var (result, isSuccess)= await _rouletManager.SpinAsync(user.Id);
            await channel.SendMessageAsync($"{user.Mention}" + result);
            return isSuccess;
        }

        [Command("자동")]
        [Remarks("로또 티켓을 자동으로 구매합니다. 명령어 : -자동 [1 ~ 10의 숫자], 입력한 숫자(1 ~ 10)만큼의 로또를 자동으로 구매합니다.")]
        public async Task LottoAutoCommand([Remainder] string input = "1")
        {
            await LottoAutoCommand(Context.User, Context.Channel, input);
        }

        public async Task<bool> LottoAutoCommand(SocketUser user, IMessageChannel channel, string input = "1")
        {
            if (channel.Id != lottochannel)
            {
                var guildChannel = channel as SocketGuildChannel;
                var lottoChannel = guildChannel.Guild.GetChannel(lottochannel);
                await channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널에서 이용해주세요!");
                return false;
            }

            int maxValue = LottoManager.maxLotto;

            if (!int.TryParse(input, out int num) || num < 1 || num > maxValue)
            {
                await channel.SendMessageAsync($"1에서 {maxValue} 사이의 숫자를 입력해주세요.");
                return false;
            }

            var userId = user.Id;
            return await LottoManager.BuyTicketAsync(userId, num, channel as ITextChannel);
        }

        [Command("내로또")]
        [Remarks("지난 회차의 정보와 내가 구매한 로또를 보여줍니다")]
        public async Task ShowLottoCommand()
        {
            await ShowLottoCommand(Context.User, Context.Channel);
        }

        public async Task ShowLottoCommand(SocketUser user, IMessageChannel channel)
        {
            if (channel.Id != lottochannel)
            {
                var guildChannel = channel as SocketGuildChannel;
                var lottoChannel = guildChannel.Guild.GetChannel(lottochannel) as SocketGuildChannel;
                await channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널에서 이용해주세요!");
                return;
            }

            await LottoManager.ShowLottoTicketsAsync(user.Id, channel as ITextChannel, user as SocketGuildUser);
        }

        [Command("스피또")]
        [Alias("복권")]
        [Remarks("스피또(복권)를 구매합니다. 명령어 : -스피또 [1 ~ 5의 숫자] or -복권 [1 ~ 5의 숫자], 입력한 숫자(1 ~ 5)만큼의 스피또를 구매합니다.")]
        public async Task SpitoCommand([Remainder] string input = "1")
        {
            if (!int.TryParse(input, out int number) || number > 5 || number < 1)
            {
                await ReplyAsync($"1 ~ 5 사이의 숫자를 입력해주세요. 예시 : {ConfigManager.Config.Prefix}스피또 5");
                return;
            }

            await LottoManager.BuySpitoAsync(Context.User.Id, number, Context.Channel as ITextChannel);
        }

        [Command("등록")]
        [Alias("register")]
        [Remarks("단어를 등록해서 mp3 파일을 저장합니다. 명령어 : -등록 [명령어] [1 ~ 100사이 볼륨 값(선택)], -등록 명령어 사용 후 mp3 파일을 올리면 내가 설정한 명령어와 볼륨으로 mp3 파일이 등록됩니다. 볼륨을 입력하지 않을시 기본값인 70으로 등록됩니다.")]
        public async Task PlayAudioCommand([Remainder] string input = null)
        {
            if (!(Context.Channel is IVoiceChannel))
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention}, 이 명령어는 음성 채널의 텍스트 채널에서만 사용할 수 있습니다.");
                return;
            }
            if (string.IsNullOrWhiteSpace(input))
            {               
                await ReplyAsync($"등록할 단어와 볼륨을 입력해주세요. 볼륨은 적지 않으면 70으로 설정됩니다. 예시: `{ConfigManager.Config.Prefix}등록 단어 볼륨(0 ~ 100)`");
                return;
            }

            var parts = input.Split(' ');
            int volume = 70;
            string word = "";

            if (parts.Length > 1 && int.TryParse(parts[^1], out var parsedVolume))
            {
                volume = Math.Clamp(parsedVolume, 0, 100);
                word = string.Join(' ', parts.Take(parts.Length - 1));
            }
            else
            {              
                word = input;
            }
            bool isProtected = await _dbManager.IsWordOwnedByAdmin(word);

            if (isProtected && Context.User.Id != ConfigManager.Config.OwnerId)
            {
                await ReplyAsync("이 단어는 관리자가 등록해서 수정이 불가능합니다. 다른 단어로 등록해주세요.");
                return;
            }

            _lastRegisteringUserId = Context.User.Id;
            _currentWord = word;
            _currentVolume = volume;
            isRegister = true;
            await ReplyAsync($"명령어와 mp3 파일을 등록하면 명령어를 말했을때 봇이 mp3 파일을 재생해줍니다. 명령어는 자주 사용할 것 같은 단어는 피해주세요. 명령어의 무분별한 사용으로 다른 유저들에게 피해를 주지 말아주세요! 너무 재생시간이 긴 파일이나 노래는 지양해주세요. 노래는 가급적 노래봇을 이용해주세요!!");
            await ReplyAsync("등록할 mp3 파일을 업로드해 주세요.");
        }       

        [Command("삭제")]
        [Alias("delete")]
        [Remarks("등록된 단어와 mp3 파일을 삭제합니다. 명령어 : -삭제 [명령어]")]
        public async Task DeleteAudioCommand([Remainder] string word)
        {
            if (!(Context.Channel is IVoiceChannel))
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention}, 이 명령어는 음성 채널의 텍스트 채널에서만 사용할 수 있습니다.");
                return;
            }

            if (string.IsNullOrWhiteSpace(word))
            {
                await ReplyAsync("삭제할 단어를 입력해주세요.");
                return;
            }

            await _dbManager.DeleteAudioFileAsync(word, Context.Channel as ITextChannel, Context.User.Id);
        }

        [Command("단어목록")]
        [Alias("listwords")]
        [Remarks("등록된 mp3 재생 명령어를 모두 보여줍니다.")]
        public async Task ListWordsCommand()
        {
            await ListWordsCommand(Context.User, Context.Channel);
        }

        public async Task ListWordsCommand(SocketUser user, IMessageChannel channel)
        {
            var (regularWords, adminWords) = await _dbManager.GetAllWordsAsync();

            if (regularWords.Count == 0 && adminWords.Count == 0)
            {
                await channel.SendMessageAsync($"<@{user.Id}>등록된 단어가 없습니다.");
                return;
            }

            var embedBuilder = new EmbedBuilder()
            {
                Title = "등록된 명령어 목록",
                Description = "등록된 명령어를 사용하면 봇이 매칭되는 mp3 파일을 재생합니다. 관리자 명령어는 일반 유저는 사용, 수정이 불가합니다.",
                Color = new Color(255, 145, 200)
            };

            if (regularWords.Count > 0)
            {
                embedBuilder.AddField("일반 명령어", string.Join(", ", regularWords), false);
            }

            if (adminWords.Count > 0)
            {
                embedBuilder.AddField("관리자 명령어", string.Join(", ", adminWords), false);
            }

            await channel.SendMessageAsync($"<@{user.Id}>", embed: embedBuilder.Build());
        }

        [Command("잔액")]
        [Remarks("보유한 재산을 보여드립니다.")]
        public async Task GetbalanceCommand()
        {
            await GetbalanceCommand(Context.User, Context.Channel);
        }

        public async Task GetbalanceCommand(SocketUser user, IMessageChannel channel)
        {
            bool isCoinRepay; 
            if (channel.Id == lottochannel)
            {
                isCoinRepay = true;
            }
            else if(channel.Id == coinchannel)
            {
                isCoinRepay = false;
            }
            else
            {
                await channel.SendMessageAsync("슬롯, 코인 채널에서 이용해주세요.");
                return;
            }

            var (ddingBalance, coinBalance , dollarBalance) = await _dbManager.GetAllBalanceAsync(user.Id);

            int userTicket = await _dbManager.GetTicketValueAsync(user.Id);
            int userSpecial = await _dbManager.GetSpecialValueAsync(user.Id);

            var (loanAmount, interest, isCoin, date) = await _dbManager.GetTotalRepaymentAmountAsync(user.Id, isCoinRepay);

            var guildUser = user as IGuildUser;
            string userNickname = guildUser?.Nickname ?? user.Username;

            var embedBuilder = new EmbedBuilder()
            {
                Title = $"{userNickname} 님의 잔액",
                Description = "룰렛으로 얻은 :dollar:로 로또를 구매할 수 있습니다.\n로또를 통해 얻은 :mushroom:으로 상품을 구매할 수 있습니다.\n",
                Color = new Color(255, 145, 200)
            };
            
            embedBuilder.AddField("보유한 금액", $"달러 : {dollarBalance:N0} :dollar:\n코인 : {coinBalance:N0} :coin:\n띵 : {ddingBalance:N0} :mushroom:\n\n", false);

            embedBuilder.AddField("보유한 슬롯머신 아이템", $"도박슬롯 티켓 : {userTicket:N0} :ticket:\n확률증가권(버섯제거) : {userSpecial:N0} :tickets:\n\n", false);

            if (loanAmount > 0)
            {
                string coinOrDollar = isCoin ? "코인" : "달러";
                string coinOrDollarSymbol = isCoin ? ":coin:" : ":dollar:";
                embedBuilder.AddField($"{coinOrDollar} 대출 현황", $"원금 : {loanAmount:N0} {coinOrDollarSymbol} \n이자 : {interest:N0} {coinOrDollarSymbol}\n상환 날짜 : {date.ToString("MM월 dd일 HH: mm")}", false);
            }

            await channel.SendMessageAsync($"<@{user.Id}>", embed: embedBuilder.Build());
        }

        [Command("상점")]
        [Remarks("상점을 열어 🍄로 구매 가능한 상품을 보여줍니다.")]
        public async Task OpenShopCommand()
        {
            await OpenShopCommand(Context.Channel);
        }
        public async Task OpenShopCommand(IMessageChannel channel)
        {
            if (channel.Id != lottochannel)
            {
                return;
            }

            var items = _shopManager.GetItems();

            var embedBuilder = new EmbedBuilder()
            {
                Title = "버섯 상점 :mushroom:",
                Description = "아래의 상품을 버튼을 눌러 구매하세요.",
                Color = new Color(255, 145, 200)
            };

            var buttons = new ComponentBuilder();
            foreach (var item in items)
            {
                string formattedPrice = item.Price.ToString("N0");
                string stockInfo = item.Stock > 0 ? $"재고: {item.Stock}개 남음" : "재고 없음";

                // 무제한인 경우는 재고를 표시하지 않음
                if (item.Stock == -1)
                {
                    embedBuilder.AddField($"{item.Name} :gift:", $"{item.Description}\n가격: {formattedPrice} :mushroom:\n재고: 무제한", false);
                    buttons.WithButton(item.Name, item.Name.ToLower() + "_btn", ButtonStyle.Primary); // 항상 버튼을 추가
                }
                else
                {
                    // 남은 재고가 있을 경우에만 버튼을 추가하고, 재고가 없으면 버튼을 생성하지 않음
                    embedBuilder.AddField($"{item.Name} :gift:", $"{item.Description}\n가격: {formattedPrice} :mushroom:\n{stockInfo}", false);

                    if (item.Stock > 0)
                    {                     
                        buttons.WithButton(item.Name, item.Name.ToLower() + "_btn", ButtonStyle.Primary); // 버튼 생성
                    }
                }
            }

            await channel.SendMessageAsync(embed: embedBuilder.Build(), components: buttons.Build());           
        }

        [Command("지원금")]
        [Remarks("처음 한 번 1000 코인, 5000 달러의 지원금을 받을 수 있습니다.")]
        public async Task PundingCommand()
        {
            await PundingCommand(Context.Channel, Context.User);
        }

        public async Task PundingCommand(IMessageChannel channel, SocketUser user)
        {
            await _slotManager.GivePundingForUser(channel, user.Id);
        }

        [Command("랭킹")]
        [Remarks("코인 채널에서 사용하면 코인 사용자의 랭킹을, 슬롯머신 채널에서 사용하면 슬롯머신 사용자의 랭킹을 출력합니다.")]
        public async Task RankingCommand()
        {
            await RankingCommand(Context.Channel, Context.Guild);
        }

        public async Task RankingCommand(IMessageChannel channel, SocketGuild guild)
        {
            ulong channelId = channel.Id;

            if (channelId == lottochannel)
            {                
                var report = await _slotManager.GenerateRankingReportAsync(1);
                int totalAmount = await _dbManager.GetTotalAmountAsync();

                var embedBuilder = new EmbedBuilder()
                {
                    Title = "슬롯머신 랭킹",
                    Description = "슬롯머신 사용자의 랭킹입니다.",
                    Color = new Color(255, 145, 200)
                };

                embedBuilder.AddField("슬롯머신 총 사용량", totalAmount.ToString("N0"), false);
                embedBuilder.AddField("사용자 랭킹", report.Rankings, false);

                // 전체 페이지 수 계산
                int totalPages = (int)Math.Ceiling(report.TotalUsers / 10.0);

                // 버튼 추가
                if (totalPages > 1)
                {
                    var buttons = new ComponentBuilder()
                                    .WithButton("이전 페이지", $"rank_prev_{totalPages}", ButtonStyle.Primary)
                                    .WithButton("다음 페이지", $"rank_next_{2}", ButtonStyle.Primary);

                    await channel.SendMessageAsync(embed: embedBuilder.Build(), components: buttons.Build());
                }
                else
                {
                    await channel.SendMessageAsync(embed: embedBuilder.Build());
                }
            }
            else if (channelId == coinchannel)
            {
                var (rankings, totalUsers) = await _coinManager.GetProfitRankingEmbedAsync(guild, 1);

                var embedBuilder = new EmbedBuilder()
                {
                    Title = "호롤로 코인왕",
                    Color = Color.Green,
                    Description = "코인 거래로 인한 차익이 기록된 랭킹입니다."
                };

                embedBuilder.AddField("사용자 랭킹", rankings, false);

                // 전체 페이지 수 계산
                int totalPages = (int)Math.Ceiling(totalUsers / 10.0);

                // 버튼 추가
                if (totalPages > 1)
                {
                    var buttons = new ComponentBuilder()
                        .WithButton("이전 페이지", $"rankc_prev_{totalPages}", ButtonStyle.Primary)
                        .WithButton("다음 페이지", $"rankc_next_{2}", ButtonStyle.Primary);

                    await channel.SendMessageAsync(embed: embedBuilder.Build(), components: buttons.Build());
                }
                else
                {
                    await channel.SendMessageAsync(embed: embedBuilder.Build());
                }
            }
            else
            {
                await channel.SendMessageAsync("슬롯, 코인 채널에서 이용해주세요.");
            }
        }

        [Command("명예의전당")]
        [Remarks("슬롯머신 랭킹 1위들이 기록되어 있습니다.")]
        public async Task HOFCommand()
        {
            await HOFCommand(Context.Channel, Context.Guild);
        }

        public async Task HOFCommand(IMessageChannel channel, SocketGuild guild)
        {
            ulong channelId = channel.Id;

            if (channelId == lottochannel)
            {
                var report = await _slotManager.GenerateHOFReportAsync(guild, 1);
                var embedBuilder = new EmbedBuilder()
                {
                    Title = "슬롯머신 명예의 전당",
                    Color = Color.Gold
                };

                embedBuilder.AddField("슬롯머신 1위 사용자", report.Rankings, false);

                // 전체 페이지 수 계산
                int totalPages = (int)Math.Ceiling(report.TotalUsers / 10.0);

                // 버튼 추가
                if (totalPages > 1)
                {
                    var buttons = new ComponentBuilder()
                                    .WithButton("이전 페이지", $"rank_prev_{totalPages}", ButtonStyle.Primary)
                                    .WithButton("다음 페이지", $"rank_next_{2}", ButtonStyle.Primary);

                    await channel.SendMessageAsync(embed: embedBuilder.Build(), components: buttons.Build());
                }
                else
                {
                    await channel.SendMessageAsync(embed: embedBuilder.Build());
                }
            }
            else
            {
                await channel.SendMessageAsync("슬롯, 코인 채널에서 이용해주세요.");
            }
        }

        [Command("슬롯정지")]
        [Remarks("슬롯머신의 동작을 멈출 수 있습니다. 이미 동작하고 있는걸 멈출수는 없지만 동작 횟수가 남아있을 경우 멈출 수 있습니다.")]
        public async Task SlotStopCommand()
        {
            await SlotStopCommand(Context.Channel, Context.User);
        }

        public async Task SlotStopCommand(IMessageChannel channel, SocketUser user)
        {           
            if(SlotMachineManager._isPlaying.Count > 0)
            {
                SlotMachineManager.isStop = true;
                await channel.SendMessageAsync($"{user.Mention} 슬롯머신을 정지합니다!");
            }
            else
            {
                await channel.SendMessageAsync($"{user.Mention} 슬롯머신이 동작하고 있지 않습니다");
            }
        }

        // 여기부터 검수
        [Command("코인종목")]
        [Alias("종목")]
        [Remarks("현재 거래가능한 코인의 목록과 가격 변동값을 보여줍니다.")]
        public async Task CoinListCommand()
        {
            await CoinListCommand(Context.Channel);
        }

        public async Task CoinListCommand(IMessageChannel channel)
        {
            if (channel.Id != coinchannel)
            {
                return;
            }

            var (embed, component) = await _coinManager.GetCoinMarketStatusEmbedAsync();
            await channel.SendMessageAsync(embed : embed, components : component);
        }

        [Command("매수")]
        [Remarks("입력한 금액으로 살 수 있는 최대한의 코인 수량을 구매합니다 2%의 수수료가 발생합니다. 명령어 : -매수 [코인이름] [금액], 금액에는 소숫점 두 자리까지의 소수를 입력하거나 * 을 입력해주세요. *을 입력하면 가지고있는 모든 금액으로 코인을 매수합니다.")]
        public async Task BuyCoinCommand(string coinName, string amountInput)
        {
            string result;

            if (Context.Channel.Id != coinchannel)
            {
                return;
            }
            if(!double.TryParse(amountInput, out double amount) && !amountInput.Equals("*"))
            {
                await ReplyAsync("잘못된 금액을 입력하셨습니다. 예시: -매수 [코인이름] [금액]");
                return;
            }
            else if (amountInput.Equals("*"))
            {
                double userDollar = await _dbManager.GetUserDollarAsync(Context.User.Id);
                result = await _coinManager.BuyCoinAsync(Context.User.Id, coinName, userDollar);
            }
            else
            {
                result = await _coinManager.BuyCoinAsync(Context.User.Id, coinName, amount);
            }
            

            await ReplyAsync(result);
        }

        [Command("매도")]
        [Remarks("보유한 코인을 입력한 수량만큼 판매합니다. 명령어 : -매도 [코인이름] [수량], 수량에는 소숫점 두 자리까지의 소수나 *을 입력해주세요 *을 입력할 경우 보유한 코인을 전부 매도합니다.")]
        public async Task SellCoinCommand(string coinName, string quantityInput)
        {
            if (Context.Channel.Id != coinchannel)
            {
                return;
            }
            if (!double.TryParse(quantityInput, out double amount) && !quantityInput.Equals("*"))
            {
                await ReplyAsync("잘못된 수량을 입력하셨습니다. 예시: -매도 [코인이름] [수량]");
                return;
            }

            string result = await _coinManager.SellCoinAsync(Context.User.Id, coinName, quantityInput);

            await ReplyAsync(result);
        }

        [Command("차트")]
        [Remarks("코인의 차트를 보여줍니다. 명령어 : -차트 [코인이름] [1 ~ 7의 숫자(출력할 차트의 기간)]")]
        public async Task CoinChartCommand(string coin, string period = "1")
        {
            await CoinChartCommand(Context.Channel, coin, period);
        }

        public async Task CoinChartCommand(IMessageChannel channel, string coin, string period)
        {
            if (channel.Id != coinchannel)
            {
                return;
            }
            if(!int.TryParse(period, out int day))
            {
                if (period.Equals("*"))
                {
                    day = 7;
                }
                else
                {
                    await channel.SendMessageAsync("차트 출력 기간은 1 ~ 7 사이의 숫자를 입력해주세요.");
                    return;
                }             
            }
            else
            {
                if(day < 1 || day > 7)
                {
                    await channel.SendMessageAsync("차트 출력 기간은 1 ~ 7 사이의 숫자를 입력해주세요.");
                    return;
                }
            }

            var (path, result) =  await _coinManager.SendCoinPriceChartAsync(channel, coin, day);

            if (!path.Equals(""))
            {
                await channel.SendFileAsync(path, result);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            else
            {
                await channel.SendMessageAsync(result);
            }
        }

        [Command("포트폴리오")]
        [Alias("포폴")]
        [Remarks("사용자가 보유한 잔액과 코인의 정보를 담은 포트폴리오를 보여줍니다.")]
        public async Task PortfolioCommand()
        {
            await PortfolioCommand(Context.User, Context.Channel);
        }

        public async Task PortfolioCommand(SocketUser user, IMessageChannel channel)
        {
            if(channel.Id != coinchannel)
            {
                return;
            }

            var guildUser = user as IGuildUser;
            string userNickname = guildUser?.Nickname ?? user.Username;

            var (embed, component) = await _coinManager.GetUserCoinHoldingsEmbedAsync(user.Id, userNickname);

            await channel.SendMessageAsync(embed: embed, components: component);
        }

        [Command("자동매매")]
        [Remarks("자동으로 코인이 설정된 금액에 도달하면 매매합니다. 사용법: -자동매매 [코인 이름] [금액] [수량] [매수 or 매도]")]
        public async Task SetAutoTradeCommand(string coinName, string targetPriceInput, string quantityInput, string action)
        {
            if (!double.TryParse(targetPriceInput, out double targetPrice) || targetPrice <= 0)
            {
                await ReplyAsync($"{Context.User.Mention} 목표 금액은 0보다 큰 숫자를 입력해주세요.");
                return;
            }
            if (!double.TryParse(quantityInput, out double quantity) || quantity <= 0)
            {
                if (!quantityInput.Equals("*"))
                {
                    await ReplyAsync($"{Context.User.Mention} 수량은 0보다 큰 숫자를 입력해주세요.");
                    return;
                }               
            }
            if (!action.Equals("매수", StringComparison.OrdinalIgnoreCase) && !action.Equals("매도", StringComparison.OrdinalIgnoreCase))
            {
                await ReplyAsync($"{Context.User.Mention} 매매타입은 매수 or 매도를 입력해주세요.");
                return;
            }
            await SetAutoTradeCommand(Context.User.Id, coinName, targetPrice, quantityInput, action, Context.Channel as ITextChannel);
        }

        public async Task<bool> SetAutoTradeCommand(ulong userId ,string coinName, double targetPrice, string quantityInput, string action, ITextChannel channel)
        {
            bool isBuying = action.Equals("매수", StringComparison.OrdinalIgnoreCase);

            if(!double.TryParse(quantityInput, out double quantity) && quantityInput.Equals("*"))
            {
                if (isBuying)
                {
                    double userDollar = await _dbManager.GetUserDollarAsync(userId);

                    // 수수료 반영 후 실제 구매 가능한 금액 계산
                    quantity = Math.Floor((userDollar / (targetPrice * (1 + CoinMarketManager.TransactionFeeRate))) * 100) / 100;
                }
                else
                {
                    var(searchCoinName, coinId) = await _dbManager.GetCoinIdByNameAsync(coinName);
                    var (searchCoinId, totalQuantity, averagePrice) = await _dbManager.GetUserCoinHoldingsForSpecificCoinAsync(userId, coinId);

                    quantity = totalQuantity;
                }
            }

            var (isSuccess, result) = await _coinManager.SetAutoTradeAsync(userId, coinName, targetPrice, quantity, isBuying);

            await channel.SendMessageAsync($"<@{userId}>" + result);
            return true;
        }

        [Command("자동매매현황")]
        [Remarks("사용자가 설정해둔 자동매매 기록을 보여줍니다.")]
        public async Task ShowAutoTradeCommand()
        {
            await ShowAutoTradeCommand(Context.User.Id, Context.User, Context.Channel as ITextChannel);
        }
        public async Task ShowAutoTradeCommand(ulong userId, SocketUser user, ITextChannel channel)
        {
            var(isSuccess, autoTrades) = await _dbManager.GetAllAutoTradeConditionsAsync(userId);

            if (!isSuccess)
            {
                await channel.SendMessageAsync(user.Mention + autoTrades.FirstOrDefault() ?? "자동 매매 조건을 가져오지 못했습니다.");
                return;
            }

            var guildUser = user as IGuildUser;
            string userNickname = guildUser?.Nickname ?? user.Username;

            var embedBuilder = new EmbedBuilder()
            {
                Title = $"{userNickname} 님의 자동매매 설정기록",
                Color = Color.Blue
            };

            foreach (var condition in autoTrades)
            {
                string[] parts = condition.Split(',');

                if (parts.Length == 4)
                {
                    string coinName = parts[0].Trim();
                    string targetPrice = parts[1].Replace("목표 가격: ", "").Trim();

                    var (fcoinName, coinId) = await _dbManager.GetCoinIdByNameAsync(coinName);
                    double currentPrice = await _dbManager.GetCoinCurrentPriceAsync(coinId);

                    string quantity = parts[2].Replace("수량: ", "").Trim();
                    string tradeType = parts[3].Replace("매매 타입: ", "").Trim();

                    embedBuilder.AddField($"{coinName}", $"목표 금액 : {targetPrice:N2} :dollar:\n현재 금액 : {currentPrice.ToString("N2")} :dollar:\n매매 수량 : {quantity}\n매매 타입 : {tradeType}", inline: false);
                }
            }

            await channel.SendMessageAsync(embed: embedBuilder.Build());
        }

        [Command("자동매매삭제")]
        [Remarks("사용자가 설정해둔 자동매매 기록을 모두 삭제합니다.")]
        public async Task DeleteAutoTradeCommand()
        {
            await DeleteAutoTradeCommand(Context.User.Id, Context.Channel as ITextChannel);
        }
        public async Task DeleteAutoTradeCommand(ulong userId, ITextChannel channel)
        {
            var(isSuccess, result) = await _dbManager.DeleteAllUserAutoTradeConditionsAsync(userId);

            await channel.SendMessageAsync($"<@{userId}> " + result);
        }       

        [Command("뉴스구독")]
        [Remarks("호롤로 코인 시장의 뉴스 알림을 받을 수 있습니다.")]
        public async Task CoinNewsSubscribeCommand()
        {
            await CoinNewsSubscribeCommand(Context.Channel, Context.User, Context.Guild);
        }

        public async Task CoinNewsSubscribeCommand(IMessageChannel channel, SocketUser user, SocketGuild guild)
        {
            if(channel.Id != coinchannel)
            {
                return;
            }

            bool isSuccess = await _coinManager.SubscribeUser(user.Id);

            if (isSuccess)
            {
                await channel.SendMessageAsync($"{user.Mention} 호롤로 뉴스를 구독하여 코인관련 소식을 알려드립니다! 알림을 받고싶지 않다면 \'{ConfigManager.Config.Prefix}구독취소\' 명령어로 구독을 취소할 수 있습니다!");
            }
            else
            {
                await channel.SendMessageAsync($"{user.Mention} 이미 뉴스를 구독중입니다.");
            }
            
        }

        [Command("구독취소")]
        [Remarks("호롤로 코인 시장의 뉴스 구독을 취소할 수 있습니다. 구독을 취소하면 뉴스 알림이 오지 않습니다.")]
        public async Task CancleSubscribeCommand()
        {
            await CancleSubscribeCommand(Context.Channel, Context.User, Context.Guild);
        }

        public async Task CancleSubscribeCommand(IMessageChannel channel, SocketUser user, SocketGuild guild)
        {
            if (channel.Id != coinchannel)
            {
                return;
            }

            bool isSuccess = await _coinManager.UnsubscribeUser(user.Id);

            if (isSuccess)
            {
                await channel.SendMessageAsync($"{user.Mention} 호롤로 뉴스를 구독을 취소하였습니다! 알림을 다시 받고싶다면 \'{ConfigManager.Config.Prefix}뉴스구독\' 명령어로 다시 구독할 수 있습니다!");
            }
            else
            {
                await channel.SendMessageAsync($"{user.Mention} 호롤로 뉴스를 구독하고있지 않습니다.");
            }                
        }

        [Command("클로버")]
        [Remarks("풀숲 채널에서 사용시 클로버 찾기 메시지를 출력합니다.")]
        public async Task CloverCommand()
        {
            if (Context.Channel.Id != bushchannel)
            {
                await ReplyAsync($"<#{bushchannel}>에서 사용해주세요.");
                return;
            }

            await EventManager.PenaltyNotification(Context.User.Id, false, 0);
        }

        //[Command("테스트")]
        //[Remarks("테스트 코드")]
        //public async Task TestSlotCommand([Remainder] string coin)
        //{
        //    if (Context.User.Id != ConfigManager.Config.OwnerId)
        //    {
        //        return;
        //    }

        //    await _coinManager.GenerateHistoricalDataForCoinAsync(coin);
        //}        

        //[Command("테스트")]
        //[Remarks("테스트 코드")]
        //public async Task TestSlotCommand([Remainder] int input)
        //{
        //    if (Context.User.Id != ConfigManager.Config.OwnerId)
        //    {
        //        return;
        //    }

        //    var slotManager = new SlotMachineManager();

        //    await slotManager.RunSlotMachine(Context.User, Context.Channel as ITextChannel, input, 1);
        //}

        //// 테스트용
        //[Command("테스트")]
        //[Remarks("테스트 코드")]
        //public async Task TestRouletteCommand()
        //{
        //    if (Context.User.Id != ConfigManager.Config.OwnerId)
        //        return;

        //    try
        //    {
        //        await TestManager.RunRouletteTestAsync(Context.Channel as ITextChannel);
        //    }
        //    catch (Exception ex)
        //    {
        //        await ExceptionManager.HandleExceptionAsync(ex);
        //    }
        //}

        //// 테스트용
        //[Command("test")]
        //[Alias("테스트")]
        //public async Task TestLottoCommand()
        //{
        //    if (Context.User.Id != ConfigManager.Config.OwnerId)
        //        return;

        //    await _testManager.TestLottoProcessAsync(Context.Channel as ITextChannel);
        //}
    }
}
