using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public static class ConfigManager
    {
        private static string ConfigFolder = "Resource";
        private static string ConfigFile = "config.json";
        private static string ConfigPath = ConfigFolder + "/" + ConfigFile;
        public static BotConfig Config { get; private set; }

        static ConfigManager()
        {
            if(!Directory.Exists(ConfigFolder))
                Directory.CreateDirectory(ConfigFolder);

            if (!File.Exists(ConfigPath))
            {
                Config = new BotConfig();
                var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(ConfigPath, json);
            }
            else
            {
                var json = File.ReadAllText(ConfigPath);
                Config = JsonConvert.DeserializeObject<BotConfig>(json);
            }
        }
    }

    public struct BotConfig
    {
        [JsonProperty("token")]
        public string Token { get; private set; }
        [JsonProperty("prefix")]
        public string Prefix { get; private set; }
        [JsonProperty("ttsprefix")]
        public string TtsPrefix { get; private set; }
        [JsonProperty("AWSAccessKeyId")]
        public string AWSAccessKeyId { get; private set; }
        [JsonProperty("AWSSecretKey")]
        public string AWSSecretKey { get; private set; }        
        [JsonProperty("ownerId")]
        public ulong OwnerId { get; private set; }
        [JsonProperty("openweatherapikey")]
        public string OpenWeatherMapApiKey { get; private set; }
        [JsonProperty("gmailClientId")]
        public string GmailClientId { get; private set; }
        [JsonProperty("gmailClientSecret")]
        public string GmailClientSecret { get; private set; }
        [JsonProperty("notificationChannelId")]
        public ulong NotificationChannelId { get; private set; }
        [JsonProperty("moongbotChannelId")]
        public ulong MoongBotChannelId { get; private set; }
        [JsonProperty("lottoChannelId")]
        public ulong LottoChannelId { get; private set; }
        [JsonProperty("coinChannelId")]
        public ulong CoinChannelId { get; private set; }
        [JsonProperty("bushChannelId")]
        public ulong BushChannelId { get; private set; }
        [JsonProperty("emojiGuildId")]
        public ulong EmojiGuildId { get; private set; }
        [JsonProperty("hololGuildId")]
        public ulong HololGuildId { get; private set; }
    }
}
