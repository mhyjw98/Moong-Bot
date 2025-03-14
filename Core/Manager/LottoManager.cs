using Discord;
using Discord.WebSocket;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MoongBot.Core.Manager
{
    public class LottoManager
    {
        private static DatabaseManager _dbManager = new DatabaseManager();

        private static readonly string lottoPath = Path.Combine("jsonFiles", "lotto_results.json");
        private static readonly string spitoPath = Path.Combine("jsonFiles", "user_spito.json");
        private static Random _random = new Random();
        public static Dictionary<ulong, List<List<int>>> _userTickets = new Dictionary<ulong, List<List<int>>>();
        public static Dictionary<ulong, int> _userSpito = new Dictionary<ulong, int>();
        private List<int> _winningNumbers;
        public static readonly int maxLotto = 10;
        public static readonly int maxSpito = 5;
        private static readonly int lottoPrice = 1;
        private static readonly int spitoPrice = 1;

        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private static readonly string[] AllEmojis = { "🍒", "🍋", "🍉", "⭐", "💎", "🍀" };
        private static readonly (int Prize, double Probability, string Emoji)[] PrizePool = {
            (10000, 0.15, "🍒"),      // 15%
            (15000, 0.10, "🍋"),    // 10%
            (25000, 0.06, "🍉"),    // 6%
            (100000, 0.03, "⭐"),    // 3%
            (200000, 0.015, "💎"),   // 1.5%
            (1000000, 0.0016, "🍀") // 0.16%
        };
        public void GenerateWinningNumbers()
        {
            _winningNumbers = GenerateTicket(0);
        }

        public static List<int> GenerateTicket(ulong userId)
        {
            List<int> ticket;

            do
            {
                ticket = new List<int>();
                while (ticket.Count < 6)
                {
                    int number = _random.Next(1, 16);
                    if (!ticket.Contains(number))
                        ticket.Add(number);
                }
                ticket.Sort();

            } while (IsDuplicateTicket(userId, ticket));

            return ticket;
        }

        private static bool IsDuplicateTicket(ulong userId, List<int> ticket)
        {
            if (_userTickets.ContainsKey(userId))
            {
                foreach (var existingTicket in _userTickets[userId])
                {
                    if (ticket.SequenceEqual(existingTicket))
                    {
                        return true; 
                    }
                }
            }
            return false; 
        }

        public static async Task<(bool, List<int>, string)> BuyManuallyTicket(ulong userId, string numbers, ITextChannel channel)
        {
            if (!_userTickets.ContainsKey(userId))
                _userTickets[userId] = new List<List<int>>();

            if (_userTickets[userId].Count >= maxLotto)
            {
                return (false, null, $"로또 티켓은 주당 {maxLotto}장까지 구매할 수 있습니다.");
            }
                           
            var numList = numbers.Split(',')
                        .Select(n =>
                        {
                            if (int.TryParse(n.Trim(), out int parsedNumber) && parsedNumber >= 1 && parsedNumber <= 15)
                                return parsedNumber;
                            return -1; // 실패 시 -1로 표시
                        })
                        .ToList();

            if (numList.Count != 6)
            {
                return (false, null, $"로또 번호는 6개의 숫자를 입력해야 합니다.");
            }

            if (numList.Contains(-1))
            {
                return (false, null, $"로또 번호는 1과 15 사이의 숫자여야 합니다.");
            }

            if (numList.Distinct().Count() != numList.Count)
            {
                return (false, null, $"로또 번호의 각 숫자는 중복될 수 없습니다.");
            }

            bool isBuyPos = await _dbManager.UseDollarAsync(userId, lottoPrice);

            if (isBuyPos)
            {
                numList.Sort();
                _userTickets[userId].Add(numList);
                await _dbManager.SaveLottoTicketAsync(userId, numList);
               
                return (true, numList, "");
            }
            else
            {
                await channel.SendMessageAsync();
                return (false, null, $"잔액이 부족해서 구매에 실패했습니다.");
            }
        }

        public static async Task<bool> BuyTicketAsync(ulong userId, int num, ITextChannel channel)
        {
            // 사용자의 로또 티켓 목록 초기화
            if (!_userTickets.ContainsKey(userId))
                _userTickets[userId] = new List<List<int>>();

            // 구매 가능 여부 초기화            
            if (_userTickets[userId].Count >= maxLotto && userId != ConfigManager.Config.OwnerId)
            {
                await channel.SendMessageAsync($"<@{userId}> 스피또는 주에 최대 {maxLotto}장까지만 구매할 수 있습니다.");
                return false;
            }

            double balance = await _dbManager.GetUserDollarAsync(userId);
            int affordableTickets = Math.Min(num, (int)(balance / lottoPrice));
            int availableSlots = maxLotto - _userTickets[userId].Count;

            int purchasableTickets = Math.Min(affordableTickets, availableSlots);

            if (purchasableTickets <= 0)
            {
                await channel.SendMessageAsync($"잔액이 부족합니다. 잔액 : {balance}");
                return false;
            }

            var tickets = new List<List<int>>();
            for (int i = 0; i < purchasableTickets; i++)
            {
                bool isSuccess = await _dbManager.UseDollarAsync(userId, lottoPrice);
                if (!isSuccess)
                {
                    break;
                }

                var ticket = GenerateTicket(userId);
                tickets.Add(ticket);
                _userTickets[userId].Add(ticket);
                await _dbManager.SaveLottoTicketAsync(userId, ticket);
            }

            // 메시지 생성
            string title = $"로또를 {purchasableTickets}장 구매했습니다!";
            string description = $"남은 금액: {balance - purchasableTickets * lottoPrice:N2} :dollar:\n\n" +
                                 string.Join("\n", tickets.Select(ticket => $"번호: {string.Join(", ", ticket)}"));

            if (purchasableTickets < num)
            {
                if (purchasableTickets == availableSlots)
                    title = $"로또는 주당 최대 {maxLotto}장만 구매할 수 있습니다. {purchasableTickets}장만 구매되었습니다.";
                else if (purchasableTickets == affordableTickets)
                    title = $"잔액이 부족하여 {purchasableTickets}장만 구매되었습니다.";
            }

            await SendEmbedAsync(channel, userId, title, description);
            return purchasableTickets > 0;
        }

        private static async Task SendEmbedAsync(ITextChannel channel, ulong userId, string title, string? description)
        {
            var embedBuilder = new EmbedBuilder()
                .WithTitle(title)
                .WithDescription(description ?? "")
                .WithColor(new Color(255, 145, 200));

            await channel.SendMessageAsync($"<@{userId}>", false, embedBuilder.Build());
        }

        public async Task<(List<ulong> firstPrizeWinners, List<ulong> secondPrizeWinners, List<ulong> thirdPrizeWinners)> CheckWinners(IMessageChannel channel)
        {
            List<ulong> firstPrizeWinners = new List<ulong>();
            List<ulong> secondPrizeWinners = new List<ulong>();
            List<ulong> thirdPrizeWinners = new List<ulong>();

            foreach (var entry in _userTickets)
            {
                foreach (var ticket in entry.Value)
                {
                    int matchCount = ticket.Intersect(_winningNumbers).Count();

                    if (matchCount == 6)
                    {
                        if (!firstPrizeWinners.Contains(entry.Key))
                        {
                            firstPrizeWinners.Add(entry.Key);
                        }
                    }
                    else if (matchCount == 5)
                    {
                        if (!secondPrizeWinners.Contains(entry.Key))
                        {
                            secondPrizeWinners.Add(entry.Key);
                        }
                    }
                    else if (matchCount == 4)
                    {
                        if (!thirdPrizeWinners.Contains(entry.Key))
                        {
                            thirdPrizeWinners.Add(entry.Key);
                        }
                    }
                }
            }

            // 로또를 산 유저들 멘션
            StringBuilder messageBuilder = new StringBuilder("");

            foreach (var userId in _userTickets.Keys)
            {
                messageBuilder.Append($"<@{userId}> ");
            }
            var messageContent = messageBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(messageContent))
            {
                await channel.SendMessageAsync(messageContent);
            }

            await ResetLotto();
            return (firstPrizeWinners, secondPrizeWinners, thirdPrizeWinners);
        }

        public List<int> GetWinningNumbers()
        {
            return _winningNumbers;
        }

        public static async Task ResetLotto()
        {
            await _dbManager.DeleteAllLottoTicketsAsync();
            _userTickets.Clear();
            _userSpito.Clear();
        }

        public void SetWinningNumbers(List<int> winningNumbers)
        {
            if (winningNumbers.Count != 6 || winningNumbers.Any(n => n < 1 || n > 15))
            {
                throw new ArgumentException("당첨 번호는 1에서 15 사이의 6개의 숫자여야 합니다.");
            }

            _winningNumbers = new List<int>(winningNumbers);
        }

        public void RegisterUserTicket(ulong userId, List<int> ticketNumbers)
        {
            if (ticketNumbers.Count != 6 || ticketNumbers.Any(n => n < 1 || n > 15))
            {
                throw new ArgumentException("로또 번호는 1에서 15 사이의 6개의 숫자여야 합니다.");
            }

            // 유저가 처음 등록하는 경우 딕셔너리에 추가
            if (!_userTickets.ContainsKey(userId))
            {
                _userTickets[userId] = new List<List<int>>();
            }

            // 유저의 티켓 목록에 새로운 티켓 추가
            _userTickets[userId].Add(new List<int>(ticketNumbers));
        }

        // 추후수정
        public static async Task ShowLottoTicketsAsync(ulong userId, IMessageChannel channel, SocketGuildUser user)
        {
            var lottoResults = await LoadLottoResultsFromFileAsync();

            var winningNumbers = lottoResults.WinningNumbers.ToObject<List<int>>();
            var firstPrizeWinners = lottoResults.FirstPrizeWinners.ToObject<List<ulong>>();
            var secondPrizeWinners = lottoResults.SecondPrizeWinners.ToObject<List<ulong>>();
            var thirdPrizeWinners = lottoResults.ThirdPrizeWinners.ToObject<List<ulong>>();

            // 여러 당첨 결과를 저장
            var userName = user.Nickname ?? user.Username;
            var userResults = new List<string>();

            if (thirdPrizeWinners.Contains(userId)) userResults.Add("🥉 3등 당첨");
            if (secondPrizeWinners.Contains(userId)) userResults.Add("🥈 2등 당첨");
            if (firstPrizeWinners.Contains(userId)) userResults.Add("🎉 1등 당첨");

            string userResultText = userResults.Count > 0 ? string.Join(", ", userResults) : "낙첨";

            // 기본 설명 생성
            var description = $"지난 회차 당첨 번호: {string.Join(", ", winningNumbers)}\n{userName} 님은 **\'{userResultText}\'**입니다";

            if (!_userTickets.ContainsKey(userId))
            {
                description += $"\n\n{userName} 님은 구매한 로또가 없습니다.";
            }

            var embedBuilder = new EmbedBuilder()
                .WithTitle($"🎟️ {userName}님의 로또 티켓 🎟️")
                .WithColor(new Color(255, 145, 200))
                .WithDescription(description);

            if (_userTickets.ContainsKey(userId))
            {
                var tickets = _userTickets[userId];
                int ticketNumber = 1;

                foreach (var ticket in tickets)
                {
                    embedBuilder.AddField($"{ticketNumber}번 로또 : ", string.Join(", ", ticket), inline: false);
                    ticketNumber++;
                }
            }
            
            await channel.SendMessageAsync($"<@{userId}>", embed: embedBuilder.Build());
        }
        public void ResetTestLotto()
        {
            _winningNumbers.Clear(); // 당첨 번호 초기화
            _userTickets.Clear();    // 유저 티켓 초기화
            SaveUsersSpitoAsync();
        }

        public static async Task<dynamic> LoadLottoResultsFromFileAsync()
        {
            if (File.Exists(lottoPath))
            {
                var json = await File.ReadAllTextAsync(lottoPath);
                var lottoResults = JsonConvert.DeserializeObject<dynamic>(json);
                return lottoResults;
            }
            return null;
        }
        public static async Task<bool> BuySpitoAsync(ulong userId, int number, ITextChannel channel)
        {
            if (!_userSpito.ContainsKey(userId))
                _userSpito[userId] = 0;

            bool isOwner = ConfigManager.Config.OwnerId == userId;
            // 구매 가능 여부 초기화            
            if (_userSpito[userId] >= maxSpito && isOwner)
            {
                await channel.SendMessageAsync($"<@{userId}> 스피또는 주에 최대 {maxSpito}장까지만 구매할 수 있습니다.");
                return false;
            }

            double balance = await _dbManager.GetUserDollarAsync(userId);
            int affordableTickets = Math.Min(number, (int)(balance / spitoPrice));
            int availableSlots = maxSpito - _userSpito[userId];

            int purchasableTickets = Math.Min(affordableTickets, availableSlots);

            if (purchasableTickets <= 0)
            {
                await channel.SendMessageAsync($"잔액이 부족합니다. 잔액 : {balance}");
                return false;
            }

            string spaceL = "\u2003\u2003\u2003";
            string spaceM = "\u2003\u2003";
            string spaceS = "\u2003";

            ulong winningResult = 0;

            if(number != purchasableTickets)
            {
                await channel.SendMessageAsync($"<@{userId}> 이미 {maxSpito - purchasableTickets}장 구매하여 {purchasableTickets}장만 구매됩니다.");
                await Task.Delay(2000);
            }

            for (int n = 0; n < purchasableTickets; n++)
            {
                var results = new List<(string, string, int, bool)>();
                int totalWinnings = 0;
                string description = "";

                foreach (var (prize, probability, emoji) in PrizePool)
                {
                    bool isWinning = _random.NextDouble() < probability;

                    string emoji1, emoji2;
                    if (isWinning)
                    {
                        emoji1 = emoji;
                        emoji2 = emoji;
                        totalWinnings += prize;
                    }
                    else
                    {
                        var nonWinningEmojis = AllEmojis.Where(e => e != emoji).ToArray();
                        emoji1 = nonWinningEmojis[_random.Next(nonWinningEmojis.Length)];
                        emoji2 = AllEmojis[_random.Next(AllEmojis.Length)];
                    }

                    results.Add((emoji1, emoji2, prize, isWinning));
                }

                // 첫 줄: 번호 및 이모지
                for (int i = 0; i < results.Count; i += 3) // 3개씩 한 줄에 출력
                {
                    for (int j = 0; j < 3; j++)
                    {
                        if (i + j < results.Count)
                            description += $"[{i + j + 1}] ({AllEmojis[i + j]}){spaceL}";
                    }
                    description += "\n";

                    // 두 번째 줄: 이모지 2개 (스포일러 처리)
                    for (int j = 0; j < 3; j++)
                    {
                        if (i + j < results.Count)
                        {
                            var (emoji1, emoji2, _, _) = results[i + j];
                            description += $"|| {emoji1} ||{spaceS}|| {emoji2} ||{spaceM}";
                        }
                    }
                    description += "\n";

                    // 세 번째 줄: 당첨 금액
                    for (int j = 0; j < 3; j++)
                    {
                        if (i + j < results.Count)
                        {
                            var (_, _, prize, _) = results[i + j];
                            description += $"{prize:N0} 🍄{spaceM}";
                        }
                    }
                    description += "\n";

                    // 네 번째 줄: 당첨 여부
                    for (int j = 0; j < 3; j++)
                    {
                        if (i + j < results.Count)
                        {
                            var (_, _, _, isWinning) = results[i + j];
                            string winningText = isWinning ? "(당첨)" : "(꽝)";
                            if (isWinning)
                                description += $"||{winningText}||{spaceL}{spaceS}";
                            else
                                description += $"||{winningText}{spaceS}||{spaceL}{spaceS}";
                        }
                    }
                    description += "\n\n";
                }
                string result = totalWinnings.ToString("N0") + "🍄";

                if (totalWinnings < 1000000)
                    result += spaceS;
                if (totalWinnings < 100000)
                    result += spaceS;
                if (totalWinnings == 0)
                    result += spaceM;

                description += $"\n🔍 클릭해서 결과를 확인하세요!";
                description += $"\n🎉 총 당첨 금액: || **{result}** ||";

                var embed = new EmbedBuilder()
                    .WithTitle(":tickets: 호롤로 스피또 :tickets: ")
                    .WithDescription(description)
                    .WithColor(Color.Gold)
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                winningResult += (ulong)totalWinnings;
                _userSpito[userId]++;               
            }
            await _dbManager.AddDdingAsync(userId, winningResult);
            SaveUsersSpitoAsync();
            return true;
        }
        public static void SaveUsersSpitoAsync()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_userSpito, Formatting.Indented);
                File.WriteAllText(spitoPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        public static async Task LoadUsersSpito()
        {
            if (File.Exists(spitoPath))
            {
                await _fileLock.WaitAsync();
                try
                {
                    var json = File.ReadAllText(spitoPath);
                    _userSpito = JsonConvert.DeserializeObject<Dictionary<ulong, int>>(json) ?? new Dictionary<ulong, int>();
                }
                finally
                {
                    _fileLock.Release();
                }
            }
        }            
    }
}
