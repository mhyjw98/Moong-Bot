using Discord;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Channels;
using System.Threading.Tasks;


namespace MoongBot.Core.Manager
{
    public static class WeatherManager
    {
        private static HttpClient _httpClient = new HttpClient();

        public static async Task<JObject> GetWeatherAsync(string city)
        {
            // 도시 이름이 한글일 경우 영어로 변환
            if (CityTranslations.TryGetValue(city, out var englishCity))
            {
                city = englishCity;
            }

            var response = await _httpClient.GetStringAsync($"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={ConfigManager.Config.OpenWeatherMapApiKey}&lang=kr&units=metric");
            return JObject.Parse(response);
        }
       
        public static bool IsRainy(JObject weatherData)
        {
            var weather = weatherData["weather"][0]["main"].ToString().ToLower();
            return weather.Contains("rain");
        }

        public static string GetWindDirection(double degrees)
        {
            string[] directions = { "북", "북북동", "북동", "동북동", "동", "동남동", "남동", "남남동", "남", "남남서", "남서", "서남서", "서", "서북서", "북서", "북북서" };
            int index = (int)((degrees + 11.25) / 22.5) % 16;
            return directions[index];
        }

        public static async Task WeatherAsync(ITextChannel channel, string city)
        {
            var embedBuilder = new EmbedBuilder();
            embedBuilder.WithThumbnailUrl("https://cdn-icons-png.flaticon.com/512/3722/3722011.png");
            embedBuilder.WithColor(255, 145, 200);

            string Description = "오늘의 날씨를 알려드립니다.";

            embedBuilder.WithDescription(Description);


            JObject weatherData;
            try
            {
                weatherData = await GetWeatherAsync(city);
            }
            catch (Exception ex)
            {
                // 유효하지 않은 도시 이름일 때 사용자에게 메시지 전송
                Console.WriteLine($"Error : {ex.Message}");
                await channel.SendMessageAsync($"죄송해요 {city}의 날씨정보를 찾을 수 없어요.");
                return;
            }

            var isRainy = IsRainy(weatherData);

            var tempMin = weatherData["main"]["temp_min"].ToString();
            var tempMax = weatherData["main"]["temp_max"].ToString();
            var feelsLike = weatherData["main"]["feels_like"].ToString();
            var pressure = weatherData["main"]["pressure"].ToString();
            var sunrise = DateTimeOffset.FromUnixTimeSeconds((long)weatherData["sys"]["sunrise"]).ToLocalTime().ToString("HH:mm");
            var sunset = DateTimeOffset.FromUnixTimeSeconds((long)weatherData["sys"]["sunset"]).ToLocalTime().ToString("HH:mm");
            var rain = weatherData["rain"]?["1h"]?.ToString() ?? "0";
            var windDeg = double.Parse(weatherData["wind"]["deg"].ToString());
            var windDirection = GetWindDirection(windDeg);
            var cloudiness = weatherData["clouds"]["all"].ToString();

            embedBuilder
                .WithTitle($"{city}의 날씨")
                .WithDescription($"현재 날씨: {weatherData["weather"][0]["description"]}")
                .AddField("온도", $"{weatherData["main"]["temp"]} °C", true)
                .AddField("체감 온도", $"{feelsLike} °C", true)
                .AddField("최저 온도", $"{tempMin} °C", true)
                .AddField("최고 온도", $"{tempMax} °C", true)
                .AddField("습도", $"{weatherData["main"]["humidity"]}%", true)
                .AddField("기압", $"{pressure} hPa", true)
                .AddField("풍속", $"{weatherData["wind"]["speed"]} m/s", true)
                .AddField("풍향", $"{windDeg}° ({windDirection}풍)", true)
                .AddField("구름량", $"{cloudiness}%", true)
                .AddField("비", isRainy ? "옴" : "안옴", true)
                .AddField("강수량", $"{rain} mm", true)
                .AddField("일출", sunrise, true)
                .AddField("일몰", sunset, true)
                .Build();

            await channel.SendMessageAsync("", false, embedBuilder.Build());
        }

        public static readonly Dictionary<string, string> CityTranslations = new Dictionary<string, string>
        {
            { "서울", "seoul" },
            { "부산", "busan" },
            { "대구", "daegu" },
            { "인천", "incheon" },
            { "광주", "gwangju" },
            { "대전", "daejeon" },
            { "울산", "ulsan" },
            { "수원", "suwon" },
            { "고양", "goyang" },
            { "용인", "yongin" },
            { "성남", "seongnam" },
            { "청주", "cheongju" },
            { "천안", "cheonan" },
            { "전주", "jeonju" },
            { "창원", "changwon" },
            { "안산", "ansan" },
            { "안양", "anyang" },
            { "남양주", "namyangju" },
            { "김해", "gimhae" },
            { "포항", "pohang" },
            { "평택", "pyeongtaek" },
            { "파주", "paju" },
            { "구리", "guri" },
            { "진주", "chinju" },
            { "원주", "wonju" },
            { "군포", "gunpo" },
            { "오산", "osan" },
            { "익산", "iksan" },
            { "춘천", "chuncheon" },
            { "경산", "gyeongsan" },
            { "양산", "yangsan" },
            { "나주", "naju" },
            { "여수", "yeosu" },
            { "순천", "suncheon" },
            { "목포", "mokpo" },
            { "삼척", "samcheok" },
            { "강릉", "gangneung" },
            { "속초", "sokcho" },
            { "태백", "taebaek" },
            { "서산", "seosan" },
            { "보령", "boryeong" },
            { "아산", "asan" },
            { "공주", "gongju" },
            { "논산", "nonsan" },
            { "김제", "gimje" },
            { "남원", "namwon" },
            { "광양", "gwangyang" },
            { "구미", "gumi" },
            { "영주", "yeongju" },
            { "상주", "sangju" },
            { "문경", "mungyeong" },
            { "경주", "gyeongju" },
            { "안동", "andong" },
            { "사천", "sacheon" },
            { "통영", "tongyeong" },
            { "의령", "uiryeong" },
            { "함안", "haman" },
            { "창녕", "changnyeong" },
            { "하동", "hadong" },
            { "함양", "hamyang" },
            { "합천", "hapcheon" },
            { "제주", "jeju" },
            { "제주도", "jeju" }
        };     
    }
}
