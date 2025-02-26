using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using MoongBot.Core.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace MoongBot.Core.Commands
{
    public class InteractionCommands : ModuleBase<SocketCommandContext>
    {
        private static InteractionManager _interactionManager = new InteractionManager();
        private static ulong lottoChannelId = ConfigManager.Config.LottoChannelId;
        private static ulong coinChannelId = ConfigManager.Config.CoinChannelId;


        [Command("수동")]
        [Remarks("로또 티켓을 수동으로 구매합니다. 버튼을 누르고 입력창에 1 ~ 15 사이의 숫자 6개를 중복없이 입력해주세요.")]
        public async Task LottoCommand()
        {
            if (Context.Channel.Id != lottoChannelId)
            {
                var guildChannel = Context.Channel as SocketGuildChannel;
                var lottoChannel = guildChannel.Guild.GetChannel(lottoChannelId);
                await Context.Channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널에서 이용해주세요!");
                return;
            }
            await _interactionManager.SendButtonAsync(Context);
        }

        //[Command("슬롯머신")]
        //[Remarks("슬롯머신을 돌려볼 수 있어요. 버튼을 누르고 사용할 금액(10 ~ 100 or 1000)과 슬롯머신 사용횟수(1 ~ 5)를 입력해주세요.")]
        //public async Task SlotMachineCommand()
        //{
        //    if (Context.Channel.Id != lottoChannelId)
        //    {
        //        var guildChannel = Context.Channel as SocketGuildChannel;
        //        var lottoChannel = guildChannel.Guild.GetChannel(lottoChannelId) as SocketGuildChannel;
        //        await Context.Channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널에서 이용해주세요!");
        //        return;
        //    }

        //    await _interactionManager.SendSlotButtonAsync(Context);
        //}

        [Command("슬롯머신")]
        [Alias("슬롯")]
        [Remarks("슬롯모션이 줄어든 간편한 슬롯머신을 돌려볼 수 있어요. 버튼을 누르고 사용할 금액(10 ~ 100 or 1000)과 슬롯머신 사용횟수(1 ~ 10)를 입력해주세요.")]
        public async Task SimpleSlotMachineCommand()
        {
            if (Context.Channel.Id != lottoChannelId)
            {
                var guildChannel = Context.Channel as SocketGuildChannel;
                var lottoChannel = guildChannel.Guild.GetChannel(lottoChannelId);
                await Context.Channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널에서 이용해주세요!");
                return;
            }

            await _interactionManager.SendSimpleSlotButtonAsync(Context);
        }

        [Command("도박슬롯")]
        [Alias("도박")]
        [Remarks("모 아니면 도 모험을 좋아하는 이용자를 위한 슬롯머신이에요. 🍄이나 💣이 나오면 코인을 빼앗기니 주의해서 사용해야해요 1회 이용에 1000코인과 티켓 한장이 소모돼요. 슬롯머신 사용횟수(1 ~ 5)를 입력해주세요.")]
        public async Task NethorSlotMachineCommand()
        {
            if (Context.Channel.Id != lottoChannelId)
            {
                var guildChannel = Context.Channel as SocketGuildChannel;
                var lottoChannel = guildChannel.Guild.GetChannel(lottoChannelId);
                await Context.Channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널에서 이용해주세요!");
                return;
            }

            await _interactionManager.SendNethorButtonAsync(Context);
        }

        [Command("슬롯스킵")]
        [Alias("스킵")]
        [Remarks("슬롯머신을 사용하지 않고 결과만 출력합니다.")]
        public async Task SkipSlotMachineCommand()
        {
            if (Context.Channel.Id != lottoChannelId)
            {
                var guildChannel = Context.Channel as SocketGuildChannel;
                var lottoChannel = guildChannel.Guild.GetChannel(lottoChannelId) as SocketGuildChannel;
                await Context.Channel.SendMessageAsync($"\"{lottoChannel.Name}\" 채널에서 이용해주세요!");
                return;
            }

            await _interactionManager.SendSkipButtonAsync(Context);
        }

        //[Command("대출")]
        //[Remarks("달러나 코인을 대출 받을 수 있습니다. 상환 기한은 일주일이며 기한 내에 갚지 못하면 채널 활동이 막히고 네잎클로버 찾기 노동을 해야 패널티가 풀리니 주의해서 사용해주세요. 슬롯 채널에서 사용시 코인 대출, 코인 채널에서 사용시 달러 대출을 받을수 있습니다.")]
        //public async Task LoanCommand()
        //{
        //    ulong channelId = Context.Channel.Id;

        //    if(channelId == lottoChannelId)
        //    {
        //        await _interactionManager.SendCoinLoanButtonAsync(Context.Channel);
        //    }
        //    else if (channelId == coinChannelId)
        //    {
        //        await _interactionManager.SendDollarLoanButtonAsync(Context.Channel);
        //    }
        //    else
        //    {
        //        await Context.Channel.SendMessageAsync("슬롯머신, 코인 채널에서 이용해주세요.");
        //    }           
        //}


        //[Command("상환")]
        //[Remarks("대출금을 상환할 수 있습니다. 이자 상환시 상환기한이 일주일 연장됩니다.")]
        //public async Task RepayCommand()
        //{
        //    if (Context.Channel.Id != lottoChannelId && Context.Channel.Id != coinChannelId)
        //    {
        //        if (Context.Channel.Id != 1276108940299730965)
        //        {
        //            var guildChannel = Context.Channel as SocketGuildChannel;
        //            var lottoChannel = guildChannel.Guild.GetChannel(lottoChannelId);
        //            var coinChannel = guildChannel.Guild.GetChannel(coinChannelId);
        //            await Context.Channel.SendMessageAsync($"\"{lottoChannel.Name}\"채널이나 \"{coinChannel.Name}\"채널에서 이용해주세요!");
        //        }
        //        return;
        //    }

        //    await _interactionManager.SendRepayButtonAsync(Context);
        //}
    }
}
