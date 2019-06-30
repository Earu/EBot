﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Energize.Essentials;
using Energize.Essentials.MessageConstructs;
using Energize.Essentials.TrackTypes;
using Energize.Interfaces.Services.Database;
using Energize.Interfaces.Services.Listeners;
using Energize.Interfaces.Services.Senders;
using SpotifyAPI.Web;
using Victoria;
using Victoria.Entities;
using Victoria.Queue;

namespace Energize.Services.Listeners.Music
{
    [Service("Music")]
    public class MusicPlayerService : ServiceImplementationBase, IMusicPlayerService
    {
        private readonly DiscordShardedClient Client;
        private readonly LavaShardClient LavaClient;
        private readonly Logger Logger;
        private readonly MessageSender MessageSender;
        private readonly ServiceManager ServiceManager;
        private readonly ConcurrentDictionary<ulong, IEnergizePlayer> Players;
        private readonly SpotifyWebAPI Spotify;
        private readonly Timer SpotifyAuthTimer;
        private readonly Random Rand;

        public MusicPlayerService(EnergizeClient client)
        {
            this.Players = new ConcurrentDictionary<ulong, IEnergizePlayer>();

            this.Client = client.DiscordClient;
            this.Logger = client.Logger;
            this.MessageSender = client.MessageSender;
            this.ServiceManager = client.ServiceManager;
            this.LavaClient = new LavaShardClient();
            this.Spotify = new SpotifyWebAPI
            {
                TokenType = "Bearer",
                UseAuth = true,
                UseAutoRetry = true
            };

            this.SpotifyAuthTimer = new Timer(async _ =>
            {
                string json = await HttpClient.PostAsync("https://accounts.spotify.com/api/token?grant_type=client_credentials", string.Empty,
                                                         this.Logger, null, req => 
                {
                    byte[] credBytes = Encoding.UTF8.GetBytes($"{Config.Instance.Spotify.ClientID}:{Config.Instance.Spotify.ClientSecret}");
                    req.Headers[HttpRequestHeader.Authorization] = $"Basic {Convert.ToBase64String(credBytes)}";
                    req.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                });

                var keys = JsonPayload.Deserialize<Dictionary<string, string>>(json, this.Logger);
                if (keys.ContainsKey("access_token"))
                    this.Spotify.AccessToken = keys["access_token"];
            });

            this.Rand = new Random();

            this.LavaClient.OnTrackException += this.OnTrackIssue;
            this.LavaClient.OnTrackStuck += async (ply, track, _) => await this.OnTrackIssue(ply, track);
            this.LavaClient.OnTrackFinished += this.OnTrackFinished;
            this.LavaClient.Log += async logMsg => this.Logger.Nice("Lavalink", ConsoleColor.Magenta, logMsg.Message);
            this.LavaClient.OnPlayerUpdated += this.OnPlayerUpdated;
            this.LavaClient.OnSocketClosed += this.OnSocketClosed;
        }

        public override Task InitializeAsync()
        {
            this.SpotifyAuthTimer.Change(0, 3600 * 1000);
            return Task.CompletedTask;
        }

        private async Task OnSocketClosed(int errorCode, string reason, bool byRemote)
        {
            await this.DisconnectAllPlayersAsync("Music streaming is unavailable at the moment, disconnecting");
            SocketChannel chan = this.Client.GetChannel(Config.Instance.Discord.BugReportChannelID);
            if (chan != null)
            {
                EmbedBuilder builder = new EmbedBuilder();
                builder
                    .WithDescription("Lost connection to Lavalink node")
                    .WithField("Error Code", errorCode)
                    .WithField("Reason", reason)
                    .WithField("By Remote", byRemote)
                    .WithColorType(EmbedColorType.Danger)
                    .WithFooter("lavalink error");

                await this.MessageSender.Send(chan, builder.Build());
            }
        }

        private async Task OnPlayerUpdated(LavaPlayer lply, ILavaTrack track, TimeSpan position)
        {
            if (this.Players.TryGetValue(lply.VoiceChannel.GuildId, out IEnergizePlayer ply))
            {
                if (ply.TrackPlayer != null)
                {
                    if (!track.IsStream && !ply.IsPaused)
                        await ply.TrackPlayer.Update(track, ply.Volume, ply.IsPaused, ply.IsLooping, true);

                    ply.Refresh();
                }
            }

            IGuild guild = lply.VoiceChannel.Guild;
            string msg = $"Updated track <{track.Title}> ({position}) for player in guild <{guild.Name}>";
            this.Logger.LogTo("victoria.log", msg);
        }

