//using Discord;
//using Microsoft.Extensions.DependencyInjection;
//using Victoria;
//using Victoria.Enums;
//using Victoria.Responses.Search;

//namespace MoongBot.Core.Manager
//{
//    public static class MusicManager
//    {
//        private static readonly LavaNode _lavaNode = ServiceManager.Provider.GetRequiredService<LavaNode>();
//        public static async Task PlayAsync(IVoiceState voiceState, ITextChannel channel, IGuild guild, string query)
//        {
//            if (voiceState.VoiceChannel is null)
//            {
//                await channel.SendMessageAsync("음성 채널에 먼저 참가해주세요.");
//                return;
//            }
//            else
//            {
//                if (!_lavaNode.HasPlayer(guild))
//                {
//                    await _lavaNode.JoinAsync(voiceState.VoiceChannel, channel);
//                }
//                else if (voiceState.VoiceChannel != _lavaNode.GetPlayer(guild).VoiceChannel)
//                {
//                    await channel.SendMessageAsync("다른 음성 채널에서 사용 중입니다.");
//                    return;
//                }
//            }
//            try
//            {
//                var player = _lavaNode.GetPlayer(guild);

//                SearchResponse search;

//                if (Uri.IsWellFormedUriString(query, UriKind.Absolute))
//                {
//                    search = await _lavaNode.SearchAsync(SearchType.Direct, query);
//                    await channel.SendMessageAsync("유효한 URL 입니다.");
//                }
//                else
//                {
//                    search = await _lavaNode.SearchAsync(SearchType.YouTube, query);
//                    await channel.SendMessageAsync("유효하지 않은 URL 입니다.");
//                }

//                if (search.Status == SearchStatus.NoMatches)
//                {
//                    await channel.SendMessageAsync("검색 결과가 없습니다.");
//                    return;
//                }
//                else if (search.Status == SearchStatus.LoadFailed)
//                {
//                    await channel.SendMessageAsync($"유투브에서 정보를 로드할 수 없습니다.");
//                }

//                var track = search.Tracks.FirstOrDefault();

//                if (track == null)
//                {
//                    await channel.SendMessageAsync("트랙을 찾을 수 없습니다.");
//                    return;
//                }

//                if (player.Track != null && (player.PlayerState is Victoria.Enums.PlayerState.Playing || player.PlayerState is Victoria.Enums.PlayerState.Paused))
//                {
//                    player.Queue.Enqueue(track);
//                    Console.WriteLine($"[{DateTime.Now}]\t(AUDIO)\tTrack was added to queue");
//                    await channel.SendMessageAsync($"{track.Title} has been added to queue");
//                    return;
//                }

//                await player.PlayAsync(track);
//                Console.WriteLine($"재생중인 음악 : {track.Title}");
//                await channel.SendMessageAsync($"재생중인 음악 : {track.Title}");
//                return;
//            }
//            catch (Exception ex)
//            {
//                await channel.SendMessageAsync($"ERROR:\t{ex.Message}");
//                return;
//            }
//        }
//    }
//}
