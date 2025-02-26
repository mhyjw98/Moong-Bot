using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public class NotificationManager
    {
        //private static DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        //private static DatabaseManager _databaseManager = new DatabaseManager();
        //public static void ScheduleDailyWeatherCheck()
        //{
        //    Console.WriteLine("ScheduleDailyWeatherCheck 로직 실행");
        //    var now = DateTime.Now;
        //    var scheduledTime = new DateTime(now.Year, now.Month, now.Day, 8, 0, 0);

        //    if (now > scheduledTime)
        //    {
        //        scheduledTime = scheduledTime.AddDays(1);
        //    }

        //    var delay = scheduledTime - now;
        //    Task.Delay(delay).ContinueWith(async _ =>
        //    {
        //        while (true)
        //        {
        //            Console.WriteLine("날씨 체크 로직 실행");
        //            await CheckWeatherAndNotifyAsync();

        //            var nextScheduledTime = DateTime.Now.Date.AddDays(1).AddHours(8);
        //            var nextDelay = nextScheduledTime - DateTime.Now;
        //            await Task.Delay(nextDelay);
        //        }
        //    });
        //}

        //private static async Task CheckWeatherAndNotifyAsync()
        //{
        //    Console.WriteLine("데이터 하나의 정보 검사");
        //    var notifications = await _databaseManager.GetNotificationsAsync();
        //    var userMentions = new List<string>();

        //    foreach (var (userId, city) in notifications)
        //    {
        //        Console.WriteLine($"탐색하는 유저 : {userId}, 도시명 : {city}");
        //        JObject weatherData = await WeatherManager.GetWeatherAsync(city);
        //        if (WeatherManager.IsRainy(weatherData))
        //        {
        //            userMentions.Add($"<@{userId}>");
        //        }
        //    }
            
        //    if (userMentions.Count > 0)
        //    {
        //        var channel = _client.GetChannel(1267466762790764676) as IMessageChannel;
        //        string userMentionsString = string.Join(" ", userMentions);
        //        await channel.SendMessageAsync($"{userMentionsString} 오늘은 비가올 확률이 높아요! 우산을 챙기세요.");
        //    }
        //}

        //public static async Task RegisterNotification(ulong userId, string city, ITextChannel channel)
        //{
        //    if (city == null)
        //    {
        //        await channel.SendMessageAsync($"지역을 같이 작성해주세요 예시 : {ConfigManager.Config.Prefix}알림등록 인천");
        //        return;
        //    }
        //    if (!WeatherManager.IsValidCity(city))
        //    {
        //        await channel.SendMessageAsync("유효하지 않은 도시명입니다.");
        //    }
        //    else
        //    {
        //        await _databaseManager.RegisterNotificationAsync(userId, city, channel);
        //    }
        //}

        //public static async Task TurnOffNotification(ulong userId, ITextChannel channel)
        //{
        //    await _databaseManager.DeleteNotification(userId, channel);
        //}

        //public static async Task UpdateNotification(ulong userId, string city, ITextChannel channel)
        //{
        //    await _databaseManager.UpdateNotificationAsync(userId, city, channel);
        //}       
    }
}
