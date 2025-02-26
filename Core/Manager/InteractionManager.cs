using Discord.Commands;
using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MoongBot.Core.Commands;
using MoongBot.Core.NewFolder;
using Newtonsoft.Json;
using Discord.Interactions;
using System.Threading.Channels;
using ScottPlot.Renderable;

namespace MoongBot.Core.Manager
{
    public class InteractionManager
    {
        private IMessage originalMessage;

        private static CoinMarketManager _coinManager = new CoinMarketManager();
        private static DatabaseManager _dbManager = new DatabaseManager();
        private static string loanFilePath = Path.Combine("jsonFiles", "loanData.json");

        public static float interestRate = 50;
        private int coinLimit = 1000000;
        private int dollarLimit = 1000000;

        public async Task SendButtonAsync(SocketCommandContext context)
        {
            var button = new ButtonBuilder()
            {
                Label = "로또 구매",
                CustomId = "manual_btn",
                Style = ButtonStyle.Primary
            };

            var component = new ComponentBuilder()
                .WithButton(button);

            await context.Channel.SendMessageAsync("버튼을 클릭해 로또를 구매해주세요.", components: component.Build());
        }

        //public async Task SendSlotButtonAsync(SocketCommandContext context)
        //{
        //    var button = new ButtonBuilder()
        //    {
        //        Label = "MoongBot SlotMachine",
        //        CustomId = "slot_btn",
        //        Style = ButtonStyle.Primary
        //    };

        //    var component = new ComponentBuilder()
        //        .WithButton(button);

        //    await context.Channel.SendMessageAsync("버튼을 눌러 슬롯머신을 사용해보세요!", components: component.Build());
        //}
        public async Task SendSimpleSlotButtonAsync(SocketCommandContext context)
        {
            var button = new ButtonBuilder()
            {
                Label = "MoongBot SlotMachine",
                CustomId = "simpleslot_btn",
                Style = ButtonStyle.Primary
            };

            var component = new ComponentBuilder()
                .WithButton(button);

            await context.Channel.SendMessageAsync("버튼을 눌러 슬롯머신을 사용해보세요!", components: component.Build());
        }

        public async Task SendNethorButtonAsync(SocketCommandContext context)
        {
            var button = new ButtonBuilder()
            {
                Label = "햄붕이의 보물창고",
                CustomId = "nethorslot_btn",
                Style = ButtonStyle.Primary
            };

            var component = new ComponentBuilder()
                .WithButton(button);

            await context.Channel.SendMessageAsync("버튼을 눌러 슬롯머신을 사용해보세요!", components: component.Build());
        }
        
        public async Task SendSkipButtonAsync(SocketCommandContext context)
        {
            var button = new ButtonBuilder()
            {
                Label = "슬롯머신 스킵",
                CustomId = "skipslot_btn",
                Style = ButtonStyle.Primary
            };

            var component = new ComponentBuilder()
                .WithButton(button);

            await context.Channel.SendMessageAsync("슬롯머신을 이용할 횟수를 입력해주세요. 결과만 출력돼요.", components: component.Build());
        }
        public async Task SendDollarLoanButtonAsync(ISocketMessageChannel channel)
        {
            var button = new ButtonBuilder()
            {
                Label = "달러 대출하기",
                CustomId = "dollarloan_btn",
                Style = ButtonStyle.Danger
            };

            var component = new ComponentBuilder()
                .WithButton(button);

            var alertEmbed = new EmbedBuilder()
                .WithTitle("대출 주의사항")
                .WithDescription("주의사항을 읽고 버튼을 눌러 대출을 진행해주세요. \n\n" +
                     $"1. 이자는 매일 원금의 {interestRate}%씩 증가하며 대출금은 일주일 내에 상환해야 합니다.\n" +
                     "2. 기한 내 상환이 불가하다면 이자 상환 시 기한이 일주일 연장됩니다. 상환할 금액에 이자보다 크거나 같고 갚아야할 돈보다는 적은 금액을 입력하면 이자만 상환됩니다.\n" +
                     "3. 기한 내에 갚지 못할 시 역할이 \'🚫\'로 변경되며 서버의 채널을 보거나 메시지를 보낼 수 없게되고 \'🍀ㅣ풀숲\'채널 에서 네잎클로버 수집을 해야 기존의 역할로 변경됩니다.")
                .WithColor(Color.Blue)
                .Build();
            
            await channel.SendMessageAsync(embed: alertEmbed, components: component.Build());
        }
        
