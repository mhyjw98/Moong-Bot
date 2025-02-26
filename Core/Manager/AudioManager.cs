using Amazon.Polly;
using Amazon.Polly.Model;
using Discord;
using Discord.WebSocket;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
        private static readonly Queue<(IGuild guild, string text)> _ttsQueue = new Queue<(IGuild guild, string text)>();
        private static readonly Queue<(IGuild guild, string text)> _mp3Queue = new Queue<(IGuild guild, string text)>();
        private static bool _isPlaying = false;
        public static readonly Dictionary<string, string> AudioFiles = new Dictionary<string, string>();
        public static async Task JoinAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            try
            {
                if (voiceState.VoiceChannel is null)
                {
                    await channel.SendMessageAsync("음성 채널에 먼저 참가해주세요.");
                    return;
                }               

                if (!_lavaNode.HasPlayer(guild))
                {
                    try
                    {
                        await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error : JoinAsync 에서 에러 발생");
                        await ExceptionManager.HandleExceptionAsync(ex);
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
                else
                {
                    Console.WriteLine($"Error message : JoinAsync 에서 알수없는 에러 발생");
                    return;
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error message : JoinAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }       
        }

        public static async Task MakeTTS(string text, IGuild guild)
        {
            try
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
                    File.SetAttributes(TTSPath, FileAttributes.Normal);
                    File.Delete(TTSPath);
                }

                using (FileStream fs = new FileStream(TTSPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sres.AudioStream.CopyTo(fs);
                    fs.Flush();
                }
                await Task.CompletedTask;
                return;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Error message : MakeTTS 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }          
        }
        public static async Task PlayAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel, string text)
        {
            try
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
                        await Task.Delay(1000);
                    }
                    else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
                    {
                        await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
                        return;
                    }
                }

                _ttsQueue.Enqueue((guild, text));

                if (!_isPlaying)
                {
                    await ProcessQueueAsync();
                }
            }
            catch (Exception ex)
            {
                await ExceptionManager.HandleExceptionAsync(ex);
            }          
        }

        public static async Task PlayAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel, string text, int krLength)
        {
            try
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
                        await Task.Delay(1000);
                    }
                    else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
                    {
                        await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
                        return;
                    }
                }
                if (channel.Id != _lavaNode.GetPlayer(guild).VoiceChannel.Id)
                {
                    return;
                }
                var player = _lavaNode.GetPlayer(voiceState.VoiceChannel.Guild);
                while (player == null || player.VoiceChannel == null || !_lavaNode.HasPlayer(voiceState.VoiceChannel.Guild))
                {
                    await Task.Delay(100); // 음성 채널에 완전히 연결될 때까지 잠시 기다림
                    player = _lavaNode.GetPlayer(voiceState.VoiceChannel.Guild);
                }
                _ttsQueue.Enqueue((guild, text));

                if (!_isPlaying)
                {
                    await ProcessQueueAsync();
                }
            }
            catch(Exception ex)
            {
                await ExceptionManager.HandleExceptionAsync(ex);
            }          
        }
        public static async Task StopAsnyc(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            var player = _lavaNode.GetPlayer(guild);
            try
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
            catch(Exception ex)
            {
                Console.WriteLine($"Error message : StopAsnyc 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }            
        }
        public static async Task LeaveAsync(IGuild guild, IVoiceState voiceState, ITextChannel channel)
        {
            if (voiceState.VoiceChannel is null)
            {
                await channel.SendMessageAsync("음성 채널에 연결되어있지 않습니다.");
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : LeaveAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
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
            try
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
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : ChangeCannelAsync 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }            
        }

        private static async Task ProcessQueueAsync()
        {
            while (_ttsQueue.Count > 0)
            {
                var (guild, text) = _ttsQueue.Dequeue();
                _isPlaying = true;

                var removeText = RemoveEmojis(text);

                if (string.IsNullOrWhiteSpace(removeText))
                {
                    _isPlaying = false;
                    continue;
                }

                await MakeTTS(removeText, guild);

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

                    // 재생 완료 대기
                    await Task.Delay(TimeSpan.FromMilliseconds(track.Duration.TotalMilliseconds));

                    // 대기 후 다음 트랙 재생
                    await Task.Delay(250);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error message : ProcessQueueAsync 에서 에러 발생");
                    await ExceptionManager.HandleExceptionAsync(ex);
                    _isPlaying = false;
                    return;
                }
            }

            _isPlaying = false;
        }

        private static string RemoveEmojis(string messageContent)
        {
            // 이모티콘 패턴 정의
            var emojiPattern = @"<a?:\w+:\d+>";
            var emojiRegex = new Regex(emojiPattern);

            // 이모티콘을 빈 문자열로 대체하여 제거
            return emojiRegex.Replace(messageContent, "").Trim();
        }

        public static async Task PlayMp3Async(IGuild guild, IVoiceState voiceState, ITextChannel channel, string text, bool isOwner)
        {
            try
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
                        await Task.Delay(1000);
                    }
                    else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
                    {
                        await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
                        return;
                    }
                }

                var dbManager = new DatabaseManager();

                using (var connection = new SqliteConnection(dbManager._connectionString))
                {
                    await connection.OpenAsync();

                    string selectQuery = @"SELECT FilePath, IsOwnerOnly, Volume FROM AudioFiles WHERE Word = @Word;";
                    using (var cmd = new SqliteCommand(selectQuery, connection))
                    {
                        cmd.Parameters.AddWithValue("@Word", text);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                string filePath = reader.GetString(0);
                                bool isOwnerOnly = reader.GetInt32(1) == 1;
                                int volume = reader.GetInt32(2);

                                if (isOwnerOnly && !isOwner)
                                {
                                    return;
                                }

                                _mp3Queue.Enqueue((guild, filePath));

                                if (!_isPlaying)
                                {
                                    await ProcessMp3QueueAsync(volume);
                                }
                            }                           
                        }
                    }
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error message : PlayMp3Async 에서 에러 발생");
                await ExceptionManager.HandleExceptionAsync(ex);
            }         
        }
        private static async Task ProcessMp3QueueAsync(int volume)
        {
            while (_mp3Queue.Count > 0)
            {
                var (guild, filePath) = _mp3Queue.Dequeue();
                _isPlaying = true;
                Console.WriteLine($"[DEBUG] Processing MP3 Queue: Guild ID - {guild.Id}, Word - {filePath}");

                try
                {
                    var player = _lavaNode.GetPlayer(guild);

                    LavaTrack track;
                    Console.WriteLine($"[DEBUG] Searching for track: {filePath}");
                    var search = await _lavaNode.SearchAsync(Victoria.Responses.Search.SearchType.Direct, Path.GetFullPath(filePath));
                    track = search.Tracks.FirstOrDefault();

                    ushort volumeLevel = (ushort)Math.Clamp(volume, 0, 100);
                    await player.UpdateVolumeAsync(volumeLevel);

                    if (player.PlayerState is PlayerState.Stopped || player.PlayerState is PlayerState.Paused)
                    {
                        if (player.Queue.Count != 0)
                        {
                            player.Queue.Clear();
                        }
                        Console.WriteLine("[DEBUG] Enqueuing track.");
                        player.Queue.Enqueue(track);
                    }

                    Console.WriteLine("[DEBUG] Starting track playback.");
                    await player.PlayAsync(track);

                    //// 재생 완료 대기
                    //await Task.Delay(TimeSpan.FromMilliseconds(track.Duration.TotalMilliseconds));

                    while (player.PlayerState == PlayerState.Playing)
                    {
                        await Task.Delay(1000); // 1초 간격으로 재생 상태 확인
                    }

                    Console.WriteLine("[DEBUG] Track playback finished.");
                    await Task.Delay(250);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error message : ProcessMp3QueueAsync 에서 에러 발생");
                    await ExceptionManager.HandleExceptionAsync(ex);
                    _isPlaying = false;
                    return;
                }
            }

            _isPlaying = false;
            Console.WriteLine("[DEBUG] MP3 Queue processing complete.");
        }
    }
}
