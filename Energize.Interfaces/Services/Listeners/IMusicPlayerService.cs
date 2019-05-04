﻿using Discord;
using System.Collections.Generic;
using System.Threading.Tasks;
using Victoria;
using Victoria.Entities;

namespace Energize.Interfaces.Services.Listeners
{
    public interface IMusicPlayerService : IServiceImplementation
    {
        LavaRestClient LavaRestClient { get; }

        Task<IEnergizePlayer> ConnectAsync(IVoiceChannel vc, ITextChannel chan);

        Task DisconnectAsync(IVoiceChannel vc);

        Task DisconnectAllPlayersAsync();

        Task<IUserMessage> AddTrack(IVoiceChannel vc, ITextChannel chan, LavaTrack track);

        Task<IUserMessage> AddPlaylist(IVoiceChannel vc, ITextChannel chan, string name, IEnumerable<LavaTrack> tracks);

        Task<bool> LoopTrack(IVoiceChannel vc, ITextChannel chan);

        Task ShuffleTracks(IVoiceChannel vc, ITextChannel chan);

        Task ClearTracks(IVoiceChannel vc, ITextChannel chan);

        Task PauseTrack(IVoiceChannel vc, ITextChannel chan);

        Task ResumeTrack(IVoiceChannel vc, ITextChannel chan);

        Task SkipTrack(IVoiceChannel vc, ITextChannel chan);

        Task SetTrackVolume(IVoiceChannel vc, ITextChannel chan, int vol);

        Task<string> GetTrackLyrics(IVoiceChannel vc, ITextChannel chan);

        ServerStats GetLavalinkStats();

        Task<IUserMessage> SendQueue(IVoiceChannel vc, IMessage msg);

        Task<IUserMessage> SendNewTrack(IVoiceChannel vc, IMessage msg, LavaTrack track);

        Task<IUserMessage> SendNewTrack(IVoiceChannel vc, ITextChannel chan, LavaTrack track);

        Task<IUserMessage> SendPlayer(IEnergizePlayer ply, LavaTrack track = null);
    }
}