        public async Task SendCoinLoanButtonAsync(ISocketMessageChannel channel)
        {
            var button = new ButtonBuilder()
            {
                Label = "코인 대출하기",
                CustomId = "coinloan_btn",
                Style = ButtonStyle.Danger
            };

            var component = new ComponentBuilder()
                .WithButton(button);

            var alertEmbed = new EmbedBuilder()
                .WithTitle("대출 주의사항")
                .WithDescription("주의사항을 읽고 버튼을 눌러 대출을 진행해주세요. \n\n" +
                     $"1. 이자는 매일 원금의 {interestRate}%씩 증가하며 대출금은 일주일 내에 상환해야 합니다.\n" +
                     "2. 기한 내 상환이 불가하다면 이자 상환 시 기한이 일주일 연장됩니다. 상환할 금액에 이자보다 크거나 같고 갚아야할 돈보다는 적은 금액을 입력하면 이자만 상환됩니다.\n" +
                     "3. 기한 내에 갚지 못할 시 역할이 \'🚫\'로 변경되며 서버의 채널을 보거나 메시지를 보낼 수 없게되고 \'🍀ㅣ풀숲\'채널 에서 네잎클로버 수집을 해야 기존의 역할로 변경됩니다.")
                .WithColor(Color.Blue)
                .Build();

            await channel.SendMessageAsync(embed: alertEmbed, components: component.Build());
        }
        public async Task SendRepayButtonAsync(SocketCommandContext context)
        {
            var button = new ButtonBuilder()
            {
                Label = "대출금 상환",
                CustomId = "repay_btn",
                Style = ButtonStyle.Success
            };

            var component = new ComponentBuilder()
                .WithButton(button);

            await context.Channel.SendMessageAsync("이자만 상환하여 상환기간을 일주일 연장할 수 있습니다. 대출금을 전액 상환하려면 한번에 상환해야합니다.",components: component.Build());
        }
        public async Task HandleButtonClickAsync(SocketMessageComponent component, BotCommands command, ConvCommands convCommand)
        {
                var channel = component.Channel;
                var guildChannel = channel as SocketGuildChannel;
                var guild = guildChannel?.Guild;
                var user = component.User;

                if (user == null || guildChannel == null || guild == null)
                {
                    Console.WriteLine("User, GuildChannel, or Guild is null");
                    await component.RespondAsync("잘못된 요청입니다.", ephemeral: true);
                    return;
                }

                bool IsUserAuthorized(string customId)
                {
                    var userIdFromButton = customId.Split('_').Last();
                    return userIdFromButton == user.Id.ToString();

                }

                switch (component.Data.CustomId)
                {
                    case "help_btn":
                    await convCommand.HelpCommand(component, guild, channel as ITextChannel);      
                    break;
                case "weather_btn":
                    var weatherModal = new ModalBuilder()
                        .WithTitle("도시명 입력")
                        .WithCustomId("weather_modal")
                        .AddTextInput("날씨 정보를 받을 도시명을 입력해주세요.", "city_input", placeholder: "ex: 인천", required: true)
                        .Build();

                    await component.RespondWithModalAsync(weatherModal);
                    break;
                case "weatherlist_btn":
                    await component.DeferAsync(ephemeral: true);
                    await convCommand.ListCommand(guild, channel);
                        break;
                    case "punding_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.PundingCommand(channel, user);
                        break;

                    case "roulette_btn":
                        bool isSuccessR = await command.RouletteCommand(user, channel);
                        if (!isSuccessR)
                        {
                            await component.RespondAsync("룰렛 돌리기에 실패했습니다.", ephemeral: true);
                        }
                        else
                        {
                            await component.DeferAsync();
                        }                      
                        break;

                    case "slot_btn":
                        var slotmodal = new ModalBuilder()
                            .WithTitle("Insert Coin")
                            .WithCustomId("slot_modal")
                            .AddTextInput("사용할 금액(10 ~ 100 or 1000)을 입력해주세요.", "number_input", placeholder: "ex: 10", required: true)
                            .AddTextInput("슬롯머신을 이용할 횟수(1 ~ 5)를 입력해주세요", "count_input", placeholder: "ex: 5", required: true)
                            .Build();

                        await component.RespondWithModalAsync(slotmodal);
                        break;
                case "simpleslot_btn":
                    var simpeslotmodal = new ModalBuilder()
                            .WithTitle("Insert Coin")
                            .WithCustomId("simpleslot_modal")
                            .AddTextInput("사용할 금액(10 ~ 100 or 1000)을 입력해주세요.", "number_input", placeholder: "ex: 10", required: true)
                            .AddTextInput("슬롯머신을 이용할 횟수(1 ~ 10)를 입력해주세요", "count_input", placeholder: "ex: 5", required: true)
                            .Build();

                    await component.RespondWithModalAsync(simpeslotmodal);
                    break;
                case "nethorslot_btn":
                    int coinValue = await _dbManager.GetUserSlotCoinAsync(user.Id);
                    int ticket = await _dbManager.GetTicketValueAsync(user.Id);
                    var nethorlotmodal = new ModalBuilder()
                        .WithTitle($"햄붕이의 보물창고, 보유 코인 : {coinValue}, 보유 티켓 {ticket}개")
                        .WithCustomId("nethorslot_modal")
                        .AddTextInput("이용할 횟수(1~5)를 입력해주세요 1000코인과 티켓 한장이 사용됩니다.", "count_input", placeholder: "ex: 5", required: true)
                        .Build();
                    await component.RespondWithModalAsync(nethorlotmodal);
                    break;
                case "skipslot_btn":
                    int coinValue2 = await _dbManager.GetUserSlotCoinAsync(user.Id);
                    var skipSlotModal = new ModalBuilder()
                        .WithTitle($"보유 코인 : {coinValue2:N0}")
                        .WithCustomId("skipslot_modal")
                        .AddTextInput("이용할 금액(10~100 or 1000)를 입력해주세요.", "price_input", placeholder: "ex: 1000", required: true)
                        .AddTextInput("이용할 횟수(10~200)를 입력해주세요.", "count_input", placeholder: "ex: 50", required: true)
                        .Build();
                    await component.RespondWithModalAsync(skipSlotModal);
                    break;
                case "stop_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.SlotStopCommand(channel, user);                     
                        break;

                    case "auto_btn":
                        var autoLottoModal = new ModalBuilder()
                            .WithTitle("로또 구매 수량 입력")
                            .WithCustomId("autolotto_modal")
                            .AddTextInput($"구매할 로또의 수량을 1 ~ {LottoManager.maxLotto} 사이의 숫자로 입력해주세요.", "quantity_input", placeholder: "ex: 5", required: true)
                            .Build();

                        await component.RespondWithModalAsync(autoLottoModal);
                        break;

                    case "manual_btn":
                        var modalBuilder = new ModalBuilder()
                            .WithTitle("로또 번호 입력")
                            .WithCustomId("numbers_modal");

                    modalBuilder.AddTextInput(
                                $"1 ~ 15사이의 숫자 6개를 중복없이 입력해주세요.(쉼표로 구분) -1번 로또",
                                $"numbers_input_0",
                                placeholder: "ex: 1,2,3,4,5,6",
                                required: true
                            );
                    int space = Math.Min(LottoManager.maxLotto - 1, 4);
                    // LottoManager.maxLotto 값에 따라 텍스트 입력 필드를 동적으로 추가
                    for (int i = 0; i < space; i++)
                        {
                            modalBuilder.AddTextInput(
                                $"{i+2}번 로또",
                                $"numbers_input_{i+1}", // 각 입력 필드에 고유한 ID 부여
                                placeholder: "ex: 1,2,3,4,5,6",
                                required: false
                            );
                        }
                        await component.RespondWithModalAsync(modalBuilder.Build());
                        break;

                case "wordlist_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.ListWordsCommand(user, channel);
                        break;

                    case "lotto_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.ShowLottoCommand(user, channel);                 
                        break;

                    case "balance_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.GetbalanceCommand(user, channel);
                        break;

                    case "shop_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.OpenShopCommand(channel);
                        break;

                    case "ranking_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.RankingCommand(channel, guild);                       
                        break;

                    case "hof_btn":
                        await component.DeferAsync(ephemeral: true);
                        await command.HOFCommand(channel, guild);
                        break;

                    case "dollarloancaution_btn":
                    await component.DeferAsync(ephemeral: true);
                    await SendDollarLoanButtonAsync(channel);
                    break;

                    case "coinloancaution_btn":
                    await component.DeferAsync(ephemeral: true);
                    await SendCoinLoanButtonAsync(channel);
                    break;

                    case "dollarloan_btn":
                        var loanModal = new ModalBuilder()
                            .WithTitle("달러 대출")
                            .WithCustomId("dollarloan_modal")
                            .AddTextInput($"대출할 금액을 입력해주세요. (1000 ~ {dollarLimit:N0} 사이의 숫자)", "amount_input", placeholder: "ex: 1000", required: true)
                            .AddTextInput("주의사항을 읽어보시고 동의해주세요. 일주일 내에 상환실패시 패널티가 부여됩니다.", "agree_input", placeholder: "Ex : 동의", required: true)                            
                            .Build();

                        await component.RespondWithModalAsync(loanModal);
                        break;

                case "coinloan_btn":
                    var coinloanModal = new ModalBuilder()
                        .WithTitle("코인 대출")
                        .WithCustomId("coinloan_modal")
                        .AddTextInput($"대출할 금액을 입력해주세요. (100 ~ {coinLimit:N0} 사이의 숫자)", "amount_input", placeholder: "ex: 1000", required: true)
                        .AddTextInput("주의사항을 읽어보시고 동의해주세요. 일주일 내에 상환실패시 패널티가 부여됩니다.", "agree_input", placeholder: "Ex : 동의", required: true)
                        .Build();

                    await component.RespondWithModalAsync(coinloanModal);
                    break;

                case "repay_btn":
                    bool isCoinRepay;
                    if (channel.Id == ConfigManager.Config.LottoChannelId)
                    {
                        isCoinRepay = true;
                    }
                    else
                    {
                        isCoinRepay = false;
                    }
                        var (loanAmount, interest, isCoin, date) = await _dbManager.GetTotalRepaymentAmountAsync(user.Id, isCoinRepay);
                        if(loanAmount == -1)
                        {
                            loanAmount = 0;
                            interest = 0;
                        }
                        string coinOrDollar = isCoinRepay ? "코인" : "달러";
                        var repayModal = new ModalBuilder()
                            .WithTitle($"{coinOrDollar} 대출금 상환")
                            .WithCustomId("repay_modal")
                            .AddTextInput($"상환할 금액을 입력해주세요.원금:{loanAmount},이자:{interest}", "amount_input", placeholder: "Ex : 1000 or *", required: true)
                            .Build();

                        await component.RespondWithModalAsync(repayModal);
                        break;
                   
                    case "coinlist_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.CoinListCommand(channel);
                        break;
                    case "chart_btn":
                        var chartModal = new ModalBuilder()
                            .WithTitle("코인 차트 출력")
                            .WithCustomId("chart_modal")
                            .AddTextInput($"출력할 차트의 코인명을 입력해주세요.", "coinname_input", placeholder: "Ex : 데꾸코인", required: true)
                            .AddTextInput($"며칠치 데이터를 출력하시겠습니까? 1 ~ 7의 숫자를 입력해주세요.", "day_input", placeholder: "Ex : 3", required: false)
                            .Build();

                        await component.RespondWithModalAsync(chartModal);
                        break;
                    case "portfolio_btn":
                    await component.DeferAsync(ephemeral: true);
                    await command.PortfolioCommand(user, channel);                       
                        break;
                    case "autotrade_btn":
                        var autotradeModal = new ModalBuilder()
                            .WithTitle("코인 자동매매")
                            .WithCustomId("autotrade_modal")
                            .AddTextInput($"자동매매 할 코인명을 입력해주세요.", "coinname_input", placeholder: "Ex : 데꾸코인", required: true)
                            .AddTextInput($"매매할 가격을 입력해주세요. 소숫점 두 자리수까지 입력 가능해요.", "price_input", placeholder: "Ex : 150.24", required: true)
                            .AddTextInput($"매매할 수량을 입력해주세요.*입력시 매매가능한 최대 수량이 설정됩니다.", "quantity_input", placeholder: "Ex : 15.24", required: true)
                            .AddTextInput($"매수할 것인지 매도할 것인지 입력해주세요.", "action_input", placeholder: "Ex : 매도", required: true)
                            .Build();                    

                        await component.RespondWithModalAsync(autotradeModal);
                        break;
                    case "showautotrade_btn":
                        await component.DeferAsync(ephemeral: true);
                        await command.ShowAutoTradeCommand(user.Id, user, channel as ITextChannel);
                        break;
                    case "deleteautotrade_btn":
                        await component.DeferAsync(ephemeral: true);
                        await command.DeleteAutoTradeCommand(user.Id, channel as ITextChannel);
                        break;
                    case "subscrib_btn":
                        await component.DeferAsync(ephemeral: true);
                        await command.CoinNewsSubscribeCommand(channel, user, guild);
                        break;
                    case "unsubscrib_btn":
                        await component.DeferAsync(ephemeral: true);
                        await command.CancleSubscribeCommand(channel, user, guild);
                        break;
                case var customId when customId.StartsWith("previous_page_") || customId.StartsWith("next_page_"):
                        await HandlePageNavigationAsync(component);
                        break;
                    case string customId when customId.StartsWith("rank_"):
                        await component.DeferAsync(ephemeral: true);
                        await HandleRankPageNavigationAsync(component, guild);
                        break;
                    case string customId when customId.StartsWith("rankc_"):
                        await component.DeferAsync(ephemeral: true);
                        await HandleCoinRankPageNavigationAsync(component, guild);
                        break;
                    case string customId when customId.StartsWith("confirm_purchase_"):
                        {
                            if (!IsUserAuthorized(customId))
                            {
                                await component.RespondAsync("이 UI는 다른 유저가 생성한 구매창입니다. 상품을 구매하려면 상점 UI에서 상품 구매를 진행해주세요.", ephemeral: true);
                                return;
                            }

                            var parts = customId.Replace("confirm_purchase_", "").Split('_');
                            var itemName = parts[0];
                            var shopManager = new ShopManager();
                            var (userDding, userCoin, userDallor) = await _dbManager.GetAllBalanceAsync(user.Id);

                            var (purchaseSuccess, productLink) = await shopManager.PurchaseItem(user, itemName, userDding);
                            if (purchaseSuccess)
                            {
                                if (originalMessage != null)
                                    await originalMessage.DeleteAsync();

                                var item = shopManager.GetItems().FirstOrDefault(i => i.Name == itemName);

                                var remainingBalance = userDding - item.Price;

                                var successEmbed = new EmbedBuilder()
                                {
                                    Title = "구매 성공",
                                    Description = $"{itemName}을(를) 성공적으로 구매하였습니다!\n\n" +
                                                  $"남은 잔액 : **{remainingBalance.ToString("N0")}** :mushroom:",
                                    Color = new Color(0, 255, 0)
                                }.Build();

                                // 비공개로 임베드 메시지 전송
                                await component.RespondAsync(embed: successEmbed, ephemeral: true);
                                // 상품을 구매한 사람에게 DM으로 상품 전달
                                if (!string.IsNullOrEmpty(productLink))
                                {
                                    try
                                    {
                                        Console.WriteLine($"{DateTime.Now.ToString("MM - dd HH:mm")}{user.Username}(id :                 {user.Id}) 유저가 {itemName} 상품 구매, 해당 유저에게 {productLink} 제공");
                                        await user.SendMessageAsync($"'{itemName}' 상품을 성공적으로 구매하셨습니다!\n                                               {productLink}");
                                    }
                                    catch (Exception ex)
                                    {
                                    Console.WriteLine($"{DateTime.Now.ToString("MM - dd HH:mm")}{user.Username}(id :                 {user.Id}) 유저에게 {itemName} 상품 구매, 해당 유저에게 {productLink} 제공 실패");
                                    await component.Channel.SendMessageAsync($"{user.Mention} <@{ConfigManager.Config.OwnerId}>상품 전달중 문제가 생겼습니다.");
                                    await ExceptionManager.HandleExceptionAsync(ex);
                                    }
                                }
                                if (itemName.Equals("띵마카세"))
                                {
                                    await channel.SendMessageAsync($"<@350896838863486977>님!! <@{user.Id}>가 \'{itemName}\' 상품을 구매했어요!");
                                }                                
                            }
                            else
                            {
                                await component.RespondAsync($"{itemName} 구매에 실패하였습니다. {productLink}", ephemeral: true);
                            }
                        }
                        break;

                    case string customId when customId.StartsWith("cancel_purchase_"):
                        var userIdFromButton = customId.Replace("cancel_purchase_", "");
                        if (userIdFromButton != user.Id.ToString())
                        {
                            await component.RespondAsync("이 UI는 다른 유저가 생성한 구매창입니다. 상품을 구매하려면 상점 UI에서 상품 구매를 진행해주세요.", ephemeral: true);
                            return;
                        }

                        if (originalMessage != null)
                            await originalMessage.DeleteAsync();
                        break;

                    case string customId when customId.StartsWith("buy_coin_"):
                        var customIdParts = customId.Split('_');

                        if (customIdParts.Length >= 3 && int.TryParse(customIdParts[2], out int coinId))
                        {
                            string coinName = await _dbManager.GetCoinNameByIdAsync(coinId);

                            double userDollar = await _dbManager.GetUserDollarAsync(user.Id);

                            var buyCoinModal = new ModalBuilder()
                                .WithTitle($"{coinName} 매입(잔액: {userDollar:N2})")
                                .WithCustomId($"buycoin_modal_{coinId}")
                                .AddTextInput($"사용할 금액을 입력해주세요 수수료는 2%입니다.", "price_input", placeholder: "Ex : 1245.16 (소수점 두 자리까지)", required: true)
                                .Build();

                            await component.RespondWithModalAsync(buyCoinModal);
                        }
                        else
                        {
                            await component.RespondAsync("잘못된 코인 ID입니다.", ephemeral: true);
                        }
                        break;
                    case string customId when customId.StartsWith("sell_coin_"):
                        var sellCustomIdParts = customId.Split('_');

                        if (sellCustomIdParts.Length >= 3 && int.TryParse(sellCustomIdParts[2], out int sellCoinId))
                        {
                            string coinName = await _dbManager.GetCoinNameByIdAsync(sellCoinId);
                            var (searchCoinId, totalQuantity, averagePrice) = await _dbManager.GetUserCoinHoldingsForSpecificCoinAsync(user.Id, sellCoinId);
                            double quantity = 0;

                            quantity = totalQuantity;

                            var buyCoinModal = new ModalBuilder()
                                .WithTitle($"{coinName} 매도(보유한 코인 수량 : {quantity:N2})")
                                .WithCustomId($"sellcoin_modal_{sellCoinId}")
                                .AddTextInput("판매할 수량을 입력해주세요.전부 팔고싶으면 입력창에 *을 입력해주세요.", "quantity_input", placeholder: "Ex : 1.16 (소수점 두 자리까지) or *", required: true)
                                .Build();

                            await component.RespondWithModalAsync(buyCoinModal);
                        }
                        else
                        {
                            await component.RespondAsync("잘못된 코인 ID입니다.", ephemeral: true);
                        }
                        break;
                case "feedback_btn":
                    var feedbackModal = new ModalBuilder()
                        .WithTitle("건의사항 및 버그 제보")
                        .WithCustomId("feedback_modal")
                        .AddTextInput("텍스트를 입력하세요.", "feedback_input", TextInputStyle.Paragraph, placeholder: "텍스트를 입력하세요.", required: true)
                        .Build();

                        await component.RespondWithModalAsync(feedbackModal);
                        break;
                default:
                        // 상점에서 생성된 버튼 처리
                        if (component.Data.CustomId.EndsWith("_btn"))
                        {
                            var itemName = component.Data.CustomId.Replace("_btn", "");
                            var shopManager = new ShopManager();
                            var (userDding, userCoin, userDallor) = await _dbManager.GetAllBalanceAsync(user.Id);
                            var item = shopManager.GetItems().FirstOrDefault(i => i.Name == itemName);

                            if (item != null)
                            {
                                await component.DeferAsync(ephemeral: true);

                                var guildUser = user as IGuildUser;
                                string userNickname = guildUser?.Nickname ?? user.Username;

                                var embedBuilder = new EmbedBuilder()
                                {
                                    Title = $"{userNickname}님의 구매 확인",
                                    Description = $"상품: **{itemName}**\n가격: **{item.Price.ToString("N0")}** :mushroom:\n현재 잔액: **{userDding.ToString("N0")}** :mushroom:\n구매를 진행하시겠습니까?",
                                    Color = new Color(255, 145, 200)
                                };

                                var buttons = new ComponentBuilder()
                                    .WithButton("구매", $"confirm_purchase_{itemName.ToLower()}_{user.Id}", ButtonStyle.Success)
                                    .WithButton("취소", $"cancel_purchase_{user.Id}", ButtonStyle.Danger);

                                originalMessage = await component.FollowupAsync(embed: embedBuilder.Build(), components: buttons.Build(), ephemeral: false);
                            }
                        }
                        break;
                }
        }