        public LavaRestClient LavaRestClient { get; private set; }

        private bool SanitizeCheck(IVoiceChannel vc, ITextChannel chan)
            => vc != null && chan != null;

        public async Task<IEnergizePlayer> ConnectAsync(IVoiceChannel vc, ITextChannel chan)
        {
            if (!this.SanitizeCheck(vc, chan)) return null;

            try
            {
                IEnergizePlayer ply;
                if (this.Players.ContainsKey(vc.GuildId))
                {
                    ply = this.Players[vc.GuildId];
                    if (ply.Lavalink == null) // in case we lose the player object
                        ply.Lavalink = await this.LavaClient.ConnectAsync(vc, chan);
                }
                else
                {
                    ply = new EnergizePlayer(await this.LavaClient.ConnectAsync(vc, chan));
                    this.Logger.Nice("MusicPlayer", ConsoleColor.Magenta, $"Connected to VC in guild <{vc.Guild}>");
                    if (this.Players.TryAdd(vc.GuildId, ply))
                    {
                        ply.BecameInactive += async () =>
                        {
                            this.Logger.Nice("MusicPlayer", ConsoleColor.Yellow, $"Connected player became inactive in guild <{ply.VoiceChannel.Guild}>");
                            await this.DisconnectAsync(vc);
                        };
                    }
                }

                this.LavaClient.UpdateTextChannel(vc.GuildId, chan);
                if (vc.Id != ply.Lavalink.VoiceChannel.Id)
                    await this.LavaClient.MoveChannelsAsync(vc);

                return ply;
            }
            catch(Exception ex)
            {
                if (ex is ObjectDisposedException)
                    this.Logger.Nice("MusicPlayer", ConsoleColor.Red, "Could not connect, threading issue from Discord.NET");
                else
                    this.Logger.Danger(ex);

                await this.DisconnectAsync(vc);

                return null;
            }
        }

        public async Task DisconnectAsync(IVoiceChannel vc)
        {
            if (vc == null) return;

            try
            {
                await this.LavaClient.DisconnectAsync(vc);
                if (this.Players.TryRemove(vc.GuildId, out IEnergizePlayer ply))
                {
                    this.Logger.Nice("MusicPlayer", ConsoleColor.Magenta, $"Disconnected from VC in guild <{vc.Guild}>");
                    ply.Disconnected = true;
                    if (ply.TrackPlayer != null)
                        await ply.TrackPlayer.DeleteMessage();
                }
            }
            catch (Exception ex)
            {
                if (this.Players.TryRemove(vc.GuildId, out IEnergizePlayer ply))
                {
                    ply.Disconnected = true;
                    if (ply.TrackPlayer != null)
                        await ply.TrackPlayer.DeleteMessage();
                }
                
                if (ex is ObjectDisposedException)
                    this.Logger.Nice("MusicPlayer", ConsoleColor.Red, "Could not disconnect, threading issue from Discord.NET");
                else
                    this.Logger.Danger(ex);
            }
        }

        public async Task DisconnectAllPlayersAsync(string warnMsg)
        {
            int count = this.Players.Count;
            foreach ((ulong _, IEnergizePlayer ply) in this.Players)
            {
                if (ply.VoiceChannel != null)
                {
                    await this.DisconnectAsync(ply.VoiceChannel);
                    if (ply.TextChannel != null)
                        await this.MessageSender.Warning(ply.TextChannel, "music player", warnMsg);
                }
            }

            this.Logger.Nice("MusicPlayer", ConsoleColor.Yellow, $"Disconnected {count} players");
        }

        public async Task<IUserMessage> AddTrackAsync(IVoiceChannel vc, ITextChannel chan, ILavaTrack lavaTrack)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return null;

            if (ply.IsPlaying)
            {
                ply.Queue.Enqueue(lavaTrack);
                return await this.SendNewTrackAsync(chan, lavaTrack);
            }
            await ply.Lavalink.PlayAsync(lavaTrack);
            return await this.SendPlayerAsync(ply, lavaTrack, chan);
        }

        public async Task<IUserMessage> PlayRadioAsync(IVoiceChannel vc, ITextChannel chan, ILavaTrack lavaTrack)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return null;

