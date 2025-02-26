using Discord;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public class RouletteManager
    {
        private readonly DatabaseManager _dbManager = new DatabaseManager();
        private readonly SlotMachineManager _slotManager = new SlotMachineManager();

        private readonly List<RouletteItem<string>> _items;
        private readonly Random _random;
        private int _totalWeight;

        private static HashSet<ulong> _dailyRouletteUsers = new HashSet<ulong>();
        private static readonly string FilePath = Path.Combine("jsonFiles", "dailyRouletteUsers.json");
        private static readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        public record RouletteItem<T>(T Item, int Weight);

        public RouletteManager()
        {
            _items = new List<RouletteItem<string>>();
            _random = new Random();
            InitializeItems();
        }

        private void InitializeItems()
        {
            var items = new (string, int)[]
            {
                ("축하해요!! 1000 :coin:, :dollar:에 당첨되었어요!", 600),
                ("축하해요!! 2000 :coin:, :dollar:에 당첨되었어요!", 400),
                ("축하해요!! 5000:coin:, :dollar:에 당첨되었어요!", 200),
                ("축하해요!! 50000 :coin:, :dollar:에 당첨되었어요!", 50),
                ("축하해요!! 100000 :coin:, :dollar:에 당첨되었어요!", 20),
                ("슬롯머신에 있는 :mushroom:이 튀어나왔어요! 이걸로 상품은 못사지만 슬롯머신을 돌릴 때 20번 버섯이 나오지 않아요!(당첨확률 UP) 머신을 돌릴 1000 :coin:도 같이 증정", 500),
                ("슬롯머신에 있는 :mushroom:이 튀어나왔어요! 이걸로 상품은 못사지만 슬롯머신을 돌릴 때 25번 버섯이 나오지 않아요!(당첨확률 UP) 머신을 돌릴 1500 :coin:도 같이 증정", 250),
                ("슬롯머신에 있는 :mushroom:이 튀어나왔어요! 이걸로 상품은 못사지만 슬롯머신을 돌릴 때 30번 버섯이 나오지 않아요!(당첨확률 UP) 머신을 돌릴 2000 :coin:도 같이 증정", 150),
                ("슬롯머신에 있는 :mushroom:이 튀어나왔어요! 이걸로 상품은 못사지만 슬롯머신을 돌릴 때 40번 버섯이 나오지 않아요!(당첨확률 UP) 머신을 돌릴 3000 :coin:도 같이 증정", 80),
                ("슬롯머신에 있는 :mushroom:이 튀어나왔어요! 이걸로 상품은 못사지만 슬롯머신을 돌릴 때 50번 버섯이 나오지 않아요!(당첨확률 UP) 머신을 돌릴 3000 :coin:도 같이 증정", 30),
                ("슬롯머신에 사용가능한 티켓을 7개 획득했어요.", 300),
                ("슬롯머신에 사용가능한 티켓을 15개 획득했어요.", 150),
                ("슬롯머신에 사용가능한 티켓을 20개 획득했어요.", 50),
                ("슬롯머신의 이용 제한 횟수가 30회 늘어났어요.", 400),
                ("슬롯머신의 이용 제한 횟수가 50회 늘어났어요.", 350),
                ("슬롯머신의 이용 제한 횟수가 70회 늘어났어요.", 180),
                ("슬롯머신의 이용 제한 횟수가 90회 늘어났어요.", 80),
                ("슬롯머신의 이용 제한 횟수가 150회 늘어났어요.", 30),
                ("당첨되었으나 만쥬가 보상을 훔쳐갔어요.", 20),
                ("당첨되었으나 띵또가 보상을 먹어버렸어요.", 20),
                ("당첨되었으나 햄붕이가 볼에 넣어 도망쳤어요.", 20),
                ("당첨되었으나 고양이가 가져갔어요.", 15),
                ("축하해요!! 극악의 확률을 뚫고 당첨되었어요!! 기분이 좋아졌어요.", 10),
                ("축하해요!! 햄붕이가 그림을 그려주는 상상을 해준대요!", 10),
                ("축하해요!! 데꾸가 그림을 그려주는 상상을 해준대요!", 10)
            };

            foreach (var (message, weight) in items)
            {
                AddItem(message, weight);
            }
        }

        private void AddItem(string item, int weight)
        {
            _items.Add(new RouletteItem<string>(item, weight));
            _totalWeight += weight;
        }

        public async Task<(string message, bool isSuccess)> SpinAsync(ulong userId)
        {
            if (_dailyRouletteUsers.Contains(userId))
            {
                return ("오늘은 이미 룰렛을 돌렸어요. 내일 다시 도전하세요!!", false);
            }

            int randomValue = _random.Next(_totalWeight);
            int cumulativeWeight = 0;

            foreach (var item in _items)
            {
                cumulativeWeight += item.Weight;
                if (randomValue < cumulativeWeight)
                {
                    _dailyRouletteUsers.Add(userId);
                    await SaveDailyRouletteUsers();

                    string resultMessage = await ProcessRewardAsync(item.Item, userId);
                    return (resultMessage, true);
                }
            }
            return (_items[0].Item, true);
        }

        private async Task<string> ProcessRewardAsync(string resultMessage, ulong userId)
        {
            if (resultMessage.Contains(":coin:"))
            {
                int coinAmount = ExtractAmount(resultMessage, @"(\d+)\s*:coin:");
                await _dbManager.AddSlotCoinAsync(userId, coinAmount);
                await _dbManager.AddDollarAsync(userId, coinAmount);

                var (_, coin, dollar) = await _dbManager.GetAllBalanceAsync(userId);

                resultMessage += $" (현재 보유 :dollar: : {dollar:N2}, 현재 보유 :coin: : {coin:N0})";
            }
            if (resultMessage.Contains(":mushroom:"))
            {
                int specialAmount = ExtractAmount(resultMessage, @"(\d+)\s*번");
                await _dbManager.AddSpecialAsync(userId, specialAmount);
                resultMessage += $" (슬롯머신에서 버섯 제거 : {specialAmount}회)";
            }
            if (resultMessage.Contains("티켓"))
            {
                int ticketAmount = ExtractAmount(resultMessage, @"(\d+)\s*개");
                await _dbManager.AddSlotTicketAsync(userId, ticketAmount);
            }
            if (resultMessage.Contains("제한"))
            {
                int slotAmount = ExtractAmount(resultMessage, @"(\d+)\s*회");
                _slotManager.IncrementLotSlotUsage(userId, slotAmount);
            }

            return resultMessage;
        }

        private int ExtractAmount(string message, string pattern)
        {
            var match = Regex.Match(message, pattern);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        public static async Task ResetDailySpins()
        {
            _dailyRouletteUsers.Clear();
            await SaveDailyRouletteUsers();
        }

        public static async Task SaveDailyRouletteUsers()
        {
            await _fileLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(_dailyRouletteUsers, JsonOptions);
                await File.WriteAllTextAsync(FilePath, json);
            }
            finally
            {
                _fileLock.Release();
            }
        }

        public static async Task LoadDailyRouletteUsers()
        {
            if (File.Exists(FilePath))
            {
                await _fileLock.WaitAsync();
                try
                {
                    var json = await File.ReadAllTextAsync(FilePath);
                    _dailyRouletteUsers = JsonSerializer.Deserialize<HashSet<ulong>>(json) ?? new HashSet<ulong>();
                }
                finally
                {
                    _fileLock.Release();
                }
            }
        }
    }    
}