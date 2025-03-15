using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.WebSocket;
using MoongBot.Core.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public static class CommandManager
    {
        private static CommandService _commandService = ServiceManager.GetService<CommandService>();
        private static DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();

        public static async Task LoadCommmandsAsync()
        {
            await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), ServiceManager.Provider);
            foreach (var command in _commandService.Commands)
                Console.WriteLine($"Command {command.Name} was loaded.");
        }
        public static IEnumerable<CommandInfo> GetCommands()
        {
            return _commandService.Commands.ToList();
        }
        public static async Task HelpCommandAsync(IGuild guild, ITextChannel channel)
        {
            try
            {
                var commands = _commandService.Commands
                    .Where(c => !c.Attributes.Any(attr => attr.GetType() == typeof(HiddenAttribute)))
                    .ToList();

                int commandsPerPage = 10;
                int pageCount = (int)Math.Ceiling(commands.Count / (double)commandsPerPage);
                var embedBuilders = new List<EmbedBuilder>();

                for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
                {
                    var embedBuilder = new EmbedBuilder()
                        .WithTitle(_client.CurrentUser.Username)
                        .WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl())
                        .WithColor(new Color(255, 145, 200))
                        .WithDescription("TTS 메세지를 출력해주는 " + _client.CurrentUser.Username + "입니다. " + ConfigManager.Config.TtsPrefix
                        + " \'메시지\' 로 메시지를 TTS로 만들어 재생시켜줍니다. TTS 명령어 사용시 자동으로 음성채널에 연결해 재생합니다." +
                        " 기본 명령어는 " + ConfigManager.Config.Prefix + "입니다.");

                    var commandsToShow = commands.Skip(pageIndex * commandsPerPage).Take(commandsPerPage).ToList();

                    foreach (var c in commandsToShow)
                    {
                        if (c.Name == "뭉")
                        {
                            embedBuilder.AddField(ConfigManager.Config.TtsPrefix, c.Remarks, false);
                        }
                        else
                        {
                            string aliases = string.Join(", ", c.Aliases);
                            embedBuilder.AddField(aliases, c.Remarks, false);
                        }
                    }

                    var footer = new EmbedFooterBuilder()
                        .WithText($"페이지 {pageIndex + 1} / {pageCount}\n사용법 예시: 뭉ㅇ 할 말, -tts 할 말 \n -> \'할 말\'이 TTS로 재생됩니다. (먼저 음성 채널에 입장은 필수\n -날씨, -참여 \n -> -명령어를 사용해 기능을 사용할 수 있습니다.)");
                    embedBuilder.WithFooter(footer);

                    embedBuilders.Add(embedBuilder);
                }

                // Send the first embed and add the buttons
                var initialEmbed = embedBuilders.First();
                var message = await channel.SendMessageAsync(embed: initialEmbed.Build());

                if (pageCount > 1)
                {
                    var previousButton = new ButtonBuilder()
                        .WithLabel("◀️ 이전")
                        .WithCustomId($"previous_page_{0}")
                        .WithStyle(ButtonStyle.Secondary);

                    var nextButton = new ButtonBuilder()
                        .WithLabel("다음 ▶️")
                        .WithCustomId($"next_page_{0}")
                        .WithStyle(ButtonStyle.Secondary);

                    var componentBuilder = new ComponentBuilder()
                        .WithButton(previousButton)
                        .WithButton(nextButton);

                    await message.ModifyAsync(msg => msg.Components = componentBuilder.Build());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : HelpCommandAsync 에서 에러 발생");
                Console.WriteLine($"Error message : {ex.Message}");
            }
        }       

        public static async Task LottoRouletteHelpCommandAsync(IGuild guild, ITextChannel channel)
        {
            try
            {
                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle("로또와 룰렛의 기능을 설명해드립니다!!");
                embedBuilder.WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());
                embedBuilder.WithColor(255, 145, 200);

                string Description = "> **기능 설명** :game_die:\n룰렛, 슬롯머신, 코인 매매를 통해 달러(:dollar:)와 코인(:coin:)을 얻거나 사용할 수 있어요. 띵(:mushroom:)은 로또의 당첨금으로만 수급할 수 있어요.\n\n" +
                     "> **달러(:dollar:) 의 사용처** :moneybag:\n:달러(:dollar:)로 로또와 코인시장에서 코인을 구매 가능해요 달러(:dollar:)로 할 수 있는 컨텐츠는 추가 구현 예정이에요!\n\n" +
                     $"> **코인(:coin:) 의 사용처** :coin:\n코인(:mushroom:) 으로는 슬롯머신을 이용할 수 있어요. **\'{ConfigManager.Config.Prefix}슬롯머신\'** 커맨드를 사용해보세요!\n\n" +
                     $"> **띵(:mushroom:) 의 사용처** :shopping_cart:\n띵(:mushroom:) 으로는 상품을 구매할 수 있어요. **\'{ConfigManager.Config.Prefix}상점\'** 커맨드로 상품을 구매해보세요!\n\n" +
                     "> **초기화 시간** :repeat:\n룰렛은 하루에 한번 돌릴 수 있고 매일 자정에 초기화돼요, 로또는 주에 3장 구매 가능하고 로또 추첨 이후 초기화돼요!\n\n" +
                     "> **로또와 룰렛으로 얻는 상품** :gift:\n룰렛에서는 재미있는 상품을 얻을 수 있고, 로또에서는 탐나는 상품을 얻을 수 있어요.\n\n" +
                     $"> **로또 추첨 시간** :calendar:\n로또 추첨은 매주 금요일 저녁 8시입니다!!";

                embedBuilder.WithDescription(Description);

                var footer = new EmbedFooterBuilder();
                footer.WithText($"\n사용법 예시: \"-룰렛\"으로 룰렛을 돌리고, \"-자동 1~{LottoManager.maxLotto}의 숫자\" or \"-수동\"으로 로또를 구매해보세요.\n\"-내로또\"로 내가 산 로또를 확인하고, \"-잔액\"으로 내가 가진 금액을 확인 가능해요.");
                embedBuilder.WithFooter(footer);

                await channel.SendMessageAsync("", false, embedBuilder.Build());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : LottoRouletteHelpCommandAsync 에서 에러 발생");
            }
        }

        public static async Task LottoRouletteHelpCommandAsync(SocketMessageComponent component, IGuild guild, ITextChannel channel)
        {
            try
            {
                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle("로또와 룰렛의 기능을 설명해드립니다!!");
                embedBuilder.WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());
                embedBuilder.WithColor(255, 145, 200);

                string Description = "> **기능 설명** :game_die:\n룰렛, 슬롯머신, 코인 매매를 통해 달러(:dollar:)와 코인(:coin:)을 얻거나 사용할 수 있어요. 띵(:mushroom:)은 로또의 당첨금으로만 수급할 수 있어요.\n\n" +
                     $"> **달러(:dollar:) 의 사용처** :moneybag:\n:달러(:dollar:)로 로또와 코인시장에서 코인을 구매 가능해요 달러(:dollar:)로 로또 티켓을 구매({ConfigManager.Config.Prefix}자동 10)하거나 스피또(복권)을 구매({ConfigManager.Config.Prefix}복권 5)할 수 있어요.\n\n" +
                     $"> **코인(:coin:) 의 사용처** :coin:\n코인(:mushroom:) 으로는 슬롯머신을 이용할 수 있어요. **\'{ConfigManager.Config.Prefix}슬롯머신\'** 커맨드를 사용해보세요!\n\n" +
                     $"> **띵(:mushroom:) 의 사용처** :shopping_cart:\n띵(:mushroom:) 으로는 상품을 구매할 수 있어요. **\'{ConfigManager.Config.Prefix}상점\'** 커맨드로 상품을 구매해보세요!\n\n" +
                     "> **초기화 시간** :repeat:\n룰렛은 하루에 한번 돌릴 수 있고 매일 자정에 초기화돼요, 로또는 주에 3장 구매 가능하고 로또 추첨 이후 초기화돼요!\n\n" +
                     "> **로또와 룰렛으로 얻는 상품** :gift:\n룰렛에서는 재미있는 상품을 얻을 수 있고, 로또에서는 탐나는 상품을 얻을 수 있어요.\n\n" +
                     $"> **로또 추첨 시간** :calendar:\n로또 추첨은 매주 금요일 저녁 8시입니다!!";

                embedBuilder.WithDescription(Description);

                var footer = new EmbedFooterBuilder();
                footer.WithText($"\n사용법 예시: \"-룰렛\"으로 룰렛을 돌리고, \"-자동 1~{LottoManager.maxLotto}의 숫자\" or \"-수동\"으로 로또를 구매해보세요.\n\"-내로또\"로 내가 산 로또를 확인하고, \"-잔액\"으로 내가 가진 금액을 확인 가능해요.");
                embedBuilder.WithFooter(footer);

                await component.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : LottoRouletteHelpCommandAsync 에서 에러 발생");
                Console.WriteLine($"Error message : {ex.Message}");
            }
        }
        public static async Task CoinHelpCommandAsync(ITextChannel channel)
        {
            try
            {
                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle("코인의 기능을 설명해드립니다!!");
                embedBuilder.WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());
                embedBuilder.WithColor(255, 145, 200);

                string Description = $"> **기능 설명** :coin:\n**\'{ConfigManager.Config.Prefix}코인종목\'** 명령어로 코인의 정보를 확인하고 코인을 매수하거나 매도 할 수 있습니다.**\'{ConfigManager.Config.Prefix}지원금\'** 명령어로 지원금을 받고 시작하세요. 명령어를 사용하거나 버튼을 사용했을때 코인이름 입력은 코인의 첫 두글자만 입력해 간단하게 사용 가능합니다.\n예시 : 지엔에이코인 => 지엔, 데꾸코인 => 데꾸\n\n" +
                     $"> **코인 매수 방법** :shopping_cart:\n**\'{ConfigManager.Config.Prefix}코인종목\'** 명령어를 사용하면 정보창의 하단에 매수 버튼이 출력됩니다. 원하는 코인의 매수 버튼을 누르고 금액을 입력(0.01단위까지 입력)하면, 사용자가 입력한 금액으로 구매 가능한 최대의 코인 수량이 구매 됩니다. 이 때 2%의 수수료가 발생합니다. **\'{ConfigManager.Config.Prefix}매수 코인이름 금액\'** 명령어를 써도 매수 가능합니다. 금액에 **\'\\*\'**을 입력하면 보유 달러로 구매할 수 있는 최대의 수량이 매수됩니다.\n예시 : {ConfigManager.Config.Prefix}매수 데꾸코인 1000.58\n\n" +
                     $"> **코인 매도 방법** :dollar:\n**\'{ConfigManager.Config.Prefix}포트폴리오\'** 명령어를 사용하면 포트폴리오 하단에 사용자가 가지고 있는 코인의 매도 버튼이 출력됩니다. 판매를 원하는 수량을 입력하면 판매가 가능합니다.**\'{ConfigManager.Config.Prefix}매도 코인이름 수량\'** 명령어를 써도 매도 가능합니다. 수량에 **\'\\*\'**을 입력하면 가지고 있는 코인을 전부 판매합니다.\n예시 : {ConfigManager.Config.Prefix}매도 데꾸코인 10.58\n\n" +
                     $"> **차트 출력 방법** :bar_chart:\n**\'{ConfigManager.Config.Prefix}차트 코인이름 출력기간(1 ~ 7)\'** 명령어를 사용하면 해당 코인의 차트가 출력됩니다.기간을 입력하지 않으면 하루치 차트가 출력됩니다.\n예시 {ConfigManager.Config.Prefix}차트 데꾸코인 3\n\n" +
                     $"> **자동매매** :money_with_wings:\n**\'{ConfigManager.Config.Prefix}자동매매 코인이름 금액 수량 매수or매도\'** 명령어를 사용하거나 버튼을 사용하면 설정한 코인이 목표 금액에 도달했을때 자동으로 입력한 수량을 매수하거나 매도합니다.\n예시 : {ConfigManager.Config.Prefix}자동매매 데꾸코인 1234.56 10.12 매수\n\n" +
                     $"> **코인 관련 뉴스** :newspaper:\n다양한 코인 관련 뉴스가 채널에 발송됩니다. 뉴스를 보고 빠르게 코인을 매수하거나 매도해보세요!! **\'{ConfigManager.Config.Prefix}뉴스구독\'** 명령어로 뉴스가 발송될때 멘션으로 알림을 받을 수 있어요. 알림을 원하지 않을때는 **\'{ConfigManager.Config.Prefix}구독취소\'** 명령어로 구독을 취소할 수 있어요.";

                embedBuilder.WithDescription(Description);

                await channel.SendMessageAsync("", false, embedBuilder.Build());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : CoinHelpCommandAsync 에서 에러 발생");
            }
        }
        public static async Task CoinHelpCommandAsync(SocketMessageComponent component, ITextChannel channel)
        {
            try
            {
                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle("호롤코인의 기능을 설명해드립니다!!");
                embedBuilder.WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());
                embedBuilder.WithColor(255, 145, 200);

                string Description = $"> **기능 설명** :coin:\n**\'{ConfigManager.Config.Prefix}코인종목\'** 명령어로 코인의 정보를 확인하고 코인을 매수하거나 매도 할 수 있습니다.**\'{ConfigManager.Config.Prefix}지원금\'** 명령어로 지원금을 받고 시작하세요. 명령어를 사용하거나 버튼을 사용했을때 코인이름 입력은 코인의 첫 두글자만 입력해 간단하게 사용 가능합니다.\n예시 : 지엔에이코인 => 지엔, 데꾸코인 => 데꾸\n\n" +
                     $"> **코인 매수 방법** :shopping_cart:\n**\'{ConfigManager.Config.Prefix}코인종목\'** 명령어를 사용하면 정보창의 하단에 매수 버튼이 출력됩니다. 원하는 코인의 매수 버튼을 누르고 금액을 입력(0.01단위까지 입력)하면, 사용자가 입력한 금액으로 구매 가능한 최대의 코인 수량이 구매 됩니다. 이 때 2%의 수수료가 발생합니다. **\'{ConfigManager.Config.Prefix}매수 코인이름 금액\'** 명령어를 써도 매수 가능합니다. 금액에 **\'\\*\'**을 입력하면 보유 달러로 구매할 수 있는 최대의 수량이 매수됩니다.\n예시 : {ConfigManager.Config.Prefix}매수 데꾸코인 1000.58\n\n" +
                     $"> **코인 매도 방법** :dollar:\n**\'{ConfigManager.Config.Prefix}포트폴리오\'** 명령어를 사용하면 포트폴리오 하단에 사용자가 가지고 있는 코인의 매도 버튼이 출력됩니다. 판매를 원하는 수량을 입력하면 판매가 가능합니다.**\'{ConfigManager.Config.Prefix}매도 코인이름 수량\'** 명령어를 써도 매도 가능합니다. 수량에 **\'\\*\'**을 입력하면 가지고 있는 코인을 전부 판매합니다.\n예시 : {ConfigManager.Config.Prefix}매도 데꾸코인 10.58\n\n" +
                     $"> **차트 출력 방법** :bar_chart:\n**\'{ConfigManager.Config.Prefix}차트 코인이름 출력기간(1 ~ 7)\'** 명령어를 사용하면 해당 코인의 차트가 출력됩니다.기간을 입력하지 않으면 하루치 차트가 출력됩니다.\n예시 {ConfigManager.Config.Prefix}차트 데꾸코인 3\n\n" +
                     $"> **자동매매** :money_with_wings:\n**\'{ConfigManager.Config.Prefix}자동매매 코인이름 금액 수량 매수or매도\'** 명령어를 사용하거나 버튼을 사용하면 설정한 코인이 목표 금액에 도달했을때 자동으로 입력한 수량을 매수하거나 매도합니다.\n예시 : {ConfigManager.Config.Prefix}자동매매 데꾸코인 1234.56 10.12 매수\n\n" +
                     $"> **코인 관련 뉴스** :newspaper:\n다양한 코인 관련 뉴스가 채널에 발송됩니다. 뉴스를 보고 빠르게 코인을 매수하거나 매도해보세요!! **\'{ConfigManager.Config.Prefix}뉴스구독\'** 명령어로 뉴스가 발송될때 멘션으로 알림을 받을 수 있어요. 알림을 원하지 않을때는 **\'{ConfigManager.Config.Prefix}구독취소\'** 명령어로 구독을 취소할 수 있어요.";

                embedBuilder.WithDescription(Description);

                await component.RespondAsync(embed: embedBuilder.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : CoinHelpCommandAsync 에서 에러 발생");
                Console.WriteLine($"Error message : {ex.Message}");
            }
        }
        public static async Task ListCommandAsync(IGuild guild, ITextChannel channel)
        {           
            try
            {
                var cityList = WeatherManager.CityTranslations.Keys.ToList();
                string cities = string.Join(", ", cityList);

                var embedBuilder = new EmbedBuilder();
                embedBuilder.WithTitle($"{ _client.CurrentUser.Username} 날씨 알림");
                embedBuilder.WithThumbnailUrl(_client.CurrentUser.GetAvatarUrl());
                embedBuilder.WithColor(255, 145, 200);

                string Description = "날씨 정보를 받을 수 있는 도시명을 알려드립니다.";

                embedBuilder.WithDescription(Description);

                embedBuilder.AddField("도시 목록", cities);

                var footer = new EmbedFooterBuilder();
                footer.WithText($"\n사용법 예시: {ConfigManager.Config.Prefix}날씨 도시명");
                embedBuilder.WithFooter(footer);

                await channel.SendMessageAsync("", false, embedBuilder.Build());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : ListCommandAsync 에서 에러 발생");
            }
        }
    }
}