            RadioTrack radio = new RadioTrack(lavaTrack);
            ply.Queue.Clear();
            ply.CurrentRadio = radio;
            await ply.Lavalink.PlayAsync(lavaTrack);
            return await this.SendPlayerAsync(ply, radio, chan);
        }

        public async Task<List<IUserMessage>> AddPlaylistAsync(IVoiceChannel vc, ITextChannel chan, string name, IEnumerable<ILavaTrack> trs)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return null;

            List<ILavaTrack> tracks = trs.ToList();
            if (tracks.Count < 1)
                return new List<IUserMessage>
                {
                    await this.MessageSender.Warning(chan, "music player", "The loaded playlist does not contain any tracks")
                };

            if (ply.IsPlaying)
            {
                foreach (ILavaTrack track in tracks)
                    ply.Queue.Enqueue(track);

                return new List<IUserMessage>
                {
                    await this.MessageSender.Good(chan, "music player", $"🎶 Added `{tracks.Count}` tracks from `{name}`")
                };
            }
            ILavaTrack lavaTrack = tracks[0];
            tracks.RemoveAt(0);

            if (tracks.Count > 0)
                foreach (ILavaTrack tr in tracks)
                    ply.Queue.Enqueue(tr);

            await ply.Lavalink.PlayAsync(lavaTrack);
            return new List<IUserMessage>
            {
                await this.MessageSender.Good(chan, "music player", $"🎶 Added `{tracks.Count}` tracks from `{name}`"),
                await this.SendPlayerAsync(ply, lavaTrack, chan)
            };
        }

        public async Task StopTrackAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;

            ply.Queue.Clear();
            if (ply.IsPlaying)
                await ply.Lavalink.StopAsync();
        }

        public async Task<bool> LoopTrackAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return false;

            bool isLooping = ply.IsLooping;
            ply.IsLooping = !isLooping;
            return !isLooping;
        }

        public async Task<bool> AutoplayTrackAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return false;

            bool autoplay = ply.Autoplay;
            ply.Autoplay = !autoplay;

            return !autoplay;
        }

        public async Task ShuffleTracksAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;

            ply.Queue.Shuffle();
        }

        public async Task ClearTracksAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;

            ply.Queue.Clear();
        }

        public async Task PauseTrackAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;

            if (ply.IsPlaying && !ply.IsPaused)
                await ply.Lavalink.PauseAsync();
        }

        public async Task ResumeTrackAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;

            if (ply.IsPlaying && ply.IsPaused)
                await ply.Lavalink.ResumeAsync();
        }

        public async Task SkipTrackAsync(IVoiceChannel vc, ITextChannel chan)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;

            if (ply.CurrentRadio != null)
                ply.CurrentRadio = null;

            if (!ply.IsPlaying) return;

            if (ply.Queue.Count > 0)
            {
                await ply.Lavalink.SkipAsync();
                await this.SendPlayerAsync(ply, ply.CurrentTrack);
                return;
            }
            else
            {
                ILavaTrack oldTrack = ply.CurrentTrack;
                await ply.Lavalink.StopAsync();
                if (ply.Autoplay)
                    await this.AddRelatedYTContentAsync(vc, chan, oldTrack);
            }
        }

        public async Task SetTrackVolumeAsync(IVoiceChannel vc, ITextChannel chan, int vol)
        {
            vol = Math.Clamp(vol, 0, 200);
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;

            if (ply.IsPlaying)
                await ply.Lavalink.SetVolumeAsync(vol);
        }

        public async Task SeekTrackAsync(IVoiceChannel vc, ITextChannel chan, int amount)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, chan);
            if (ply == null) return;
            if (!ply.IsPlaying) return;

            ILavaTrack lavaTrack = ply.CurrentTrack;
            TimeSpan total = lavaTrack.Position.Add(TimeSpan.FromSeconds(amount));
            if (total < lavaTrack.Length && total >= TimeSpan.Zero)
                await ply.Lavalink.SeekAsync(total);
        }

        public ServerStats LavalinkStats { get => this.LavaClient.ServerStats; }

        public int PlayerCount { get => this.Players.Count; }

        private static async Task<string> GetThumbnailAsync(ILavaTrack track)
        {
            try
            {
                return await track.FetchThumbnailAsync();
            }
            catch
            {
                return string.Empty;
            }
        }
        
        public async Task<IUserMessage> SendQueueAsync(IVoiceChannel vc, IMessage msg)
        {
            IEnergizePlayer ply = await this.ConnectAsync(vc, msg.Channel as ITextChannel);
            IPaginatorSenderService paginator = this.ServiceManager.GetService<IPaginatorSenderService>("Paginator");
            List<IQueueObject> objs = ply.Queue.Items.ToList();
            if (objs.Count > 0)
            {
                return await paginator.SendPaginator(msg, "track queue", objs, async (obj, builder) =>
                {
                    int i = objs.IndexOf(obj);
                    builder.WithDescription($"🎶 Track `#{i + 1}` out of `{objs.Count}` in the queue");

                    if (obj is ILavaTrack track)
                    {
                        builder
                            .WithField("Title", track.Title)
                            .WithField("Author", track.Author)
                            .WithField("Length", track.IsStream ? " - " : track.Length.ToString(@"hh\:mm\:ss"))
                            .WithField("Stream", track.IsStream);

                        string thumbnailurl = await GetThumbnailAsync(track);
                        if (!string.IsNullOrWhiteSpace(thumbnailurl))
                            builder.WithThumbnailUrl(thumbnailurl);
                    }
                    else
                    {
                        builder
                            .WithField("Title", "?")
                            .WithField("Author", "?")
                            .WithField("Length", "?")
                            .WithField("Stream", "?");
                    }
                });
            }
            return await this.MessageSender.Good(msg, "track queue", "The track queue is empty");
        }

        private async Task<Embed> GetNewTrackEmbed(ILavaTrack lavaTrack, IMessage msg = null)
        {
            string thumbnailUrl = await GetThumbnailAsync(lavaTrack);
            EmbedBuilder builder = new EmbedBuilder();
            if (msg != null)
                builder.WithAuthorNickname(msg);
            string desc = "🎶 Added the following track to the queue:";
            if (!string.IsNullOrWhiteSpace(thumbnailUrl))
                builder.WithThumbnailUrl(thumbnailUrl);
            return builder
                .WithDescription(desc)
                .WithColorType(EmbedColorType.Good)
                .WithFooter("music player")
                .WithField("Title", lavaTrack.Title)
                .WithField("Author", lavaTrack.Author)
                .WithField("Length", lavaTrack.IsStream ? " - " : lavaTrack.Length.ToString(@"hh\:mm\:ss"))
                .WithField("Stream", lavaTrack.IsStream)
                .Build();
        }

        public async Task<IUserMessage> SendNewTrackAsync(IMessage msg, ILavaTrack lavaTrack)
        {
            Embed embed = await this.GetNewTrackEmbed(lavaTrack, msg);

            return await this.MessageSender.Send(msg, embed);
        }

        public async Task<IUserMessage> SendNewTrackAsync(ITextChannel chan, ILavaTrack lavaTrack)
        {
            Embed embed = await this.GetNewTrackEmbed(lavaTrack);

            return await this.MessageSender.Send(chan, embed);
        }

        private void AddPlayerReactions(IUserMessage msg, bool isRadio = false)
        {
            Task.Run(async () =>
            {
                if (msg == null) return;

                SocketGuildChannel chan = (SocketGuildChannel)msg.Channel;
                SocketGuild guild = chan.Guild;
                if (guild.CurrentUser.GetPermissions(chan).AddReactions)
                {
                    List<string> unicodeStrings = new List<string> { "⏯", "🔁", "⬆", "⬇", "⏭" };
                    if (isRadio)
                        unicodeStrings.RemoveAt(1);

                    foreach (string unicode in unicodeStrings)
                        await msg.AddReactionAsync(new Emoji(unicode));
                }
            }).ContinueWith(t =>
            {
                if (t.IsFaulted)
                    this.Logger.Nice("MusicPlayer", ConsoleColor.Yellow, $"Could not create player reactions: {t.Exception.Message}");
            });
        }

        public async Task<IUserMessage> SendPlayerAsync(IEnergizePlayer ply, IQueueObject obj = null, IChannel chan = null)
        {
            obj = obj ?? ply.CurrentTrack;

            if (ply.TrackPlayer == null)
            {
                ply.TrackPlayer = new TrackPlayer(ply.VoiceChannel.GuildId);
                await ply.TrackPlayer.Update(obj, ply.Volume, ply.IsPaused, ply.IsLooping, false);
            }
            else
            {
                await ply.TrackPlayer.Update(obj, ply.Volume, ply.IsPaused, ply.IsLooping, false);
                await ply.TrackPlayer.DeleteMessage();
            }

            if (obj == null) return null;

            ply.TrackPlayer.Message = await this.MessageSender.Send(chan ?? ply.TextChannel, ply.TrackPlayer.Embed);
            this.AddPlayerReactions(ply.TrackPlayer.Message, obj is RadioTrack);
            return ply.TrackPlayer.Message;
        }

        private async Task<YoutubeVideo> FetchYTRelatedVideoAsync(string videoId)
        {
            string endpoint = $"https://www.googleapis.com/youtube/v3/search?part=snippet&relatedToVideoId={videoId}&type=video&key={Config.Instance.Keys.YoutubeKey}&maxResults=6";
            string json = await HttpClient.GetAsync(endpoint, this.Logger);
            YoutubeRelatedVideos relatedVideos = JsonPayload.Deserialize<YoutubeRelatedVideos>(json, this.Logger);
            if (relatedVideos == null || relatedVideos.Videos.Length == 0) return null;
            IDatabaseService dbService = this.ServiceManager.GetService<IDatabaseService>("Database");
            using (IDatabaseContext ctx = await dbService.GetContext())
                await ctx.Instance.SaveYoutubeVideoIds(relatedVideos.Videos.Select(vid => vid.Id));

            IEnumerable<YoutubeVideo> vids = relatedVideos.Videos.Where(vid => !vid.Id.VideoID.Equals(videoId));
            return relatedVideos.Videos[this.Rand.Next(0, relatedVideos.Videos.Length)];
        }

        private static readonly Regex YTRegex = new Regex(@"(?!videoseries)[a-zA-Z0-9_-]{11}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private async Task<(bool, YoutubeVideo)> TryGetVideoAsync(ILavaTrack lavaTrack)
        {
            bool failed = false;
            YoutubeVideo video = null;
            if (lavaTrack.Uri.AbsoluteUri.Contains("youtu"))
            {
                Match match = YTRegex.Match(lavaTrack.Uri.AbsoluteUri);
                if (!match.Success)
                    failed = true;

                video = await this.FetchYTRelatedVideoAsync(match.Value);
                if (video == null)
                    failed = true;
            }
            else
                failed = true;

            return (failed, video);
        }

        private async Task<string> GetNextTrackVideoURLAsync(bool useDb, YoutubeVideo video)
        {
            if (!useDb)
                return $"https://www.youtube.com/watch?v={video.Id.VideoID}";

            IDatabaseService dbService = this.ServiceManager.GetService<IDatabaseService>("Database");
            using (IDatabaseContext ctx = await dbService.GetContext())
            {
                IYoutubeVideoID videoId = await ctx.Instance.GetRandomVideoIdAsync();
                if (videoId == null) return string.Empty;

                return $"https://www.youtube.com/watch?v={videoId.VideoID.Trim()}";
            }
        }

        private async Task AddRelatedYTContentAsync(IVoiceChannel vc, ITextChannel chan, ILavaTrack oldTrack)
        {
            (bool failed, YoutubeVideo video) = await this.TryGetVideoAsync(oldTrack);
            string videoUrl = await this.GetNextTrackVideoURLAsync(failed, video);
            SearchResult res = await this.LavaRestClient.SearchTracksAsync(videoUrl);
            List<ILavaTrack> tracks = res.Tracks.ToList();
            if (tracks.Count == 0) return;

            switch (res.LoadType)
            {
                case LoadType.SearchResult:
                case LoadType.TrackLoaded:
                    await this.AddTrackAsync(vc, chan, tracks[0]);
                    break;
                case LoadType.PlaylistLoaded:
                    await this.AddPlaylistAsync(vc, chan, res.PlaylistInfo.Name, tracks);
                    break;
                default:
                    await this.MessageSender.Warning(chan, "music player", "Failed to get/load the next autoplay track");
                    break;
            }
        }

        private async Task OnTrackFinished(LavaPlayer lavalink, ILavaTrack lavaTrack, TrackEndReason reason)
        {
            if (!reason.ShouldPlayNext()) return;

            IEnergizePlayer ply = this.Players[lavalink.VoiceChannel.GuildId];
            if (ply.IsLooping)
            {
                lavaTrack.ResetPosition();
                await ply.Lavalink.PlayAsync(lavaTrack);
            }
            else
            {
                if (ply.Queue.TryDequeue(out IQueueObject obj))
                {
                    if (obj is ILavaTrack newTrack)
                    {
                        await ply.Lavalink.PlayAsync(newTrack);
                        await this.SendPlayerAsync(ply, newTrack);
                    }
                }
                else
                {
                    if (ply.Autoplay && ply.Queue.Count == 0)
                    {
                        await this.AddRelatedYTContentAsync(ply.VoiceChannel, ply.TextChannel, lavaTrack);
                    }
                    else
                    {
                        if (ply.TrackPlayer != null)
                            await ply.TrackPlayer.DeleteMessage();
                    }
                }
            }
        }

        private async Task OnTrackIssue(LavaPlayer ply, ILavaTrack lavaTrack, string error = null)
        {
            if (error != null)
            {
                this.Logger.Nice("MusicPlayer", ConsoleColor.Red, $"Exception thrown by lavalink for track <{lavaTrack.Title}>\n{error}");
                if (lavaTrack.Uri.AbsoluteUri.Contains("soundcloud.com"))
                    error = $"{error} It is likely that this track is not usable outside of SoundCloud.";
            }
            else
            {
                this.Logger.Nice("MusicPlayer", ConsoleColor.Red, $"Track <{lavaTrack.Title}> got stuck");
                error = "The track got stuck.";
            }

            EmbedBuilder builder = new EmbedBuilder();
            builder
                .WithColorType(EmbedColorType.Warning)
                .WithFooter("music player")
                .WithDescription("🎶 Could not play track:")
                .WithField("URL", $"**{lavaTrack.Uri}**")
                .WithField("Error", error);

            await this.MessageSender.Send(ply.TextChannel, builder.Build());
        }

        private delegate Task ReactionCallback(MusicPlayerService music, IEnergizePlayer ply);
        private readonly static Dictionary<string, ReactionCallback> ReactionCallbacks = new Dictionary<string, ReactionCallback>
        {
            ["⏯"] = async (music, ply) =>
            {
                if (!ply.IsPlaying) return;
                if (ply.IsPaused)
                    await music.ResumeTrackAsync(ply.VoiceChannel, ply.TextChannel);
                else
                    await music.PauseTrackAsync(ply.VoiceChannel, ply.TextChannel);
            },
            ["🔁"] = async (music, ply) => await music.LoopTrackAsync(ply.VoiceChannel, ply.TextChannel),
            ["⬆"] = async (music, ply) => await music.SetTrackVolumeAsync(ply.VoiceChannel, ply.TextChannel, ply.Volume + 10),
            ["⬇"] = async (music, ply) => await music.SetTrackVolumeAsync(ply.VoiceChannel, ply.TextChannel, ply.Volume - 10),
            ["⏭"] = async (music, ply) =>
            {
                await ply.TrackPlayer.DeleteMessage();
                await music.SkipTrackAsync(ply.VoiceChannel, ply.TextChannel);
            }
        };

        private bool IsValidReaction(ISocketMessageChannel chan, SocketReaction reaction)
        {
            if (chan is IDMChannel) return false;
            if (reaction.Emote?.Name == null) return false;
            if (reaction.User.GetValueOrDefault() == null) return false;
            if (reaction.User.Value.IsBot || reaction.User.Value.IsWebhook) return false;

            return ReactionCallbacks.ContainsKey(reaction.Emote.Name);
        }

        private bool IsValidTrackPlayer(TrackPlayer trackplayer, ulong msgid)
            => trackplayer != null && trackplayer.Message != null && trackplayer.Message.Id == msgid;

        private async Task OnReaction(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel chan, SocketReaction reaction)
        {
            if (!this.IsValidReaction(chan, reaction)) return;

            IGuildUser guser = (IGuildUser)reaction.User.Value;
            if (!this.Players.TryGetValue(guser.GuildId, out IEnergizePlayer ply) || guser.VoiceChannel == null) return;
            if (!this.IsValidTrackPlayer(ply.TrackPlayer, cache.Id)) return;

            await ReactionCallbacks[reaction.Emote.Name](this, ply);
            if (ply.CurrentRadio != null)
                await ply.TrackPlayer.Update(ply.CurrentRadio, ply.Volume, ply.IsPaused, ply.IsLooping);
            else
                await ply.TrackPlayer.Update(ply.CurrentTrack, ply.Volume, ply.IsPaused, ply.IsLooping, true);
        }

        [Event("ReactionAdded")]
        public async Task OnReactionAdded(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel chan, SocketReaction reaction)
            => await this.OnReaction(cache, chan, reaction);

        [Event("ReactionRemoved")]
        public async Task OnReactionRemoved(Cacheable<IUserMessage, ulong> cache, ISocketMessageChannel chan, SocketReaction reaction)
            => await this.OnReaction(cache, chan, reaction);

        private volatile int CurrentShardCount;
        [Event("ShardReady")]
        public async Task OnShardReady(DiscordSocketClient _)
        {
            if (this.Client.Shards.Count != ++this.CurrentShardCount) return;

            Configuration config = new Configuration
            {
                ReconnectInterval = TimeSpan.FromSeconds(15),
                ReconnectAttempts = 3,
                Host = Config.Instance.Lavalink.Host,
                Port = Config.Instance.Lavalink.Port,
                Password = Config.Instance.Lavalink.Password,
                SelfDeaf = false,
                BufferSize = 8192,
                PreservePlayers = true,
                AutoDisconnect = false,
                LogSeverity = LogSeverity.Debug,
                DefaultVolume = 50,
                InactivityTimeout = TimeSpan.FromMinutes(3),
            };

            this.LavaRestClient = new LavaRestClient(config);
            await this.LavaClient.StartAsync(this.Client, config);
        }

        private SocketVoiceChannel GetVoiceChannel(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            SocketVoiceChannel vc = oldState.VoiceChannel ?? newState.VoiceChannel;
            SocketGuildUser botUser = vc.Guild.CurrentUser;
            if (oldState.VoiceChannel != botUser.VoiceChannel
                && newState.VoiceChannel != botUser.VoiceChannel) // unrelated channel activities
            {
                return null;
            }

            if (newState.VoiceChannel != null)
            {
                if (user.Id == botUser.Id) // we moved of channel
                    return newState.VoiceChannel;

                if (botUser.VoiceChannel == newState.VoiceChannel) // a user joined our channel
                    return newState.VoiceChannel;
            }
            else
            {
                if (oldState.VoiceChannel != null && user.Id != botUser.Id) // user disconnected
                    return oldState.VoiceChannel;
            }

            return vc;
        }

        private async Task DisconnectTaskAsync(IVoiceChannel vc, CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromMinutes(1), token);

            if (token.IsCancellationRequested)
                return;

            await this.DisconnectAsync(vc);
        }

        [Event("UserVoiceStateUpdated")] 
        public async Task OnVoiceStateUpdated(SocketUser user, SocketVoiceState oldState, SocketVoiceState newState)
        {
            SocketVoiceChannel vc = this.GetVoiceChannel(user, oldState, newState);
            if (vc == null) return;
            if (!this.Players.TryGetValue(vc.Guild.Id, out IEnergizePlayer ply)) return;

            if (vc.Users.Count(x => !x.IsBot) < 1)
            {
                ply.DisconnectTask = this.DisconnectTaskAsync(vc, ply.CTSDisconnect.Token);
            }    
            else
            {
                if (ply.DisconnectTask != null)
                {
                    ply.CTSDisconnect.Cancel(false);
                    ply.CTSDisconnect = new CancellationTokenSource();
                    ply.DisconnectTask = null;
                }
            }
        }

        [Event("ShardDisconnected")]
        public async Task OnShardDisconnected(Exception _, DiscordSocketClient client)
        {
            int count = 0;
            foreach(SocketGuild guild in client.Guilds)
            {
                if (this.Players.TryGetValue(guild.Id, out IEnergizePlayer ply))
                {
                    count++;
                    if (ply.VoiceChannel != null)
                    {
                        await this.DisconnectAsync(ply.VoiceChannel);
                        if (ply.TextChannel != null)
                            await this.MessageSender.Warning(ply.TextChannel, "music player", "There was a problem with Discord, disconnecting...");
                    }
                }
            }

            this.Logger.Nice("MusicPlayer", ConsoleColor.Yellow, $"Disconnected {count} players");
        }
    }
}