        private async Task HandlePageNavigationAsync(SocketMessageComponent component)
        {
            try
            {                
                var customIdParts = component.Data.CustomId.Split('_');
                if (customIdParts.Length < 3)
                {
                    await component.RespondAsync("Invalid custom ID format.", ephemeral: true);
                    return;
                }

                var direction = customIdParts[0];
                var currentPageIndex = int.Parse(customIdParts[2]);

                var pageCount = HelpEmbedService.PageCount;
                int newPageIndex;

                if (direction == "previous")
                {
                    newPageIndex = (currentPageIndex - 1 + pageCount) % pageCount;
                }
                else
                {
                    newPageIndex = (currentPageIndex + 1) % pageCount;                   
                }

                var embed = HelpEmbedService.GetEmbedForPage(newPageIndex);
                var componentBuilder = new ComponentBuilder();

                if (pageCount > 1)
                {
                    var previousButton = new ButtonBuilder()
                        .WithLabel("◀️ 이전")
                        .WithCustomId($"previous_page_{newPageIndex}")
                        .WithStyle(ButtonStyle.Secondary);
                    componentBuilder.WithButton(previousButton);
                }
                
                // Add next button if not on the last page
                if (pageCount > 1)
                {
                    var nextButton = new ButtonBuilder()
                        .WithLabel("다음 ▶️")
                        .WithCustomId($"next_page_{newPageIndex}")
                        .WithStyle(ButtonStyle.Secondary);
                    componentBuilder.WithButton(nextButton);
                }

                await component.UpdateAsync(msg =>
                {
                    msg.Embed = embed.Build();
                    msg.Components = componentBuilder.Build();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling page navigation: {ex.Message}");
                await component.RespondAsync("An error occurred while processing your request.", ephemeral: true);
            }
        }

        public async Task HandleRankPageNavigationAsync(SocketMessageComponent component, SocketGuild guild)
        {
            var customId = component.Data.CustomId;

            int currentPage = 1;                     
            int totalPages = 0;

            var splitCustomId = customId.Split('_');
            currentPage = int.Parse(splitCustomId.Last());

            // 슬롯 머신 매니저에서 해당 페이지에 맞는 랭킹 데이터를 가져옵니다.
            var slotManager = new SlotMachineManager();
            var report = await slotManager.GenerateRankingReportAsync(currentPage);

            totalPages = (int)Math.Ceiling(report.TotalUsers / 10.0);

            int prevPage = currentPage-1 == 0 ? totalPages : currentPage-1;
            int nextPage = currentPage+1 > totalPages ? 1 : currentPage+1;

            var embedBuilder = new EmbedBuilder()
            {
                Title = "슬롯머신 랭킹",
                Description = "슬롯머신 사용자의 랭킹입니다.",
                Color = new Color(255, 145, 200)
            };

            embedBuilder.AddField("사용자 랭킹", report.Rankings, false);

            // 새로 갱신된 페이지에 맞는 버튼 세팅
            var buttons = new ComponentBuilder()
                .WithButton("이전 페이지", $"rank_prev_{prevPage}", ButtonStyle.Primary)
                .WithButton("다음 페이지", $"rank_next_{nextPage}", ButtonStyle.Primary);

            // 기존 메시지의 내용을 수정 (임베드와 버튼)
            await component.Message.ModifyAsync(msg =>
            {
                msg.Embed = embedBuilder.Build();
                msg.Components = buttons.Build();
            });
        }

        public async Task HandleCoinRankPageNavigationAsync(SocketMessageComponent component, SocketGuild guild)
        {
            var customId = component.Data.CustomId;

            int currentPage = 1;
            int totalPages = 0;

            var splitCustomId = customId.Split('_');
            currentPage = int.Parse(splitCustomId.Last());

            // 슬롯 머신 매니저에서 해당 페이지에 맞는 랭킹 데이터를 가져옵니다.
            var (rankings, totalUsers) = await _coinManager.GetProfitRankingEmbedAsync(guild, currentPage);

            totalPages = (int)Math.Ceiling(totalUsers / 10.0);

            int prevPage = currentPage - 1 == 0 ? totalPages : currentPage - 1;
            int nextPage = currentPage + 1 > totalPages ? 1 : currentPage + 1;

            var embedBuilder = new EmbedBuilder()
            {
                Title = "호롤로 코인왕",
                Color = Color.Green,
                Description = "코인 거래로 인한 차익이 기록된 랭킹입니다."
            };

            embedBuilder.AddField("사용자 랭킹", rankings, false);

            // 새로 갱신된 페이지에 맞는 버튼 세팅
            var buttons = new ComponentBuilder()
                .WithButton("이전 페이지", $"rankc_prev_{prevPage}", ButtonStyle.Primary)
                .WithButton("다음 페이지", $"rankc_next_{nextPage}", ButtonStyle.Primary);

            // 기존 메시지의 내용을 수정 (임베드와 버튼)
            await component.Message.ModifyAsync(msg =>
            {
                msg.Embed = embedBuilder.Build();
                msg.Components = buttons.Build();
            });
        }
        public async Task HandleModalSubmittedAsync(SocketModal modal)
        {
            if (modal.Data.CustomId == "weather_modal")
            {
                var cityInput = modal.Data.Components.First(x => x.CustomId == "city_input").Value;

                await WeatherManager.WeatherAsync(modal.Channel as ITextChannel, cityInput);

                await modal.DeferAsync();
            }
            if (modal.Data.CustomId == "autolotto_modal")
            {
                var quantityInput = modal.Data.Components.First(x => x.CustomId == "quantity_input").Value;

                if(int.TryParse(quantityInput, out int quantity))
                {
                    int maxValue = LottoManager.maxLotto;
                    if (quantity <= maxValue && quantity > 0)
                    {
                        bool isSuccess = await LottoManager.BuyTicketAsync(modal.User.Id, quantity, modal.Channel as ITextChannel);

                        await modal.DeferAsync();
                    }
                    else
                    {
                        await modal.RespondAsync($"1 ~ {maxValue} 사이의 숫자를 입력해주세요.", ephemeral: true);
                    }
                }
            }
            if (modal.Data.CustomId == "numbers_modal")
            {
                var numbersInputs = modal.Data.Components
                                .Where(x => x.CustomId.StartsWith("numbers_input_"))
                                .Select(x => x.Value)
                                .ToList();

                var groupedNumbers = numbersInputs
                                    .Select(input => input.Split(',')
                                    .Select(n => n.Trim())
                                    .ToArray())
                                    .ToList();
                string description = "";
                string notification = "";
                int count = 1;
                int successCount = 0;

                var duplicateError = new List<string>();   // 숫자 중복 오류
                var rangeError = new List<string>();       // 범위 오류
                var formatError = new List<string>();      // 형식 오류
                var insufficientError = new List<string>();// 잔액부족 오류
                var limitError = new List<string>();// 로또한도 오류

                foreach (var sortedNumbers in groupedNumbers)
                {
                    if (sortedNumbers.All(n => string.IsNullOrWhiteSpace(n)))
                    {
                        continue; // 빈 입력은 건너뜁니다.
                    }

                    string joinedNumbers = string.Join(",", sortedNumbers);

                    var (isSuccess, numList, notfic) = await LottoManager.BuyManuallyTicket(modal.User.Id, joinedNumbers, modal.Channel as ITextChannel);


                    if (isSuccess)
                    {
                        description += $"번호 : {string.Join(", ", numList)}\n";
                        successCount++;
                        count++;
                    }
                    else
                    {
                        // 실패 원인에 따라 메시지 저장
                        if (notfic.Contains("중복"))
                        {
                            duplicateError.Add($"{count}번 로또 ({joinedNumbers})");
                        }
                        else if (notfic.Contains("사이"))
                        {
                            rangeError.Add($"{count}번 로또 ({joinedNumbers})");
                        }                                            
                        else if (notfic.Contains("잔액"))
                        {
                            insufficientError.Add($"{count}번 로또 ({joinedNumbers})");
                            break; 
                        }
                        else if (notfic.Contains("티켓"))
                        {
                            limitError.Add($"{count}번 로또 ({joinedNumbers})");
                            break;
                        }
                        else if (notfic.Contains("6개"))
                        {
                            formatError.Add($"{count}번 로또 ({joinedNumbers})");
                        }
                        count++;
                    }
                }

                if (successCount > 0)
                {
                    await modal.Channel.SendMessageAsync($"<@{modal.User.Id}> 로또 구매에 성공했습니다!");

                    var embedBuilder = new EmbedBuilder()
                        .WithTitle("티켓 구매에 성공했습니다!")
                        .WithDescription(description)
                        .WithColor(new Color(255, 145, 200));
                    await modal.Channel.SendMessageAsync(embed: embedBuilder.Build());

                    if (duplicateError.Any() || rangeError.Any() || formatError.Any() || insufficientError.Any() || limitError.Any())
                    {
                        string errorMessage = "";

                        if (duplicateError.Any())
                        {
                            errorMessage += "- 숫자 중복 입력\n" + string.Join("\n", duplicateError) + "\n\n";
                        }
                        if (rangeError.Any())
                        {
                            errorMessage += "- 잘못된 범위 입력\n" + string.Join("\n", rangeError) + "\n\n";
                        }
                        if (formatError.Any())
                        {
                            errorMessage += "- 잘못된 형식의 입력\n" + string.Join("\n", formatError) + "\n\n";
                        }
                        if (insufficientError.Any())
                        {
                            errorMessage += "- 사용자 잔액 부족\n" + string.Join("\n", insufficientError) + "\n\n";
                        }
                        if (limitError.Any())
                        {
                            errorMessage += "- 로또 수량 한도 초과\n" + string.Join("\n", limitError) + "\n\n";
                        }

                        var embedBuilder2 = new EmbedBuilder()
                            .WithTitle("구매에 실패한 티켓이 있습니다.")
                            .WithDescription(errorMessage)
                            .WithColor(Color.Red);
                        await modal.Channel.SendMessageAsync(embed: embedBuilder2.Build());
                    }


                    await modal.DeferAsync();
                }
                else
                {
                    if (duplicateError.Any() || rangeError.Any() || formatError.Any() || insufficientError.Any() || limitError.Any())
                    {
                        string errorMessage = "";

                        if (duplicateError.Any())
                        {
                            errorMessage += "- 숫자 중복 입력\n" + string.Join("\n", duplicateError) + "\n\n";
                        }
                        if (rangeError.Any())
                        {
                            errorMessage += "- 잘못된 범위 입력\n" + string.Join("\n", rangeError) + "\n\n";
                        }
                        if (formatError.Any())
                        {
                            errorMessage += "- 잘못된 형식의 입력\n" + string.Join("\n", formatError) + "\n\n";
                        }
                        if (insufficientError.Any())
                        {
                            errorMessage += "- 사용자 잔액 부족\n" + string.Join("\n", insufficientError) + "\n\n";
                        }
                        if (limitError.Any())
                        {
                            errorMessage += "- 잔액 부족\n" + string.Join("\n", limitError) + "\n\n";
                        }
                        var embedBuilder = new EmbedBuilder()
                            .WithTitle("로또 구매에 실패했습니다.")
                            .WithDescription(errorMessage)
                            .WithColor(Color.Red);

                        await modal.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
                    }
                    else
                    {
                        await modal.RespondAsync($"{modal.User.Mention} 로또 구매에 실패했습니다.", ephemeral: true);
                    }
                }                
            }
            if(modal.Data.CustomId == "nethorslot_modal")
            {
                var countInput = modal.Data.Components.First(x => x.CustomId == "count_input").Value;
               
                        if(int.TryParse(countInput, out int count))
                        {
                            if(count > 0 && count <= 5)
                            {
                                // 슬롯머신 실행
                                var channel = modal.Channel as ITextChannel;
                                if (channel != null)
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        var guild = channel.Guild as SocketGuild;
                                        var slotMachineManager = new SlotMachineManager();
                                        await modal.DeferAsync();
                                        var (isSuccess, message) = await slotMachineManager.RunSlotMachine(modal.User, channel, 1000, count, true);

                                        if (!isSuccess)
                                        {
                                            // 문제가 발생했을 때만 메시지를 보냄
                                            await channel.SendMessageAsync($"{modal.User.Mention} {message}");
                                        }

                                    });
                                }
                                else
                                {
                                    await modal.RespondAsync("슬롯머신 작동에 실패했습니다. 채널 정보를 가져올 수 없습니다.", ephemeral: true);
                                }
                            }
                            else
                            {
                                await modal.RespondAsync("이용 횟수는 1 ~ 5 사이의 값이여야 합니다. 다시 입력해주세요.", ephemeral: true);
                            }
                        }                        

                

            }
            if (modal.Data.CustomId == "skipslot_modal")
            {
                var countInput = modal.Data.Components.First(x => x.CustomId == "count_input").Value;
                var priceInput = modal.Data.Components.First(x => x.CustomId == "price_input").Value;
                
                if (int.TryParse(countInput, out int count) && int.TryParse(priceInput, out int price))
                {
                    if (count > 9 && count <= 200)
                    {
                        if((price > 9 && price <= 100) || price == 1000)
                        {
                            double userCoin = await _dbManager.GetUserSlotCoinAsync(modal.User.Id);

                            int totalPrice = count * price;
                            if (totalPrice > userCoin)
                            {
                                await modal.RespondAsync($"{price:N0}코인으로 {count}번 이용할 잔액이 부족합니다. 현재 잔액 {userCoin:N0}", ephemeral: true);
                            }
                            else
                            {
                                await modal.DeferAsync();
                                var slotMachineManager = new SlotMachineManager();
                                await slotMachineManager.SkipSlotMachine(modal.User, modal.Channel as ITextChannel, price, count);
                            }
                        }
                        else
                        {
                            await modal.RespondAsync("금액은 10 ~ 100이거나 1000을 입력해야 합니다. 다시 입력해주세요.", ephemeral: true);
                        }
                    }
                    else
                    {
                        await modal.RespondAsync("이용 횟수는 10 ~ 200 사이의 값이여야 합니다. 다시 입력해주세요.", ephemeral: true);
                    }
                }
                else
                {
                    await modal.RespondAsync("숫자를 입력해주세요.", ephemeral: true);
                }
            }
                
