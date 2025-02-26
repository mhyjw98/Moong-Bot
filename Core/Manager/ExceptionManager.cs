using Discord.WebSocket;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoongBot.Core.Manager
{
    public static class ExceptionManager
    {
        private static DiscordSocketClient _client = ServiceManager.GetService<DiscordSocketClient>();
        private static ulong ownerId = ConfigManager.Config.OwnerId;
        public static async Task HandleExceptionAsync(Exception ex)
        {
            try
            {
                var owner = await _client.GetUserAsync(ownerId);

                if (owner != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("에러 발생")
                        .WithDescription($"**Message:** {ex.Message}\n**Stack Trace:**\n```\n{ex.StackTrace}\n```")
                        .WithColor(Color.Red)
                        .Build();

                    await owner.SendMessageAsync(embed: embed);
                }
                else
                {
                    Console.WriteLine($"Owner with ID {ownerId} not found.");
                }
            }
            catch (Exception dmEx)
            {
                Console.WriteLine($"Failed to send error message to owner: {dmEx.Message}");
            }
        }

        public static async Task HandleExceptionAsync(Exception ex, IGuild guild = null, IMessageChannel channel = null, string messageContent = null)
        {
            string methodName = ex.TargetSite != null ? ex.TargetSite.Name : "Unknown Method";
            Console.WriteLine($"Error in method: {methodName}, Message: {ex.Message}");

            try
            {
                var owner = await _client.GetUserAsync(ownerId);

                if (owner != null)
                {
                    var embedBuilder = new EmbedBuilder()
                        .WithTitle($"{guild.Name} 서버의 {channel.Name} 채널에서 에러 발생")
                        .WithDescription(
                            $"**Message:** {messageContent ?? "No message content"}\n" +
                            $"**Error Message:** {ex.Message}\n" +
                            $"**Occurred in Method:** {methodName}\n" +
                            $"**Stack Trace:**\n```\n{ex.StackTrace}\n```"
                            )
                        .WithColor(Color.Red);

                    var embed = embedBuilder.Build();
                    await owner.SendMessageAsync(embed: embed);
                }
                else
                {
                    Console.WriteLine($"Owner with ID {ownerId} not found.");
                }
            }
            catch (Exception dmEx)
            {
                Console.WriteLine($"Failed to send error message to owner: {dmEx.Message}");
            }
        }

        public static async Task SendOwnerMessageAsync(ulong userId, string userName, string msg)
        {
            try
            {               
                var owner = await _client.GetUserAsync(ownerId);

                if (owner != null)
                {
                    var embed = new EmbedBuilder()
                        .WithTitle("건의사항 or 버그 제보")
                        .WithDescription($"**Message FROM {userName} (UserId : {userId})**\n ```\n{msg}\n```")
                        .WithColor(Color.Green)
                        .Build();

                    await owner.SendMessageAsync(embed: embed);
                }
                else
                {
                    Console.WriteLine($"Owner with ID {ownerId} not found.");
                }
            }
            catch (Exception dmEx)
            {
                Console.WriteLine($"Failed to send error message to owner: {dmEx.Message}");
            }
        }
    }
}
