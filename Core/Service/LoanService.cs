using Discord;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using MoongBot.Core.Manager;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Service
{
    public class LoanService
    {
        private DatabaseManager _databaseManager = new DatabaseManager();
        private static CoinMarketManager _coinManager = new CoinMarketManager();
        private static readonly string rolePath = Path.Combine("jsonFiles", "userRoles.json");
        private readonly Random _random = new Random();
        private readonly ulong bushChannelId = ConfigManager.Config.BushChannelId;
        public async Task IncreaseInterestDailyAsync()
        {
            await _databaseManager.IncreaseInterestDailyAsync();
        }

        public async Task SendChannelMessage(ulong userId, int penalty, bool isCoin)
        {
            await EventManager.PenaltyNotification(userId, true, penalty);
            string randomReporter = _coinManager.reporterNames[_random.Next(_coinManager.reporterNames.Count)];
            string currentTime = DateTime.Now.ToString("MM월 dd일 HH:mm");
            string news;
            if (isCoin)
            {
                news = $"해당 사용자는 슬롯머신에서의 한방을 기대하고 대출을 했으나 대출금을 감당하지 못해 클로버를 캐러가게 된 것으로 알려졌습니다. 이에 호롤카지노 소유주 <@{ConfigManager.Config.OwnerId}>는 \"도박꾼의 말로다. 대출까지 끌어다쓰며 도박을 하는것은 좋지 않다\"라며 안타까움을 표했습니다.";
            }
            else
            {
                news = "해당 투자자는 최근 코인시장의 급격한 변동 속에서 큰 손실을 입었고 대출금을 감당하지 못해 클로버를 캐러가게 된 것으로 알려졌습니다. 이에 전문가는 \"투자 실패의 대가는 가볍지 않으니 투자자들은 책임감을 가지고 신중히 투자하기를 당부한다.\"라고 의견을 밝혔습니다.";
            }
            var embedBuilder = new EmbedBuilder()
                .WithTitle($":newspaper: {currentTime} - 호롤로 뉴스")
                .WithDescription($"<@{userId}>님이 대출금을 갚지 못해 <#{bushChannelId}>으로 끌려갔다는 소식입니다."+ news)
                .WithColor(Color.DarkGreen)
                .WithFooter($"호롤일보 {randomReporter} 기자 babo@holol.com");


            // 뉴스 메시지 전송
            if (isCoin)
            {
                await EventManager.SlotNewsNotification(embedBuilder.Build());
            }
            else
            {
                if (CoinMarketManager._subscribedUsers.Count > 0)
                {
                    // 구독한 유저들의 멘션 문자열 생성
                    string userMentions = string.Join(" ", CoinMarketManager._subscribedUsers.Select(userId => $"<@{userId}>"));

                    // 멘션된 유저들과 함께 뉴스 전송
                    await _coinManager.SendMarketEventAlertAsync(embedBuilder.Build(), userMentions);
                }
                else
                {
                    // 구독자가 없으면 그냥 뉴스만 전송
                    await _coinManager.SendMarketEventAlertAsync(embedBuilder.Build());
                }
            }
                
        }

        public async Task SendChannelMessage(ulong userId, bool isCoinRepay)
        {
            await EventManager.PenaltyNotification(userId, isCoinRepay);
        }
        public async Task<bool> ApplyPenaltyAsync(SocketGuildUser user, ulong userId, IGuild guild, int penalty)
        {
            try
            {
                var userRoles = user.Roles.Where(r => r.Id != 867037994573889546/*패널티역할*/ && r.Id != 753229940979400844/*서버후원자*/ && r.Id != 671005303919214603/*everyone*/ && r.Id != 717940402778406944/*관리자*/).Select(r => r.Id).ToList();

                SaveRolesToJson(userId, userRoles);

                var penaltyRole = guild.Roles.FirstOrDefault(r => r.Id == 867037994573889546);
                if (penaltyRole != null)
                {
                    await user.AddRoleAsync(penaltyRole);

                    if (userRoles.Any())
                    {
                        await user.RemoveRolesAsync(userRoles);
                    }
                    await _databaseManager.SetPenaltyCount(userId, penalty);
                    return true;
                }
                else
                {
                    Console.WriteLine("Error : ApplyPenaltyAsync() 역할을 못찾음");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"패널티 부여중 에러 발생 : {ex.Message}");
                await ExceptionManager.HandleExceptionAsync(ex);
                return false;
            }
        }

        public async Task<bool> CheckAndRemovePenaltyAsync(SocketUser user, ulong userId, SocketGuild guild, SocketTextChannel channel)
        {
            Console.WriteLine($"db에서 유저의 패널티 횟수 체크");
            int penaltyCount = await _databaseManager.GetPenaltyCount(userId);

            if (penaltyCount > 1)
            {
                await _databaseManager.DecrementPenaltyCount(userId);
            }
            else if (penaltyCount == 1)
            {
                try
                {
                    if (user is SocketGuildUser guildUser)
                    {
                        var originalRoles = LoadRolesFromJson(userId);

                        if (originalRoles != null)
                        {
                            Console.WriteLine("기존 역할 돌려주기 실행");
                            var rolesToRestore = guild.Roles.Where(r => r.Id != 717940402778406944 && r.Id != 867037994573889546 && originalRoles.Contains(r.Id));
                            if (rolesToRestore.Any())
                            {
                                
                                await guildUser.AddRolesAsync(rolesToRestore);
                                RemoveUserFromJson(userId);
                            }
                            else
                            {
                                Console.WriteLine("복원할 역할이 없습니다.");
                            }
                            var penaltyRole = guild.Roles.FirstOrDefault(r => r.Id == 867037994573889546);
                            if (penaltyRole != null)
                            {
                                await guildUser.RemoveRoleAsync(penaltyRole);
                            }                           
                        }
                        else
                        {
                            Console.WriteLine($"{userId}에 해당하는 딕셔너리 값이 없습니다.");
                        }                      
                    }

                    await _databaseManager.DeleteLoanRecord(userId);
                    await channel.SendMessageAsync($"<@{userId}> 님, 패널티가 해제되었습니다. 이제 다시 활동할 수 있습니다.");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"패널티 제거중 문제 발생 : {ex.Message}");
                    await ExceptionManager.HandleExceptionAsync(ex);
                    return false;
                }
            }

            return false;
        }

        private void SaveRolesToJson(ulong userId, List<ulong> roles)
        {
            var filePath = rolePath;
            Dictionary<ulong, List<ulong>> rolesDictionary;

            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                rolesDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, List<ulong>>>(json) ?? new Dictionary<ulong, List<ulong>>();
            }
            else
            {
                rolesDictionary = new Dictionary<ulong, List<ulong>>();
            }

            rolesDictionary[userId] = roles;
            var updatedJson = JsonConvert.SerializeObject(rolesDictionary, Formatting.Indented);
            File.WriteAllText(filePath, updatedJson);
        }

        // JSON 파일에서 역할 정보 불러오기
         private List<ulong> LoadRolesFromJson(ulong userId)
        {
            var filePath = rolePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var rolesDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, List<ulong>>>(json);

                if (rolesDictionary != null && rolesDictionary.TryGetValue(userId, out var roles))
                {
                    return roles;
                }
            }

            return null;
        }

        private void RemoveUserFromJson(ulong userId)
        {
            var filePath = rolePath;
            if (File.Exists(filePath))
            {
                var json = File.ReadAllText(filePath);
                var rolesDictionary = JsonConvert.DeserializeObject<Dictionary<ulong, List<ulong>>>(json)
                    ?? new Dictionary<ulong, List<ulong>>();

                if (rolesDictionary.ContainsKey(userId))
                {
                    rolesDictionary.Remove(userId);
                    var updatedJson = JsonConvert.SerializeObject(rolesDictionary, Formatting.Indented);
                    File.WriteAllText(filePath, updatedJson);
                }
                else
                {
                    Console.WriteLine($"{userId}에 해당하는 데이터가 JSON 파일에 존재하지 않습니다.");
                }
            }
            else
            {
                Console.WriteLine("JSON 파일이 존재하지 않습니다.");
            }
        }
    }
}