            if (modal.Data.CustomId == "simpleslot_modal")
            {
                var numberInput = modal.Data.Components.First(x => x.CustomId == "number_input").Value;
                var countInput = modal.Data.Components.First(x => x.CustomId == "count_input").Value;

                if (int.TryParse(numberInput, out int amount))
                {
                    if ((amount >= 10 && amount <= 100) || amount == 1000)
                    {
                        if (int.TryParse(countInput, out int count))
                        {
                            if (count > 0 && count <= 10)
                            {
                                // 슬롯머신 실행
                                var channel = modal.Channel as ITextChannel;
                                if (channel != null)
                                {
                                    _ = Task.Run(async () =>
                                    {
                                        var guild = channel.Guild as SocketGuild;
                                        var slotMachineManager = new SlotMachineManager();
                                        await modal.DeferAsync();
                                        var (isSuccess, message) = await slotMachineManager.RunSlotMachine(modal.User, channel, amount, count, false);

                                        if (!isSuccess)
                                        {
                                            // 문제가 발생했을 때만 메시지를 보냄
                                            await channel.SendMessageAsync($"{modal.User.Mention} {message}");
                                        }

                                    });
                                }
                                else
                                {
                                    await modal.RespondAsync("슬롯머신 작동에 실패했습니다. 채널 정보를 가져올 수 없습니다.", ephemeral: true);
                                }
                            }
                            else
                            {
                                await modal.RespondAsync("이용 횟수는 1 ~ 10 사이의 값이여야 합니다. 다시 입력해주세요.", ephemeral: true);
                            }
                        }
                    }
                    else
                    {
                        await modal.RespondAsync("금액은 10 ~ 100 사이의 값이거나 1000이여야 합니다. 다시 입력해주세요.", ephemeral: true);
                    }
                }
                else
                {
                    await modal.RespondAsync("올바른 숫자를 입력해주세요.", ephemeral: true);
                }

            }            

