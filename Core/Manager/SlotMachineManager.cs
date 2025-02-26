using Discord;
using Discord.Commands;
using Discord.Interactions;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using ScottPlot.Drawing.Colormaps;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public class SlotMachineManager
    {
        private static DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private DatabaseManager dbManager = new DatabaseManager();

        private static readonly string FilePath = Path.Combine("jsonFiles", "pundingUsers.json");
        private static readonly string UsageFilePath = Path.Combine("jsonFiles", "slotUsage.json");
        private readonly Random _random = new Random();
        
        private static HashSet<ulong> _pundingUsers = new HashSet<ulong>();
        private static Dictionary<ulong, int> _slotUsage = new Dictionary<ulong, int>();
        public static Dictionary<ulong, bool> _isPlaying = new Dictionary<ulong, bool>();

        private int pundingCoin = 1000;
        private int pundingDollar = 5000;
        private const int punddingTicket = 20;
        private const int punddingSpecial = 30;
        private const int MaxDailyUses = 200;
        private const int MaxConcurrentPlayers = 2;

        public static bool isStop;

        private List<(string Emoji, int Weight)> _emojis = new List<(string Emoji, int Weight)>
            {
                ("🍒",22),
                ("🍋", 9),
                ("🍉", 9),
                ("🍇", 9),
                ("⭐", 6),
                ("🍀", 3),
                ("🍄", 2)
            };

        public SlotMachineManager()
        {
            LoadSlotUsage();
        }

        private string GetRandomEmoji(int totalWeight)
        {           
            int randomNumber = _random.Next(1, totalWeight + 1);

            int cumulativeWeight = 0;
            foreach (var (emoji, weight) in _emojis)
            {
                cumulativeWeight += weight;
                if (randomNumber <= cumulativeWeight)
                {
                    return emoji;
                }
            }

            throw new InvalidOperationException("Failed to get a random emoji.");
        }

        private void LoadSlotUsage()
        {
            if (File.Exists(UsageFilePath))
            {
                var json = File.ReadAllText(UsageFilePath);
                _slotUsage = JsonConvert.DeserializeObject<Dictionary<ulong, int>>(json) ?? new Dictionary<ulong, int>();
            }
        }
        public void IncrementSlotUsage(ulong userId)
        {
            if (!_slotUsage.ContainsKey(userId))
            {
                _slotUsage[userId] = 0;
            }
            _slotUsage[userId]++;
            SaveSlotUsage();
        }

        public void IncrementLotSlotUsage(ulong userId, int amount)
        {
            if (!_slotUsage.ContainsKey(userId))
            {
                _slotUsage[userId] = -amount;
            }
            else
            {
                _slotUsage[userId] -= amount;
            }
            SaveSlotUsage();
        }
        public bool CanUseSlotMachine(ulong userId)
        {
            if (_slotUsage.ContainsKey(userId) && _slotUsage[userId] >= MaxDailyUses)
            {
                return false;
            }
            return true;
        }
        public int GetUseSlotMachineCount(ulong userId)
        {
            if (_slotUsage.ContainsKey(userId))
            {
                return _slotUsage[userId];
            }
            return 0;
        }

        private static void SaveSlotUsage()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_slotUsage, Formatting.Indented);
                File.WriteAllText(UsageFilePath, json);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void ResetDailyUsage()
        {
            _slotUsage.Clear();
            SaveSlotUsage();
        }       

        public async Task<(bool, string)> RunSlotMachine(IUser user, ITextChannel channel, int input, int number, bool isTicket)
        {
            try
            {
                if (_isPlaying.ContainsKey(user.Id))
                {
                    return (false, "이미 슬롯머신을 사용중입니다.");
                }
                if (_isPlaying.Count >= MaxConcurrentPlayers)
                {
                    return (false, "슬롯머신을 동시에 사용할 수 있는 인원이 초과되었습니다. 잠시 후 다시 시도해주세요.");
                }
                if (isTicket && await dbManager.GetTicketValueAsync(user.Id) == 0)
                {    
                    return (false, "슬롯머신을 이용할 티켓이 없습니다.");
                }               

                _isPlaying[user.Id] = true;
                int totalInput = 0;
                double totalPayout = 0;
                int count = 0;
                var usCulture = new CultureInfo("en-US");
                var guildUser = user as IGuildUser;
                string userNickname = guildUser?.Nickname ?? user.Username;
                ulong userId = user.Id;
                bool hasEnoughMoney = true;
                int specialValue = await dbManager.GetSpecialValueAsync(userId);

                for (int index = 0; index < number; index++)
                {                    
                    // 슬롯머신 사용 횟수 제한 확인
                    if (!CanUseSlotMachine(userId))
                    {
                        break;                        
                    }
                    if (isStop)
                    {
                        isStop = false;
                        break;
                    }
                    hasEnoughMoney = await dbManager.UseSlotCoinAsync(userId, input);
                    if (!hasEnoughMoney)
                    {
                        break;
                    }
                    string notMsg = "";
                    string resultMsg = "";

                    if (isTicket)
                    {
                        await dbManager.UseSlotTicketAsync(userId);
                    }
                    count++;
                    totalInput += input;

                    bool isSpecialActive = await ChangeEmoji(input, isTicket, specialValue, userId);
                    if(specialValue > 0)
                    {
                        specialValue--;
                    }
                    int totalWeight = _emojis.Sum(e => e.Weight);

                    int remainCount = MaxDailyUses - GetUseSlotMachineCount(userId) - 1;
                    notMsg = $"{user.Mention}님이 {input} :coin:를 넣어 슬롯머신을 {count}회 사용중, 오늘 남은 횟수 : {remainCount}회";

                    var startSlot = isTicket ? new[] {"🐹", "🐹", "🐹" } : new[] {"❓", "❓", "❓" };

                    var message = await channel.SendMessageAsync(GetSlotMachineDisplay(startSlot, isSpecialActive, isTicket, input, notMsg, resultMsg));

                    var sequences = new List<string[]>();
                    int slotNumber = 5;
                    for (int i = 0; i < 3; i++)
                    {
                        var sequence = new string[slotNumber];
                        for (int j = 0; j < slotNumber; j++)
                        {
                            sequence[j] = GetRandomEmoji(totalWeight);
                        }
                        sequences.Add(sequence);
                    }

                    var result = new string[3];                   
                    for (int i = 0; i < 3; i++)
                    {
                        result[i] = sequences[i][slotNumber - 1];
                    }

                    if (isTicket)
                    {
                        bool isLose = result[0] != result[1] && result[1] != result[2] && result[0] != result[2];

                        while (isLose)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                result[i] = GetRandomEmoji(totalWeight);
                            }
                            isLose = result[0] != result[1] && result[1] != result[2] && result[0] != result[2];
                        }
                    }

                    await Task.Delay(10);

                    for (int j = 0; j < slotNumber; j++)
                    {
                        var tempResult = new string[3];
                        for (int i = 0; i < 3; i++)
                        {
                            tempResult[i] = sequences[i][j];
                        }

                        await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(tempResult, isSpecialActive, isTicket, input, notMsg, resultMsg));
                    }

                    await Task.Delay(10);
                    await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                    // Calculate the result and update the user's balance if necessary
                    int payout = 0;                 

                    if (input >= 1000)
                    {
                        payout = CalculateHighStakesPayout(result, input, isTicket); // 새로운 배당 로직
                    }
                    else
                    {
                        payout = CalculatePayout(result, input); // 기존 배당 로직
                    }
                                      
                    if (payout != 0)
                    {                       
                        // Check for specific payout scenarios and message accordingly
                        if (result[0] == result[1] && result[1] == result[2])
                        {
                            if (result[0] == "🍄")
                            {
                                resultMsg = $"위험한 독 버섯에 걸려버렸어요 {payout:N0} :coin:";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else if (result[0] == "💣")
                            {
                                resultMsg = $"위험한 폭탄에 걸려버렸어요 {payout:N0} :coin:";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else if (result[0] == "🍀")
                            {
                                resultMsg = $"{user.Mention}님이 🍀 잭팟을 터뜨렸습니다!! 상금 {payout:N0} :coin: 가 지급되었습니다 축하해요!!!";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else if (result[0] == "⭐")
                            {
                                resultMsg = $"⭐ 잭팟을 터뜨렸습니다!! 상금 {payout:N0} :coin: 가 지급되었습니다.";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else
                            {
                                resultMsg = $"축하해요!! {payout:N0} :coin: 가 지급되었어요!! 잭팟!";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                        }
                        else if (result[0] == result[1] || result[1] == result[2] || result[0] == result[2])
                        {
                            if ((result[0] == "🍒" && result[1] == "🍒") || (result[1] == "🍒" && result[2] == "🍒") || (result[0] == "🍒" && result[2] == "🍒") || (result[0] == "🔥" && result[1] == "🔥") || (result[1] == "🔥" && result[2] == "🔥") || (result[0] == "🔥" && result[2] == "🔥"))
                            {
                                resultMsg = $"{payout:N0} :coin: 가 지급되었어요!";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else if ((result[0] == "🍄" && result[1] == "🍄") || (result[1] == "🍄" && result[2] == "🍄") || (result[0] == "🍄" && result[2] == "🍄") || (result[0] == "💣" && result[1] == "💣") || (result[1] == "💣" && result[2] == "💣") || (result[0] == "💣" && result[2] == "💣"))
                            {
                                resultMsg = $"코인을 빼앗겼어요. {payout:N0} :coin:";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else
                            {
                                resultMsg = $"축하해요!! {payout:N0} :coin: 가 지급되었어요!";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            
                        }
                        else
                        {
                            resultMsg = $"다음 기회를 노려보세요! {payout:N0} :coin: 받았어요.";
                            await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                        }
                    }
                    else
                    {
                        resultMsg = "다음 기회를 노려보세요!";
                        await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                    }
                    int bonus = 0;

                    if (result[2].Contains("⭐"))
                    {
                        if (result[0] == result[1]&& result[1] == result[2])
                        {
                            bonus += 3;
                            resultMsg += $"\n⭐ 잭팟이 나와서 보너스 스핀이 {bonus}회 돌아갑니다!";
                            await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                        }
                        else
                        {
                            bonus += 1;
                            resultMsg += $"\n3번 슬롯에 ⭐이 나와서 보너스 스핀이 {bonus}회 돌아갑니다!";
                            await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                        }

                        while (bonus > 0)
                        {                           
                            // 보너스 스핀 실행
                            for (int i = 0; i < 3; i++)
                            {
                                await Task.Delay(10); // 스핀 효과

                                result[i] = GetRandomEmoji(totalWeight);

                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }

                            int bonusPayout = 0;
                            // 보너스 스핀 결과 처리
                            if (input >= 1000)
                            {
                                bonusPayout = CalculateHighStakesPayout(result, input, isTicket);
                            }
                            else
                            {
                                bonusPayout = CalculatePayout(result, input);
                            }
                            payout += bonusPayout;

                            if (bonusPayout > 0)
                            {
                                resultMsg += $"\n보너스 스핀에서 {bonusPayout} :coin: 를 받았어요! 축하해요!";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else if (bonusPayout < 0)
                            {
                                resultMsg += $"\n보너스 스핀에서 {bonusPayout} :coin:을 빼앗겼어요.";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else
                            {
                                resultMsg += $"\n보너스 스핀 결과는 꽝이에요.";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }

                            if (result[0] == result[1] && result[1] == result[2] && result[2].Contains("⭐"))
                            {
                                bonus += 3;
                                resultMsg += $"\n⭐ 잭팟이 나와서 보너스 스핀 3번이 추가됩니다.";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }
                            else if (result[2].Contains("⭐"))
                            {
                                bonus += 1;
                                resultMsg += $"\n3번 슬롯에 ⭐이 나와서 보너스 스핀 1번이 추가됩니다.";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }

                            bonus -= 1; 
                            if(bonus > 0)
                            {
                                resultMsg += $"\n남은 보너스 스핀: {bonus}회";
                                await message.ModifyAsync(msg => msg.Content = GetSlotMachineDisplay(result, isSpecialActive, isTicket, input, notMsg, resultMsg));
                            }                            
                        }
                    }
                   

                    if (isSpecialActive)
                    {
                        _emojis.Add(("🍄", 2));
                    }

                    totalPayout += payout;
                    await dbManager.LogSlotMachineResultAsync(userId, payout - input);
                    await dbManager.AddSlotCoinAsync(userId, payout);
                    IncrementSlotUsage(userId);
                }

                _isPlaying.Remove(userId);  // 사용 완료 후 제거

                
                await dbManager.LogSlotMachineResultAsync(1, totalInput);
                double balance = await dbManager.GetUserSlotCoinAsync(userId);

                if(count > 0)
                {
                    var embed = ShowResultEmbed(userNickname, count, totalInput, totalPayout, balance);

                    await channel.SendMessageAsync(embed: embed.Build());
                    if(number != count)
                    {
                        if (hasEnoughMoney)
                        {
                            await channel.SendMessageAsync("슬롯머신을 이용할 달러가 부족합니다.");
                        }
                        else
                        {
                            await channel.SendMessageAsync($"오늘은 이미 {MaxDailyUses}회를 이용했습니다. 내일 다시 시도해주세요.");
                        }
                    }

                    return (true, "");
                }
                else
                {
                    if (hasEnoughMoney)
                    {
                        return (false, $"오늘은 이미 {MaxDailyUses}회를 이용했습니다. 내일 다시 시도해주세요.");
                    }
                    else
                    {
                        return (false, "슬롯머신을 이용할 달러가 부족합니다.");
                    }
                }                                
            }
            catch (Exception ex)
            {
                _isPlaying.Remove(user.Id);  // 에러 발생 시 슬롯머신 상태 해제
                await dbManager.AddSlotCoinAsync(user.Id, input);
                Console.WriteLine($"Error message : RunSlotMachine 에서 에러 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return (false, "슬롯머신 사용중 문제가 발생했어요. 머신에 사용한 코인은 환불되었어요.");
            }
        }

        public async Task<(bool, string)> SkipSlotMachine(IUser user, ITextChannel channel, int input, int number)
        {
            try
            {
                var results = new Dictionary<int, int>();
                int totalInput = 0;
                int totalPayout = 0;
                int count = 0;                
                var guildUser = user as IGuildUser;
                string userNickname = guildUser?.Nickname ?? user.Username;
                ulong userId = user.Id;
                int specialValue = await dbManager.GetSpecialValueAsync(userId);

                for (int index = 0; index < number; index++)
                {
                    if (!CanUseSlotMachine(userId))
                    {
                        break;
                    }

                    count++;
                    totalInput += input;
                    bool isSpecialActive = false;
                    
                    await ChangeEmoji(input, false, specialValue, userId);
                    if(specialValue > 0)
                    {
                        specialValue--;
                    }

                    int totalWeight = _emojis.Sum(e => e.Weight);

                    var result = new string[3];
                    for (int i = 0; i < 3; i++)
                    {
                        result[i] = GetRandomEmoji(totalWeight);
                    }
                    int payout;
                    if (input >= 1000)
                    {
                        payout = CalculateHighStakesPayout(result, input, false); // 새로운 배당 로직
                    }
                    else
                    {
                        payout = CalculatePayout(result, input); // 기존 배당 로직
                    }                    
                    
                    int bonus = 0;

                    if (result[2].Contains("⭐"))
                    {
                        if (result[0] == result[1] && result[1] == result[2])
                        {
                            bonus += 3;
                        }
                        else
                        {
                            bonus += 1;
                        }

                        while (bonus > 0)
                        {
                            // 보너스 스핀 실행
                            for (int i = 0; i < 3; i++)
                            {
                                result[i] = GetRandomEmoji(totalWeight);
                            }

                            // 보너스 스핀 결과 처리
                            if (input >= 1000)
                            {
                                payout += CalculateHighStakesPayout(result, input, false);
                            }
                            else
                            {
                                payout += CalculatePayout(result, input);
                            }                            

                            if (result[0] == result[1] && result[1] == result[2] && result[2].Contains("⭐"))
                            {
                                bonus += 3;                                
                            }
                            else if (result[2].Contains("⭐"))
                            {
                                bonus += 1;
                            }

                            bonus -= 1;
                        }
                    }

                    int difValue = payout - input;
                    await dbManager.LogSlotMachineResultAsync(userId, difValue);
                                        
                    totalPayout += payout;
                    
                    if (!results.ContainsKey(payout))
                    {
                        results[payout] = 0;
                    }
                    results[payout]++;

                    if (isSpecialActive)
                    {
                        _emojis.Add(("🍄", 2));
                    }

                    IncrementSlotUsage(user.Id);
                }
                int resultPayout = totalPayout - totalInput;
                if(resultPayout > 0)
                {
                    await dbManager.AddSlotCoinAsync(userId, resultPayout);
                }
                else
                {
                    await dbManager.UseSlotCoinAsync(userId, -resultPayout);
                }
                await dbManager.LogSlotMachineResultAsync(1, totalInput);
                
                double balance = await dbManager.GetUserSlotCoinAsync(userId);
                var sortedResults = results.OrderBy(r => r.Key).ToDictionary(r => r.Key, r => r.Value);

                if(count > 0)
                {
                    var embed = ShowResultEmbed(userNickname, count, totalInput, totalPayout, balance);

                    foreach (var result in sortedResults)
                    {
                        embed.AddField($"당첨금: {result.Key}", $"{result.Value}회", true);
                    }

                    embed.WithFooter("슬롯머신 사용 결과");

                    await channel.SendMessageAsync(embed: embed.Build());

                    return (true, "");
                }
                else
                {
                    await channel.SendMessageAsync("오늘은 더이상 슬롯머신을 이용할 수 없어요.");
                }
                return (false, "");
            }
            catch (Exception ex)
            {
                _isPlaying.Remove(user.Id);  // 에러 발생 시 슬롯머신 상태 해제
                await dbManager.AddSlotCoinAsync(user.Id, input);
                Console.WriteLine($"Error message : RunSlotMachine 에서 에러 발생: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return (false, "슬롯머신 사용중 문제가 발생했어요. 머신에 사용한 코인은 환불되었어요.");
            }
        }

        private async Task<bool> ChangeEmoji(int input, bool isTicket, int specialValue, ulong userId)
        {
            if (input == 1000)
            {
                if (isTicket)
                {
                    _emojis = new List<(string Emoji, int Weight)>
                                    {
                                        ("💣",13),
                                        ("💰", 18),
                                        ("💎", 16),
                                        ("🍀", 3),
                                        ("🍄", 11)
                                    };
                }
                else
                {
                    _emojis = new List<(string Emoji, int Weight)>
                                    {
                                        ("🔥", 25),
                                        ("💰", 14),
                                        ("⭐", 11),
                                        ("💎", 6),
                                        ("🍀", 4), // 3 > 4로 수정
                                        ("🍄", 2)
                                    };
                }
            }
            bool isSpecialActive = false;

            if (specialValue > 0 && !isTicket)
            {
                isSpecialActive = true;
                await dbManager.UseSpecialAsync(userId);
                _emojis.RemoveAll(e => e.Emoji == "🍄"); // 버섯 제거
            }

            return isSpecialActive;
        }
        private EmbedBuilder ShowResultEmbed(string userNickname, int count, int totalInput, double totalPayout, double balance)
        {
            var usCulture = new CultureInfo("en-US");
            var embed = new EmbedBuilder
            {
                Title = $"{userNickname} 님의 슬롯머신 {count}회 사용 결과",
                Color = Color.Gold
            };
            embed.AddField("사용한 총 금액", $"{totalInput.ToString("N0")} :coin:");
            embed.AddField("획득한 총 금액", $"{(totalPayout % 1 == 0 ? totalPayout.ToString("N0") : totalPayout.ToString("C2", usCulture))} :coin:");
            embed.AddField("수익", $"{((totalPayout - totalInput) % 1 == 0 ? (totalPayout - totalInput).ToString("N0") : (totalPayout - totalInput).ToString("C2", usCulture))} :coin:");
            embed.AddField("잔액", $"{(balance % 1 == 0 ? balance.ToString("N0") : balance.ToString("C2", usCulture))} :coin:");

            return embed;
        }
        public async Task<(string Rankings, int TotalUsers)> GenerateRankingReportAsync(int page)
        {
            try
            {
                var userRankings = await dbManager.GetUserRankingsAsync();

                if(userRankings == null)
                {
                    return ("",0);
                }

                var rankingsBuilder = new StringBuilder();
                int rank = (page - 1) * 10 + 1;  // 페이지에 따라 시작 순위가 달라짐

                var rankingsPage = userRankings
                    .Skip((page - 1) * 10)  
                    .Take(10)               
                    .ToList();
                int totalUsers = userRankings.Count;

                foreach (var ranking in rankingsPage)
                {
                    int rankAmount = ranking.TotalAmount;
                    string rankerTotalAmount = rankAmount.ToString("N0");

                    rankingsBuilder.AppendLine($"{rank}위 <@{ranking.UserId}> : {rankerTotalAmount} 코인");
                    rank++;
                }

                return (rankingsBuilder.ToString(), totalUsers);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GenerateRankingReportAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return ("", 0);
            }
        }
        public async Task RegistHOFAsync(SocketGuild guild)
        {
            Console.WriteLine("코인 랭킹 1위 기록중...");
            var (userId, coinValue) = await dbManager.GetTopSlotUserAsync();

            var user = guild.GetUser(userId);
            if (user == null)
            {
                Console.WriteLine("사용자를 찾을 수 없습니다.");
            }            
            string userName = user.Nickname ?? user.Username;
            await dbManager.RegistHOFAsync(userName, coinValue);
        }
        public async Task<(string Rankings, int TotalUsers)> GenerateHOFReportAsync(SocketGuild guild, int page)
        {
            try
            {
                var userRankings = await dbManager.GetSlotHOFAsync();

                var rankingsBuilder = new StringBuilder();
                int rank = (page - 1) * 10 + 1;  // 페이지에 따라 시작 순위가 달라짐

                var rankingsPage = userRankings
                    .Skip((page - 1) * 10)
                    .Take(10)
                    .ToList();
                int totalUsers = userRankings.Count;

                foreach (var ranking in rankingsPage)
                {
                    int rankAmount = ranking.TotalAmount;
                    string rankerTotalAmount = rankAmount.ToString("N0");

                    rankingsBuilder.AppendLine($"{rank}. {ranking.UserName} : {rankerTotalAmount} 코인");
                    rank++;
                }

                return (rankingsBuilder.ToString(), totalUsers);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : GenerateRankingReportAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
                return ("", 0);
            }
        }
        private string GetSlotMachineDisplay(string[] slots, bool isSpecialActive, bool isTicket, int input, string notMsg, string resultMsg)
        {
            string title = isTicket ? "햄붕이의 보물창고" : "MoongBot Slot Machine";
            string top   =       "╔════════════════════╗";
            string leverPart1  = isTicket ? $"║　╔═════════╗　　    ║{" 🍔"}" : $"║　╔═════════╗　　    ║{" 🔴"}";
            string leverPart2 = $"║　║{slots[0]}║{slots[1]}║{slots[2]}║     　 ║ ║";
            string leverPart3  = "║　╚═════════╝　　　　 ╠═╝";
            string leverPart4  = "║　　　　　　╔══════╗　║";
            string leverPart5  = "║　　　　　　║　　　 ║　║";
            string leverPart6  = "║　🇲🇴🇴🇳🇬　　║　　　 ║　║";
            string leverPart7  = "║　　　　　　╚══════╝　║";
            string leverPart8 =  "╚════════════════════╝";
            string bottom =      "╔════════════════════╗";

            string decoration = isSpecialActive ? "🔥" : "";
            string decoration2 = isTicket ? "🐹" : "";

            // 슬롯머신 전체 출력 형태
            string slotMachineDisplay = $@"
                    {decoration}{decoration2}{title}{decoration2}{decoration}
                    {top}
                    {leverPart1}
                    {leverPart2}
                    {leverPart3}
                    {leverPart4}
                    {leverPart5}
                    {leverPart6}
                    {leverPart7}
                    {leverPart8}
                    {bottom}";

            return $"{notMsg}\n```\n{slotMachineDisplay}\n```\n{resultMsg}";
        }

        public int CalculatePayout(string[] slots, int betAmount)
        {
            double basePayout = 0.0;

            // 모든 슬롯이 같은 이모지일 경우
            if (slots[0] == slots[1] && slots[1] == slots[2])
            {
                if (slots[0] == "🍒")
                {
                    basePayout = 100;
                }
                else if (slots[0] == "🍀")
                {
                    basePayout = 3000;
                }
                else if (slots[0] == "⭐")
                {
                    basePayout = 1000;
                }
                else if (slots[0] == "🍄")
                {
                    basePayout = -50000;
                }
                else
                {
                    basePayout = 700;
                }
            }

            // 두 개의 슬롯이 같은 이모지일 경우
            else if (slots[0] == slots[1] && slots[1] != slots[2])
            {
                if (slots[0] == "🍒")
                {
                    basePayout = 12.5f;
                }
                else if (slots[0] == "🍀")
                {
                    basePayout = 70;
                }
                else if (slots[0] == "🍋" || slots[0] == "🍉" || slots[0] == "🍇" || slots[0] == "⭐")
                {
                    basePayout = 40;
                }
            }
            else if (slots[1] == slots[2] && slots[1] != slots[0])
            {
                if (slots[1] == "🍒")
                {
                    basePayout = 12.5f;
                }
                else if (slots[1] == "🍀")
                {
                    basePayout = 70;
                }
                else if (slots[1] == "🍋" || slots[1] == "🍉" || slots[1] == "🍇" || slots[1] == "⭐")
                {
                    basePayout = 40;
                }
            }
            else if (slots[0] == slots[2] && slots[0] != slots[1])
            {
                if (slots[0] == "🍒")
                {
                    basePayout = 12.5f;
                }
                else if (slots[0] == "🍀")
                {
                    basePayout = 70;
                }
                else if (slots[0] == "🍋" || slots[0] == "🍉" || slots[0] == "🍇" || slots[0] == "⭐")
                {
                    basePayout = 40;
                }
            }

            double multiplier = 1 + ((betAmount - 10) * 7 / 90.0);
            return (int)Math.Ceiling(basePayout * multiplier);
        }

        public int CalculateHighStakesPayout(string[] slots, int betAmount, bool isTicket = false)
        {
            int basePayout = 0;

            // 모든 슬롯이 같은 이모지일 경우
            if (slots[0] == slots[1] && slots[1] == slots[2])
            {
                if (slots[0] == "🍀")
                {
                    basePayout = 3000000; 
                }
                else if (slots[0] == "💎")
                {
                    if (isTicket)
                    {
                        basePayout = 700000;
                    }
                    else
                    {
                        basePayout = 500000;
                    }
                }
                else if (slots[0] == "⭐")
                {
                    basePayout = 30000; 
                }
                else if (slots[0] == "💰")
                {
                    if (isTicket)
                    {
                        basePayout = 200000;
                    }
                    else
                    {
                        basePayout = 15000;
                    }
                    
                }
                else if (slots[0] == "🔥")
                {
                    basePayout = 6000;
                }
                else if (slots[0] == "🍄")
                {
                    basePayout = -500000;
                }
                else if (slots[0] == "💣")
                {
                    basePayout = -300000;
                }
            }
            // 두개의 슬롯이 같은 그림일 경우
            else if (slots[0] == slots[1] && slots[1] != slots[2])
            {
                if (slots[0] == "🔥")
                {
                    basePayout = 400;
                }
                else if (slots[0] == "🍀")
                {
                    if (isTicket)
                    {
                        basePayout = 10000;
                    }
                    else
                    {
                        basePayout = 4000;
                    }
                }
                else if (slots[0] == "💎")
                {
                    if (isTicket)
                    {
                        basePayout = 5000;
                    }
                    else
                    {
                        basePayout = 2500;
                    }                
                }
                else if(slots[0] == "💰" || slots[0] == "⭐")
                {
                    if (isTicket)
                    {
                        basePayout = 4000;
                    }
                    else
                    {
                        basePayout = 1500;
                    }
                }
                else if (slots[0] == "🍄")
                {
                    basePayout = -1000;                  
                }
                else if (slots[0] == "💣")
                {
                    basePayout = -500;
                }
            }
            else if (slots[1] == slots[2] && slots[1] != slots[0])
            {
                if (slots[1] == "🔥")
                {
                    basePayout = 400;
                }
                else if (slots[1] == "🍀")
                {
                    if (isTicket)
                    {
                        basePayout = 10000;
                    }
                    else
                    {
                        basePayout = 4000;
                    }
                }
                else if (slots[1] == "💎")
                {
                    if (isTicket)
                    {
                        basePayout = 5000;
                    }
                    else
                    {
                        basePayout = 2500;
                    }
                }
                else if (slots[1] == "💰" || slots[1] == "⭐")
                {
                    if (isTicket)
                    {
                        basePayout = 4000;
                    }
                    else
                    {
                        basePayout = 1500;
                    }
                }
                else if (slots[1] == "🍄")
                {
                    basePayout = -1000;
                }
                else if (slots[1] == "💣")
                {
                    basePayout = -500;
                }
            }
            else if (slots[0] == slots[2] && slots[0] != slots[1])
            {
                if (slots[0] == "🔥")
                {
                    basePayout = 400;
                }
                else if (slots[0] == "🍀")
                {
                    if (isTicket)
                    {
                        basePayout = 10000;
                    }
                    else
                    {
                        basePayout = 4000;
                    }
                }
                else if (slots[0] == "💎")
                {
                    if (isTicket)
                    {
                        basePayout = 5000;
                    }
                    else
                    {
                        basePayout = 2500;
                    }
                }
                else if (slots[0] == "💰" || slots[0] == "⭐")
                {
                    if (isTicket)
                    {
                        basePayout = 4000;
                    }
                    else
                    {
                        basePayout = 1500;
                    }                   
                }
                else if (slots[0] == "🍄")
                {
                    basePayout = -1000;
                }
                else if (slots[0] == "💣")
                {
                    basePayout = -500;
                }
            }

            return basePayout;
        }
        public async Task GivePundingForUser(IMessageChannel channel, ulong userId)
        {
            try
            {
                bool isMinus = false;
                if (_pundingUsers.Contains(userId))
                {                    
                    int coin = await dbManager.GetUserSlotCoinAsync(userId);
                    if(coin >= 0)
                    {
                        await channel.SendMessageAsync($"<@{userId}>님은 이미 지원금을 받았습니다!!");
                        return;
                    }
                    isMinus = true;
                }

                // 1일 기준으로 날짜 계산
                DateTime referenceDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime currentDate = DateTime.Now;
                int daysSinceReference = Math.Max((currentDate - referenceDate).Days, 0);

                pundingDollar = pundingDollar + (daysSinceReference * 500);
                pundingCoin = pundingCoin + (daysSinceReference * 500);

                // 지원금 코인, 달러, 티켓, 확률증가권 지급
                await dbManager.AddSlotCoinAsync(userId, pundingCoin);
                await dbManager.AddDollarAsync(userId, pundingDollar);
                if (!isMinus)
                {
                    await dbManager.AddSlotTicketAsync(userId, punddingTicket);
                    await dbManager.AddSpecialAsync(userId, punddingSpecial);
                }
                string message = $"<@{userId}>님에게 지원금 {pundingCoin} :coin: 과 {pundingDollar} :dollar: 를 지급했습니다!";

                if (!isMinus)
                {
                    message += $"\n추가로 도박 슬롯머신에 사용가능한 티켓 {punddingTicket}개와 일반 슬롯머신 확률증가(버섯제거) {punddingSpecial}회를 지급했습니다.";
                }

                await channel.SendMessageAsync(message);

                _pundingUsers.Add(userId);
                await SavePundingUsers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GivePundingForUserAsync: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
            }
        }
        public static async Task<bool> SavePundingUsers()
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_pundingUsers);
                File.WriteAllText(FilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in SavePundingUsers: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }

        public static async Task<bool> DeletePundingUsers()
        {
            _pundingUsers.Clear();
            return await SavePundingUsers();
        }

        public static async void LoadPundingUsers()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    _pundingUsers = System.Text.Json.JsonSerializer.Deserialize<HashSet<ulong>>(json) ?? new HashSet<ulong>();
                }
                else
                {
                    _pundingUsers = new HashSet<ulong>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in LoadPundingUsers: {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                _pundingUsers = new HashSet<ulong>(); // 문제가 발생하면 빈 목록으로 초기화
            }
        }

        //테스트용 함수
        public string[] GetSlotMachineResult(int input)
        {
            var emojis = new List<(string Emoji, int Weight)>();


            emojis = new List<(string Emoji, int Weight)>
                                    {
                                        ("💣",13),
                                        ("💰", 18),
                                        ("💎", 16),
                                        ("🍀", 3),
                                        ("🍄", 11)
                                    };

            var result = new string[3];

            for (int i = 0; i < 3; i++)
            {
                result[i] = GetTestRandomEmoji(emojis); // Get a single emoji for each slot
            }

            bool isLose = result[0] != result[1] && result[1] != result[2] && result[0] != result[2];

            while (isLose)
            {
                for (int i = 0; i < 3; i++)
                {
                    result[i] = GetTestRandomEmoji(emojis);
                }
                isLose = result[0] != result[1] && result[1] != result[2] && result[0] != result[2];
            }

            return result;
        }

        public int RunSlotMachineForTesting(int input)
        {
            var result = GetSlotMachineResult(input);
            int payout = 0;
            if (input >= 1000)
            {
                payout = CalculateHighStakesPayout(result, input, true);
            }
            else
            {
                payout = CalculatePayout(result, input);
            }

            // 보너스 스핀 처리
            if (result[2].Contains("⭐"))
            {
                result = GetSlotMachineResult(input); // 보너스 스핀 결과
                if (input >= 1000)
                {
                    payout = CalculateHighStakesPayout(result, input, true);
                }
                else
                {
                    payout = CalculatePayout(result, input);
                };
            }

            return payout;
        }

        public async Task ResetSlotRecord()
        {
            Console.WriteLine("슬롯 기록 삭제중...");
            bool deleteSlot = await dbManager.DeleteAllSlotMachineResultsAsync();
            bool deleteBalances = await dbManager.ResetCoinBalancesAsync();
            bool deleteItems = await dbManager.ResetSlotItemcesAsync();
            bool deletePundings = await DeletePundingUsers();
            bool isSuccess = deleteSlot && deletePundings && deleteItems && deleteBalances;

            if (deleteSlot)
            {
                Console.WriteLine("슬롯머신 기록 삭제 완료");
            }
            if (deleteBalances)
            {
                Console.WriteLine("사용자 코인 삭제 완료");
            }
            if (deleteItems)
            {
                Console.WriteLine("사용자 슬롯 아이템 삭제 완료");
            }
            if (deletePundings)
            {
                Console.WriteLine("지원금 기록 삭제 완료");
            }
            if (isSuccess)
            {
                Console.WriteLine("초기화 로직 성공");
            }
            else
            {
                Console.WriteLine("초기화 로직중 실패 요소 존재");
            }
        }

        private string GetTestRandomEmoji(List<(string Emoji, int Weight)> _emojis)
        {
            int totalWeight = _emojis.Sum(e => e.Weight);

            int randomNumber = _random.Next(1, totalWeight + 1);

            int cumulativeWeight = 0;
            foreach (var (emoji, weight) in _emojis)
            {
                cumulativeWeight += weight;
                if (randomNumber <= cumulativeWeight)
                {
                    return emoji;
                }
            }

            throw new Exception();
        }
    }
}

