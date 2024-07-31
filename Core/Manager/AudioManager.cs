using Amazon.Polly;
using Amazon.Polly.Model;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using Victoria;
using Victoria.Enums;
using Victoria.EventArgs;

namespace MoongBot.Core.Manager
{
    public static class AudioManager
    {
        private static readonly LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
        private static string ResourcesFolder = "Resources";
        private static string TTS = "tts.mp3";
        public static async Task JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if (voiceState.VoiceChannel is null)
            {
                await channel.SendMessageAsync("음성 채널에 먼저 참가해주세요.");
                return;
            }
            else
            {
                if (!_lavaNode.HasPlayer(guild))
                {
                    try
                    {
                        await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[{DateTime.Now}] {ex.Message}");
                    }
                }
                else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
                {
                    await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
                    return;
                }
                else if (voiceState.VoiceChannel == _lavaNode.GetPlayer(guild).VoiceChannel)
                {
                    await channel.SendMessageAsync("이미 봇이 음성 채널에 들어와있습니다.");
                    return;
                }
            }
        }

        public static async Task MakeTTS(string text, IGuild guild)
        {
            string AWSAccessKeyId = ConfigManager.Config.AWSAccessKeyId;
            string AWSSecretKey = ConfigManager.Config.AWSSecretKey;
            var pc = new AmazonPollyClient(AWSAccessKeyId, AWSSecretKey);
            var sreq = new SynthesizeSpeechRequest
            {
                Text = "<speak>" + text + "</speak>",
                OutputFormat = OutputFormat.Mp3,
                VoiceId = VoiceId.Seoyeon,
                LanguageCode = "ko-KR",
                TextType = TextType.Ssml
            };
            var sres = await pc.SynthesizeSpeechAsync(sreq);

            if (sres.HttpStatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine("Http Error");
                return;
            }
            Console.WriteLine("TTS made successfully.");
            string guildFolder = GetGuildFolderPath(guild);
            if (!Directory.Exists(guildFolder))
                Directory.CreateDirectory(guildFolder);

            string TTSPath = guildFolder + "/" + TTS;
            Console.WriteLine(TTSPath);
            if (File.Exists(TTSPath))
            {
                File.Delete(TTSPath);
            }
            FileStream fs = new FileStream(TTSPath, FileMode.Create);

            sres.AudioStream.CopyTo(fs);
            fs.Flush();
            fs.Close();
            await Task.CompletedTask;
            return;
        }
        public static async Task PlayAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel, string text)
        {

            if (voiceState.VoiceChannel is null)
            {
                await channel.SendMessageAsync("음성 채널에 먼저 참가해주세요.");
                return;
            }
            else
            {
                if (!_lavaNode.HasPlayer(guild))
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                }
                else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
                {
                    await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
                    return;
                }
            }
            await MakeTTS(text, guild);
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                LavaTrack track;
                string TTSPath = GetGuildFolderPath(guild) + "/" + TTS;
                var search = await _lavaNode.SearchAsync(Victoria.Responses.Search.SearchType.Direct, Path.GetFullPath(TTSPath));
                track = search.Tracks.FirstOrDefault();
                if (player.PlayerState is PlayerState.Stopped || player.PlayerState is PlayerState.Paused)
                {
                    if (player.Queue.Count != 0)
                    {
                        player.Queue.Clear();
                    }
                    player.Queue.Enqueue(track);
                }
                await player.PlayAsync(track);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] {ex.Message}");
                return;
            }
        }

        public static async Task PlayAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel, string text, int krLength)
        {
            text = text.Substring(krLength);
            if (voiceState.VoiceChannel is null)
            {
                await channel.SendMessageAsync("음성 채널에 먼저 참가해주세요.");
                return;
            }
            else
            {
                if (!_lavaNode.HasPlayer(guild))
                {
                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                }
                else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
                {
                    await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
                    return;
                }
            }
            await MakeTTS(text, guild);

            var player = _lavaNode.GetPlayer(guild);
            string TTSPath = GetGuildFolderPath(guild) + "/" + TTS;
            LavaTrack track;
            var search = await _lavaNode.SearchAsync(Victoria.Responses.Search.SearchType.Direct, Path.GetFullPath(TTSPath));
            track = search.Tracks.FirstOrDefault();
            if (player.PlayerState is PlayerState.Stopped)
            {
                player.Queue.Enqueue(track);
            }
            await player.PlayAsync(track);
        }
        public static async Task StopAsnyc(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            var player = _lavaNode.GetPlayer(guild);

            if (voiceState.VoiceChannel is null)
            {
                await channel.SendMessageAsync("음성 채널에 먼저 참가해주세요.");
                return;
            }
            else
            {
                if (!_lavaNode.HasPlayer(guild))
                {
                    return;
                }
                else if (voiceState.VoiceChannel != player.VoiceChannel)
                {
                    await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
                    return;
                }
                else if (voiceState.VoiceChannel == player.VoiceChannel)
                {
                    if (_lavaNode.GetPlayer(guild).PlayerState is PlayerState.Playing)
                    {
                        await player.StopAsync();
                        await channel.SendMessageAsync("음성 출력을 중단하였습니다.");
                    }
                    else return;
                }
            }
        }
        public static async Task LeaveAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if (voiceState.VoiceChannel is null)
            {
                await channel.SendMessageAsync("봇이 음성 채널에 연결되어있지 않습니다.");
                return;
            }

            if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
            {
                await channel.SendMessageAsync("같은 음성 채널에 참가해주세요.");
                return;
            }
            try
            {
                var player = _lavaNode.GetPlayer(guild);
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"[{DateTime.Now}]\t {ex.Message}");
                return;
            }
        }

        private static string GetGuildFolderPath(IGuild guild)
        {

            string guildPath = Path.GetFullPath(ResourcesFolder) + "/" + guild.Id.ToString();
            Console.WriteLine(guildPath);
            return guildPath;
        }

        public static async Task ChangeCannelAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if (!_lavaNode.HasPlayer(guild))
            {
                await channel.SendMessageAsync("사용 중이 아닙니다.");
                return;
            }
            if (voiceState.VoiceChannel is null)
            {
                await channel.SendMessageAsync("음성 채널에 먼저 참가해주세요.");
                return;
            }         
            if (voiceState.VoiceChannel == _lavaNode.GetPlayer(guild).VoiceChannel)
            {
                await channel.SendMessageAsync("이미 같은 음성채널에서 사용하고 있습니다.");
                return;
            }
            else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
            {
                var player = _lavaNode.GetPlayer(guild);
                string msg = "``" + player.VoiceChannel.Name + "``에서 ``" + voiceState.VoiceChannel.Name + "``으로 이동했어요.";
                if (player.PlayerState is PlayerState.Playing) await player.StopAsync();
                await _lavaNode.LeaveAsync(player.VoiceChannel);

                await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);

                await channel.SendMessageAsync(msg);
            }
        }
    }
}