            if (modal.Data.CustomId == "dollarloan_modal")
            {
                var numberInput = modal.Data.Components.First(x => x.CustomId == "amount_input").Value;
                var agree = modal.Data.Components.First(x => x.CustomId == "agree_input").Value;


                if (int.TryParse(numberInput, out int amount))
                {
                    if (amount >= 1000 && amount <= dollarLimit)
                    {
                        if (!agree.Equals("동의", StringComparison.OrdinalIgnoreCase))
                        {
                            await modal.RespondAsync("안내문구를 확인하고 \'동의\'를 입력해주세요.", ephemeral: true);
                        }
                        else
                        {                                                       
                            var (loanAmount, interest, isCoin, date) = await _dbManager.GetTotalRepaymentAmountAsync(modal.User.Id, false);

                            if(loanAmount + amount > dollarLimit)
                            {
                                await modal.RespondAsync($"대출의 한도는 {dollarLimit:N0}달러까지 입니다. 현재 대출원금 : {loanAmount:N2} :dollar:", ephemeral: true);
                                return;
                            }

                            bool isSuccess = await _dbManager.AddDollarAsync(modal.User.Id, amount);
                            if (isSuccess)
                            {
                                DateTime repaymentDate = await _dbManager.RecordLoanAsync(modal.User.Id, amount, 0);// 0은 달러를 대출한다는 뜻
                                if(repaymentDate != DateTime.MinValue)
                                {
                                    await AddLoanData(modal.User.Id, repaymentDate,0);
                                    string repaymentDateString = repaymentDate.ToString("MM - dd HH : mm");
                                    await modal.RespondAsync($"{amount} 달러가 입금되었습니다! 상환 기간은 {repaymentDateString}까지 입니다. 기한내에 대출금을 상환하셔야 합니다. 이자는 매일 원금의 {interestRate}%씩 증가합니다. 기한내에 전액을 갚지 못할 것 같다면 이자만 먼저 갚으시면 기한이 일주일 연기됩니다. 상환 실패시 서버의 채널을 볼 수 없게되고 \'🍀ㅣ풀숲\'채널에서 네잎클로버 수집을 해야 \'🚫\'역할이 해제되고 기존의 역할로 변경되니 주의해주세요. 대출로 인해 문제가 발생했다면 <@{ConfigManager.Config.OwnerId}>에게 dm이나 귓속말에서 말해주세요.", ephemeral: true);
                                }
                                else
                                {
                                    await _dbManager.UseDollarAsync(modal.User.Id, amount);
                                    await modal.Channel.SendMessageAsync($"<@{ConfigManager.Config.OwnerId}>, <@{modal.User.Id}>님의 대출 진행중 저장에 문제 발생");
                                }
                            }
                            else
                            {
                                await modal.RespondAsync("입급 하는 과정에서 문제가 발생했습니다. 나중에 다시 시도해주세요.", ephemeral: true);
                            }
                        }                                               
                    }
                }
                else
                {
                    await modal.RespondAsync("올바른 숫자를 입력해주세요.", ephemeral: true);
                }                
            }
            if (modal.Data.CustomId == "coinloan_modal")
            {
                var numberInput = modal.Data.Components.First(x => x.CustomId == "amount_input").Value;
                var agree = modal.Data.Components.First(x => x.CustomId == "agree_input").Value;


                if (int.TryParse(numberInput, out int amount))
                {
                    if (amount >= 100 && amount <= coinLimit)
                    {
                        if (!agree.Equals("동의", StringComparison.OrdinalIgnoreCase))
                        {
                            await modal.RespondAsync("안내문구를 확인하고 \'동의\'를 입력해주세요.", ephemeral: true);
                        }
                        else
                        {
                            var (loanAmount, interest, isCoin, date) = await _dbManager.GetTotalRepaymentAmountAsync(modal.User.Id, true);

                            if (isCoin && loanAmount > 0)
                            {
                                await modal.RespondAsync("이미 대출을 받은 상태입니다.", ephemeral: true);
                                return;
                            }

                            bool isSuccess = await _dbManager.AddSlotCoinAsync(modal.User.Id, amount);
                            if (isSuccess)
                            {
                                DateTime repaymentDate = await _dbManager.RecordLoanAsync(modal.User.Id, amount, 1);// 1은 코인을 대출한다는 뜻
                                if (repaymentDate != DateTime.MinValue)
                                {
                                    await AddLoanData(modal.User.Id, repaymentDate,1);
                                    string repaymentDateString = repaymentDate.ToString("MM - dd HH : mm");
                                    await modal.RespondAsync($"{amount} 코인이 지급되었습니다! 상환 기간은 {repaymentDateString}까지 입니다. 기한내에 대출금을 상환하셔야 합니다. 이자는 매일 원금의 {interestRate}%씩 증가합니다. 기한내에 전액을 갚지 못할 것 같다면 이자만 먼저 갚으시면 기한이 일주일 연기됩니다. 상환 실패시 서버의 채널을 볼 수 없게되고 \'🍀ㅣ풀숲\'채널에서 네잎클로버 수집을 해야 \'🚫\'역할이 해제되고 기존의 역할로 변경되니 주의해주세요. 대출로 인해 문제가 발생했다면 <@{ConfigManager.Config.OwnerId}>에게 dm이나 귓속말에서 말해주세요.", ephemeral: true);
                                }
                                else
                                {
                                    await _dbManager.UseSlotCoinAsync(modal.User.Id, amount);
                                    await modal.Channel.SendMessageAsync($"<@{ConfigManager.Config.OwnerId}>, <@{modal.User.Id}>님의 대출 진행중 저장에 문제 발생");
                                }
                            }
                            else
                            {
                                await modal.RespondAsync("지급 하는 과정에서 문제가 발생했습니다. 나중에 다시 시도해주세요.", ephemeral: true);
                            }
                        }
                    }
                }
                else
                {
                    await modal.RespondAsync("올바른 숫자를 입력해주세요.", ephemeral: true);
                }
            }
            
