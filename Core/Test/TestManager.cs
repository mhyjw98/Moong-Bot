using Discord;
using Discord.WebSocket;
using MoongBot.Core.Manager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Test
{

    public class TestManager
    {
        private readonly LottoManager _lottoManager;
        private readonly DatabaseManager _dbManager;

        public TestManager(LottoManager lottoManager, DatabaseManager dbManager)
        {
            _lottoManager = lottoManager;
            _dbManager = dbManager;
        }
        public static async Task TestExceptionHandlingAsync(IGuild guild, ITextChannel channel)
        {
            try
            {
                // 예외를 강제로 발생시키는 코드
                throw new InvalidOperationException("This is a test exception for verifying the DM error handling.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : TestExceptionHandlingAsync 에서 에러 발생");
                // 발생한 예외를 ExceptionManager로 처리
                await ExceptionManager.HandleExceptionAsync(ex);

                // 테스트 결과를 채널에 메시지로 전송
                await channel.SendMessageAsync("에러 테스트 메시지");
            }
        }

        public static async Task RunRouletteTestAsync(ITextChannel channel)
        {
            var rouletteManager = new RouletteManager();
            var resultCounts = new Dictionary<string, int>();

            // 1000번 룰렛 돌리기
            for (int i = 0; i < 10000; i++)
            {
                var (result, isSuccess) = await rouletteManager.SpinAsync(ConfigManager.Config.OwnerId);
                var formattedResult = FormatTestResult(result);

                if (resultCounts.ContainsKey(formattedResult))
                {
                    resultCounts[formattedResult]++;
                }
                else
                {
                    resultCounts[formattedResult] = 1;
                }
            }

            // 결과 채널 출력
            if (channel != null)
            {
                var embedBuilder = new EmbedBuilder()
                    .WithTitle("룰렛 테스트 결과")
                    .WithColor(Color.Blue);

                foreach (var result in resultCounts)
                {
                    embedBuilder.AddField(result.Key, $"{result.Value}번", inline: true);
                }

                await channel.SendMessageAsync(embed: embedBuilder.Build());
            }
        }

        private static string FormatTestResult(string result)
        {
            // 주요 5개 결과에 대해 간단하게 출력
            if (result.Contains("5 :dollar:"))
            {
                return "5 :dollar:";
            }
            else if (result.Contains("15 :dollar:"))
            {
                return "15 :dollar:";
            }
            else if (result.Contains("45 :dollar:"))
            {
                return "45 :dollar:";
            }
            else if (result.Contains("100 :dollar:"))
            {
                return "100 :dollar:";
            }
            else if (result.Contains("꽝"))
            {
                return "꽝";
            }
            else
            {
                // 나머지 문장은 그대로 반환
                return result;
            }
        }

        public async Task TestLottoProcessAsync(ITextChannel channel)
        {
            // 1. 가상의 유저 생성 및 로또 번호 등록
            ulong testUserId = 123456789012345678; // 가상의 유저 ID
            List<int> testLottoNumbers = new List<int> { 1, 2, 3, 4, 5, 7 }; 

            ulong testUserId2 = 123456789012345928; // 가상의 유저 ID
            List<int> testLottoNumbers2 = new List<int> { 1, 2, 3, 4, 7, 8 }; 

            ulong testUserId3 = 123456789012348228; // 가상의 유저 ID
            List<int> testLottoNumbers3 = new List<int> { 1, 3, 4, 5, 8, 9 }; 

            ulong ownerId = ConfigManager.Config.OwnerId;
            List<int> testLottoOwnerNumbers = new List<int> { 1, 2, 3, 4, 5, 6 };

            // 로또 번호를 등록
            _lottoManager.RegisterUserTicket(testUserId, testLottoNumbers);
            _lottoManager.RegisterUserTicket(testUserId2, testLottoNumbers2);
            _lottoManager.RegisterUserTicket(testUserId3, testLottoNumbers3);
            _lottoManager.RegisterUserTicket(ownerId, testLottoOwnerNumbers);

            // 2. 직접 당첨 번호 설정
            List<int> winningNumbers = new List<int> { 1, 2, 3, 4, 5, 6 }; // 직접 설정한 당첨 번호
            _lottoManager.SetWinningNumbers(winningNumbers); // 당첨 번호 설정 함수 추가 필요

            // 3. 당첨 번호와 유저 로또 번호 비교 및 당첨자 결정
            var (firstPrizeWinners, secondPrizeWinners, thirdPrizeWinners) = await _lottoManager.CheckWinners();

            // 4. 당첨 결과 공지
            await AnnounceLottoResultAsync(winningNumbers, firstPrizeWinners, secondPrizeWinners, thirdPrizeWinners, channel);

            _lottoManager.ResetTestLotto();
        }

        private async Task AnnounceLottoResultAsync(
            List<int> winningNumbers,
            List<ulong> firstPrizeWinners,
            List<ulong> secondPrizeWinners,
            List<ulong> thirdPrizeWinners,
            ITextChannel channel)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle("로또 결과 테스트 발표!")
                .WithColor(new Color(255, 145, 200))
                .WithDescription($"당첨 번호: {string.Join(", ", winningNumbers)}")
                .WithTimestamp(DateTimeOffset.Now);

            if (firstPrizeWinners.Count > 0)
            {
                var firstPrizeMentions = string.Join("\n", firstPrizeWinners.Select(winnerId =>
                {
                    return $"<@{winnerId}>";
                }));
                embedBuilder.AddField("1등 당첨자", firstPrizeMentions);
            }
            else
            {
                embedBuilder.AddField("1등 당첨자 없음", "다음 기회를 노려보세요!");
            }

            if (secondPrizeWinners.Count > 0)
            {
                var secondPrizeMentions = string.Join("\n", secondPrizeWinners.Select(winnerId =>
                {
                    return $"<@{winnerId}>";
                }));
                embedBuilder.AddField("2등 당첨자", secondPrizeMentions);
            }
            else
            {
                embedBuilder.AddField("2등 당첨자 없음", "다음 기회를 노려보세요!");
            }

            if (thirdPrizeWinners.Count > 0)
            {
                var thirdPrizeMentions = string.Join("\n", thirdPrizeWinners.Select(winnerId =>
                {
                    return $"<@{winnerId}>";
                }));
                embedBuilder.AddField("3등 당첨자", thirdPrizeMentions);
            }
            else
            {
                embedBuilder.AddField("3등 당첨자 없음", "다음 기회를 노려보세요!");
            }
          
            await channel.SendMessageAsync(embed: embedBuilder.Build());            
        }

        public async Task TestRunSlotMachineMultipleTimes(ITextChannel channel, SocketGuild guild, int numberOfTests, int input)
        {
            var slotMachineManager = new SlotMachineManager();
            var results = new Dictionary<int, int>();

            for (int i = 0; i < numberOfTests; i++)
            {
                int payout = slotMachineManager.RunSlotMachineForTesting(input);

                if (!results.ContainsKey(payout))
                {
                    results[payout] = 0;
                }
                results[payout]++;
            }

            await SendResultsAsEmbed(channel, results);
        }

        private async Task SendResultsAsEmbed(ITextChannel channel, Dictionary<int, int> results)
        {
            var sortedResults = results.OrderBy(r => r.Key).ToDictionary(r => r.Key, r => r.Value);

            var embed = new EmbedBuilder
            {
                Title = "슬롯 머신 테스트 결과",
                Description = $"총 {results.Values.Sum()}회 작동 결과",
                Color = Color.Green
            };

            foreach (var result in sortedResults)
            {
                embed.AddField($"당첨금: {result.Key}", $"{result.Value}회", true);
            }

            await channel.SendMessageAsync(embed: embed.Build());
        }
    }
}
