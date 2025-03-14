using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using MoongBot.Core.Manager;
using MoongBot.Core.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace MoongBot.Core.Commands
{
    public class OwnerCommands : ModuleBase<SocketCommandContext>
    {
        private static CoinMarketManager _coinManager = new CoinMarketManager();
        private static DatabaseManager _dbManager = new DatabaseManager();
        private LoanService _loanService = new LoanService();

        [Command("관리자등록")]
        [Remarks("관리자의 mp3 파일 등록 명령어입니다. 일반 유저는 사용 불가합니다.")]
        [Hidden]
        public async Task SpecialRegisterCommand([Remainder] string input = null)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            if (!(Context.Channel is IVoiceChannel))
            {
                await Context.Channel.SendMessageAsync($"{Context.User.Mention}, 이 명령어는 음성 채널의 텍스트 채널에서만 사용할 수 있습니다.");
                return;
            }
            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyAsync($"등록할 단어를 입력해주세요. 예시: `{ConfigManager.Config.Prefix}관리자등록 단어`");
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

            BotCommands._lastRegisteringUserId = Context.User.Id;
            BotCommands._currentWord = word;
            BotCommands._currentVolume = volume;
            BotCommands.isRegister = true;
            BotCommands.isSpecialRegister = true;
            await ReplyAsync("등록할 mp3 파일을 업로드해 주세요.");
        }

        [Command("코인추가")]
        [Remarks("코인을 추가하는 명령어")]
        [Hidden]
        public async Task CoinRegisterCommand(string name, string priceText, string symbol)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyAsync($"등록할 코인을 입력해주세요.");
                return;
            }
            if (!double.TryParse(priceText, out double price))
            {
                await ReplyAsync($"double 타입의 가격을 입력해주세요.");
            }
            if (string.IsNullOrWhiteSpace(symbol))
            {
                await ReplyAsync($"등록할 코인의 심볼을 입력해주세요.");
                return;
            }

            await _dbManager.AddCoinAsync(name, price, symbol);

            await _coinManager.AddCoinEventAsync(name, price);

            await ReplyAsync($"{name}을 {price}으로 등록했습니다. symbol : {symbol}");
        }

        [Command("상장폐지")]
        [Remarks("코인을 삭제하는 명령어")]
        [Hidden]
        public async Task CoinDeleteCommand(string name)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                await ReplyAsync($"삭제할 코인을 입력해주세요.");
                return;
            }           

            await _coinManager.DeleteCoinEventAsync(name);

            await ReplyAsync($"{name}의 상장폐지 이벤트를 시작했습니다.");
        }

        [Command("유저포폴")]
        [Remarks("사용자가 보유한 잔액과 코인의 정보를 담은 포트폴리오를 보여줍니다.")]
        [Hidden]
        public async Task PortfolioCommand(ulong userId)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            var guildUser = Context.Guild.GetUser(userId);
            string userNickname;

            if (guildUser != null)
            {
                userNickname = guildUser?.Nickname ?? guildUser.Username;
            }
            else
            {
                userNickname = "??";
            }

            var (embed, component) = await _coinManager.GetUserCoinHoldingsEmbedAsync(userId, userNickname);

            await Context.Channel.SendMessageAsync(embed: embed, components: component);
        }

        [Command("코인상승")]
        [Remarks("코인가격 상승")]
        [Hidden]
        public async Task CoinSurgeCommand([Remainder] string input = null)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyAsync($"등록할 코인의 이름를 입력해주세요. 예시: `{ConfigManager.Config.Prefix}코인상승 데꾸코인`");
                return;
            }
            var (coinName ,coinId) = await _dbManager.GetCoinIdByNameAsync(input);

            if(coinName != null)
            {
                await _coinManager.CoinPriceSurgeEventAsync(coinId);
                await ReplyAsync($"{input}에 해당하는 코인의 상승 이벤트를 등록했습니다.");
            }
            else
            {
                await ReplyAsync($"{input}에 해당하는 코인 이름을 찾지 못했습니다.");
            }            
        }

        [Command("코인하락")]
        [Remarks("코인 가격 하락")]
        [Hidden]
        public async Task CoinPlungeCommand([Remainder] string input = null)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyAsync($"등록할 코인의 이름을 입력해주세요. 예시: `{ConfigManager.Config.Prefix}코인상승 데꾸코인`");
                return;
            }

            var (coinName, coinId) = await _dbManager.GetCoinIdByNameAsync(input);

            if (coinName != null)
            {
                await _coinManager.CoinPricePlungeEventAsync(coinId);
                await ReplyAsync($"{input}에 해당하는 코인의 하락 이벤트를 등록했습니다.");
            }
            else
            {
                await ReplyAsync($"{input}에 해당하는 코인 이름을 찾지 못했습니다.");
            }
        }       

        [Command("코인버블")]
        [Remarks("코인 가격 상승후 하락")]
        [Hidden]
        public async Task CoinSurgeAndFallCommand([Remainder] string input = null)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyAsync($"등록할 코인의 이름을 입력해주세요. 예시: `{ConfigManager.Config.Prefix}코인버블 데꾸코인`");
                return;
            }

            var (coinName, coinId) = await _dbManager.GetCoinIdByNameAsync(input);

            if (coinName != null)
            {
                await ReplyAsync($"{input}에 해당하는 코인의 상승 후 하락 이벤트를 등록했습니다.");
                await _coinManager.HandlePriceSurgeAndFallEventAsync(coinId);                
            }
            else
            {
                await ReplyAsync($"{input}에 해당하는 코인 이름을 찾지 못했습니다.");
            }
        }
        [Command("코인반등")]
        [Remarks("코인 가격 하락후 상승")]
        [Hidden]
        public async Task CoinPlungeAndSurgeCommand([Remainder] string input = null)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(input))
            {
                await ReplyAsync($"등록할 코인의 이름을 입력해주세요. 예시: `{ConfigManager.Config.Prefix}코인반등 데꾸코인`");
                return;
            }

            var (coinName, coinId) = await _dbManager.GetCoinIdByNameAsync(input);

            if (coinName != null)
            {
                await ReplyAsync($"{input}에 해당하는 코인의 상승 후 하락 이벤트를 등록했습니다.");
                await _coinManager.HandlePriceDropAndSurgeEventAsync(coinId);
            }
            else
            {
                await ReplyAsync($"{input}에 해당하는 코인 이름을 찾지 못했습니다.");
            }
        }

        [Command("대공황")]
        [Remarks("대공황 이벤트를 발생시킵니다.")]
        [Hidden]
        public async Task TriggerGreatDepressionAsync()
        {
            // 관리자만 실행 가능하도록 체크
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            // 대공황 뉴스 출력 및 코인 급락 처리
            await _coinManager.RunGreatDepressionEventAsync();
        }
        [Command("달러입금")]
        [Remarks("입금하기")]
        [Hidden]
        public async Task DepositCommand(ulong userId, int price)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            await _dbManager.AddDollarAsync(userId, price);
            await ReplyAsync($"<@{userId}> 에게 {price} 달러를 입금했어요.");
        }

        [Command("코인입금")]
        [Remarks("입금하기")]
        [Hidden]
        public async Task CoinDepositCommand(ulong userId, int price)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            
            await _dbManager.AddSlotCoinAsync(userId, price);
            await ReplyAsync($"<@{userId}> 에게 {price} 코인을 지급했어요.");
        }

        [Command("슬롯초기화")]
        [Remarks("슬롯머신 기록 초기화")]
        [Hidden]
        public async Task ResetSlotCommand()
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            await SlotMachineManager.ResetSlotRecord();
        }
        [Command("코인초기화")]
        [Remarks("코인 기록 초기화")]
        [Hidden]
        public async Task ResetCoinCommand()
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            await CoinMarketManager.ResetCoinRecord();           
        }
        [Command("티켓추가")]
        [Remarks("입금하기")]
        [Hidden]
        public async Task AddTicketCommand(ulong userId, int number)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            
            await _dbManager.AddSlotTicketAsync(userId, number);
            await ReplyAsync($"<@{userId}> 에게 티켓 {number}개 지급했어요.");
        }

        [Command("버섯제거")]
        [Remarks("버섯제거")]
        [Hidden]
        public async Task AddSpecialCommand(ulong userId, int number)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            await _dbManager.AddSpecialAsync(userId, number);
            await ReplyAsync($"<@{userId}> 에게 버섯제거 {number}회 지급했어요.");
        }

        [Command("보상")]
        [Remarks("유저들에게 보상주기")]
        [Hidden]
        public async Task GiveRewardCommand(ulong userId)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            await _dbManager.AddSlotTicketAsync(userId, 20);
            await _dbManager.AddSpecialAsync(userId, 30);


            await ReplyAsync($"<@{userId}> 에게 티켓 20개와 확률증가(버섯제거) 30회를 지급했어요.");
        }
     
        [Command("패널티")]
        [Remarks("유저에게 패널티주기")]
        [Hidden]
        public async Task PenaltyCommand(ulong userId, string coinOrDollarInput)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            bool isCoinRepay;

            if (coinOrDollarInput.Equals("코인"))
            {
                isCoinRepay = true;
            }
            else if(coinOrDollarInput.Equals("달러"))
            {
                isCoinRepay = false;
            }
            else
            {
                await ReplyAsync("잘못된 화폐를 입력했습니다.");
                return;
            }

            Bot bot = new Bot();

            await bot.ExecuteLoanRepaymentCheckAsync(userId, isCoinRepay);
        }

        [Command("패널티2")]
        [Remarks("유저에게 패널티주기")]
        [Hidden]
        public async Task GivePenaltyCommand(ulong userId, int penalty, int profit, int isCoin)
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }

            bool isCoinRepay = isCoin == 1 ? true : false;

            await _dbManager.RecordUserProfitAsync(userId, -profit);
            await _loanService.SendChannelMessage(userId, penalty, isCoinRepay);
        }

        [Command("로또추첨")]
        [Remarks("로또 추첨하기")]
        [Hidden]
        public async Task DrawLottoCommand()
        {
            if (Context.User.Id != ConfigManager.Config.OwnerId)
            {
                return;
            }
            Bot bot = new Bot();
            await bot.AnnounceLottoResultAsync();
        }
       
        //[Command("슬롯")]
        //[Remarks("테스트중인 슬롯머신")]
        //[Hidden]
        //public async Task SlotCommand()
        //{
        //    if (Context.User.Id != ConfigManager.Config.OwnerId)
        //    {
        //        return;
        //    }

        //    _ = Task.Run(async () =>
        //    {
        //        var slotMachineManager = new SlotMachineManager();
        //        var (isSuccess, message) = await slotMachineManager.RunSlotMachine(Context.User, Context.Channel as ITextChannel, 1000, 1, true);

        //        if (!isSuccess)
        //        {
        //            // 문제가 발생했을 때만 메시지를 보냄
        //            await Context.Channel.SendMessageAsync($"{Context.User.Mention} {message}");
        //        }

        //    });
        //}
    }
}