            if (modal.Data.CustomId == "repay_modal")
            {
                var numberInput = modal.Data.Components.First(x => x.CustomId == "amount_input").Value;
                bool isCoinRepay = modal.Channel.Id == ConfigManager.Config.LottoChannelId;

                if (int.TryParse(numberInput, out int amount))
                {                 
                    var (isSuccess, message) = await _dbManager.ProcessRepaymentAsync(modal.User.Id, amount, isCoinRepay);

                    if (isSuccess)
                    {
                        await modal.RespondAsync($"{message}이 완료 되었습니다!", ephemeral: true);
                        if(message.Equals("대출금 상환"))
                        {
                            RemoveLoanData(modal.User.Id, isCoinRepay ? 1 : 0);
                        }
                    }
                    else
                    {
                        if(message.Equals("대출을 하지 않았습니다"))
                        {
                            await modal.RespondAsync($"{message}", ephemeral: true);
                        }
                        else
                        {
                            await modal.RespondAsync($"{message}(으)로 인해 문제가 발생했습니다. 나중에 다시 시도해주세요.", ephemeral: true);
                        }
                        
                    }
                }
                else if (numberInput.Equals("*"))
                {
                    double userBalnace;

                    if (isCoinRepay)
                    {
                        userBalnace = await _dbManager.GetUserSlotCoinAsync(modal.User.Id);
                    }
                    else
                    {
                        userBalnace = await _dbManager.GetUserDollarAsync(modal.User.Id);
                    }
                    
                    var (principal, interest, isCoin, date) = await _dbManager.GetTotalRepaymentAmountAsync(modal.User.Id, isCoinRepay);
                    int totalLoan = principal + interest;

                    double repayAmountDouble = Math.Min(totalLoan, userBalnace);
                    int repayAmount = (int)repayAmountDouble;

                    var (isSuccess, message) = await _dbManager.ProcessRepaymentAsync(modal.User.Id, repayAmount, isCoinRepay);

                    if (isSuccess)
                    {
                        await modal.RespondAsync($"{message}이 완료 되었습니다!", ephemeral: true);
                        if (message.Equals("대출금 상환"))
                        {
                            RemoveLoanData(modal.User.Id, isCoinRepay ? 1 : 0);
                        }
                    }
                    else
                    {
                        if (message.Equals("대출을 하지 않았습니다"))
                        {
                            await modal.RespondAsync($"{message}", ephemeral: true);
                        }
                        else
                        {
                            await modal.RespondAsync($"{message}(으)로 인해 문제가 발생했습니다. 나중에 다시 시도해주세요.", ephemeral: true);
                        }

                    }
                }
                else
                {
                    await modal.RespondAsync("올바른 숫자를 입력해주세요.", ephemeral: true);
                }
            }
            if (modal.Data.CustomId.StartsWith("buycoin_modal_"))
            {
                // CustomId에서 coinId와 coinName을 추출
                var customIdParts = modal.Data.CustomId.Split('_');

                if (customIdParts.Length >= 3 && int.TryParse(customIdParts[2], out int coinId))
                {
                    var priceInput = modal.Data.Components.First(x => x.CustomId == "price_input").Value;

                    // 입력값이 유효한 금액인지 확인
                    if (double.TryParse(priceInput, out double price))
                    {
                        price = Math.Round(price, 2);  // 소수점 2자리로 반올림                       

                        // 코인 이름 가져오기
                        string coinName = await _dbManager.GetCoinNameByIdAsync(coinId);

                        // 코인 구매 로직 처리
                        string result = await _coinManager.BuyCoinAsync(modal.User.Id, coinName, price);

                        // 결과 메시지 응답
                        await modal.RespondAsync(result);
                    }
                    else if (priceInput.Equals("*"))
                    {
                        double userDollar = await _dbManager.GetUserDollarAsync(modal.User.Id);

                        string coinName = await _dbManager.GetCoinNameByIdAsync(coinId);

                        // 코인 구매 로직 처리
                        string result = await _coinManager.BuyCoinAsync(modal.User.Id, coinName, userDollar);

                        // 결과 메시지 응답
                        await modal.RespondAsync(result);
                    }
                    else
                    {
                        await modal.RespondAsync("올바른 값을 입력해주세요.", ephemeral: true);
                    }
                }
                else
                {
                    await modal.RespondAsync("잘못된 코인 ID입니다.", ephemeral: true);
                }
            }
            if (modal.Data.CustomId.StartsWith("sellcoin_modal_"))
            {
                // CustomId에서 coinId와 coinName을 추출
                var customIdParts = modal.Data.CustomId.Split('_');

                if (customIdParts.Length >= 3 && int.TryParse(customIdParts[2], out int coinId))
                {
                    var quantityInput = modal.Data.Components.First(x => x.CustomId == "quantity_input").Value;

                    // 입력값이 유효한 금액인지 확인
                    if (double.TryParse(quantityInput, out double quantity))
                    {
                        quantity = Math.Floor(quantity * 100) / 100;  // 소수점 2자리까지 버림

                        // 코인 이름 가져오기
                        string coinName = await _dbManager.GetCoinNameByIdAsync(coinId);

                        // 코인 구매 로직 처리
                        string result = await _coinManager.SellCoinAsync(modal.User.Id, coinName, quantity.ToString());

                        // 결과 메시지 응답
                        await modal.RespondAsync(result);
                    }
                    else if (quantityInput.Equals("*"))
                    {
                        // 코인 이름 가져오기
                        string coinName = await _dbManager.GetCoinNameByIdAsync(coinId);

                        // 코인 구매 로직 처리 (*는 quantityInput 그대로 넘김)
                        string result = await _coinManager.SellCoinAsync(modal.User.Id, coinName, quantityInput);

                        // 결과 메시지 응답
                        await modal.RespondAsync(result);
                    }
                    else
                    {
                        // quantityInput이 double 변환 불가능하고 "*"도 아닌 경우에 대한 예외 처리
                        await modal.RespondAsync("잘못된 입력입니다.");
                    }
                }
                else
                {
                    await modal.RespondAsync("잘못된 코인 ID입니다.", ephemeral: true);
                }
            }
            if (modal.Data.CustomId.StartsWith("chart_modal"))
            {
                // CustomId에서 coinId와 coinName을 추출
                var coinName = modal.Data.Components.First(x => x.CustomId == "coinname_input").Value;
                var dayInput = modal.Data.Components.First(x => x.CustomId == "day_input").Value;

                if (!int.TryParse(dayInput, out int day))
                {
                    if (dayInput.Equals("*"))
                    {
                        day = 7;
                    }
                    else if (string.IsNullOrWhiteSpace(dayInput))
                    {
                        day = 1;
                    }
                    else
                    {
                        await modal.RespondAsync("출력할 차트의 기간에 1 ~ 7의 숫자를 입력해주세요.", ephemeral: true);
                        return;
                    }                   
                }

                if (day > 0 && day < 8)
                {
                    var (path, result) = await _coinManager.SendCoinPriceChartAsync(modal.Channel, coinName, day);

                    if (path.Equals(""))
                    {
                        await modal.RespondAsync(result, ephemeral: true);
                    }
                    else
                    {
                        await modal.RespondAsync("차트를 생성 중입니다...", ephemeral: true);

                        await modal.Channel.SendFileAsync(path, result);

                        // 이미지 파일 삭제
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                            Console.WriteLine("차트 이미지 파일 삭제 완료");
                        }
                    }
                }
                else
                {
                    await modal.RespondAsync("출력할 차트의 기간에 1 ~ 7의 숫자를 입력해주세요.", ephemeral: true);
                }              
            }
            if (modal.Data.CustomId.StartsWith("autotrade_modal"))
            {
                var coinName = modal.Data.Components.First(x => x.CustomId == "coinname_input").Value;
                var priceInput = modal.Data.Components.First(x => x.CustomId == "price_input").Value;
                var quantityInput = modal.Data.Components.First(x => x.CustomId == "quantity_input").Value;
                var actionInput = modal.Data.Components.First(x => x.CustomId == "action_input").Value;

                if(actionInput.Equals("매수") || actionInput.Equals("매도"))
                {
                    if (double.TryParse(priceInput, out double price))
                    {
                        if (!double.TryParse(quantityInput, out double quantity))
                        {
                            if (!quantityInput.Equals("*"))
                            {
                                await modal.RespondAsync("수량을 올바르게 입력해주세요.", ephemeral:           true);
                                return;
                            }                           
                        }

                        var botCommand = new BotCommands();
                        bool isSuccess = await botCommand.SetAutoTradeCommand(modal.User.Id, coinName, price, quantityInput, actionInput, modal.Channel as ITextChannel);

                        if (isSuccess)
                        {
                            await modal.RespondAsync("자동매매 설정에 성공했습니다!", ephemeral: true);
                        }
                        else
                        {
                            await modal.RespondAsync("자동매매 설정에 실패했습니다 다시 시도해주세요.", ephemeral: true);
                        }
                    }
                    else
                    {
                        await modal.RespondAsync("금액을 올바르게 입력해주세요.", ephemeral: true);
                    }
                }
                else
                {
                    await modal.RespondAsync("\'매수\' 와 \'매도\'만 입력 가능합니다.", ephemeral: true);
                }
            }
            if(modal.Data.CustomId == "feedback_modal")
            {
                ulong userId = modal.User.Id;
                string userName = (modal.User as SocketGuildUser)?.Nickname ?? modal.User.Username;
                string msg = modal.Data.Components.First(x => x.CustomId == "feedback_input").Value;

                await ExceptionManager.SendOwnerMessageAsync(userId, userName, msg);

                await modal.RespondAsync("피드백이 접수되었습니다! 감사합니다!", ephemeral: true);
            }
        }

        public async Task AddLoanData(ulong userId, DateTime loanTime, int isCoin)
        {         
            Bot.loanDataDictionary[userId] = (loanTime, isCoin);
            SaveToJson();

            Console.WriteLine($"저장된 유저 id : {userId}, 저장된 시간 : {Bot.loanDataDictionary[userId]}");

            //try
            //{
            //    var bot = new Bot();
            //    bot.SetLoanTimers();             
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"Error message : AddLoanData의 Bot.Instance.SetLoanTimers 에서 에러 발생");
            //    await ExceptionManager.HandleExceptionAsync(ex);
            //}            
        }

        public void SaveToJson()
        {          
            var json = JsonConvert.SerializeObject(Bot.loanDataDictionary, Formatting.Indented);
            File.WriteAllText(loanFilePath, json);
            Console.WriteLine("대출 데이터 json 저장");
        }

        public void LoadFromJson()
        {
            if (File.Exists(loanFilePath))
            {
                var json = File.ReadAllText(loanFilePath);
                Bot.loanDataDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, (DateTime, int)>>(json)?? new Dictionary<ulong, (DateTime, int)>();

                foreach (var loan in Bot.loanDataDictionary)
                {
                    Console.WriteLine($"저장된 유저 id : {loan.Key}, 저장된 시간 : {loan.Value}");
                }              
                Console.WriteLine("대출 데이터 불러오기");
            }
        }

        public void RemoveLoanData(ulong userId, int isCoin)
        {
            // Dictionary에서 해당 사용자 데이터가 있는지 확인
            if (Bot.loanDataDictionary.TryGetValue(userId, out var loanData))
            {
                // isCoin 값이 일치하는 경우에만 데이터 삭제
                if (loanData.Amount == isCoin)
                {
                    Bot.loanDataDictionary.Remove(userId);
                    SaveToJson();
                    Console.WriteLine($"사용자 {userId}의 데이터가 제거되었습니다.");
                }
            }
            else
            {
                Console.WriteLine($"사용자 {userId}의 데이터를 찾을 수 없습니다.");
            }
        }
    }
}
