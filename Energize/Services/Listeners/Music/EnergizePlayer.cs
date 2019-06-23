﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Energize.Essentials.MessageConstructs;
using Energize.Essentials.TrackTypes;
using Energize.Interfaces.Services.Listeners;
using Victoria;
using Victoria.Entities;
using Victoria.Queue;
using Timer = System.Timers.Timer;

namespace Energize.Services.Listeners.Music
{
    // Wrapper class to gain more control over Victoria, and try to prevent weird behaviors
    internal class EnergizePlayer : IEnergizePlayer
    {
        public event Action BecameInactive;

        private readonly double TimeToLive;

        private Timer TTLTimer;

        internal EnergizePlayer(LavaPlayer ply)
        {
            this.Lavalink = ply;
            this.DisconnectTask = null;
            this.CTSDisconnect = new CancellationTokenSource();
            this.Autoplay = false;
            this.IsLooping = false;
            this.Disconnected = false;
            this.TrackPlayer = null;
            this.CurrentRadioLava = null;
            this.Queue = new LavaQueue<ILavaTrack>();
            this.TimeToLive = 3 * 60 * 1000;
            this.TTLTimer = null;
            this.Refresh();
        }

        public LavaPlayer Lavalink { get; set; }

        public Task DisconnectTask { get; set; }
        public CancellationTokenSource CTSDisconnect { get; set; }

        public bool Autoplay { get; set; }
        public bool IsLooping { get; set; }
        public bool Disconnected { get; set; }
        public TrackPlayer TrackPlayer { get; set; }
        public RadioTrack CurrentRadioLava { get; set; }
        public LavaQueue<ILavaTrack> Queue { get; private set; }

        public bool IsPlaying { get =>
            this.Lavalink.IsPlaying; }
        public bool IsPaused { get =>
            this.Lavalink.IsPaused; }
        public ILavaTrack CurrentTrack { get =>
            this.Lavalink?.CurrentTrack; }
        public IVoiceChannel VoiceChannel { get =>
            this.Lavalink?.VoiceChannel; }
        public ITextChannel TextChannel { get =>
            this.Lavalink?.TextChannel; }
        public int Volume { get =>
            this.Lavalink == null ? 100 : this.Lavalink.CurrentVolume; }

        public void Refresh()
        {
            if (this.TTLTimer != null)
            {
                this.TTLTimer.Stop();
                this.TTLTimer.Close();
                this.TTLTimer = null;
            }

            this.TTLTimer = new Timer(this.TimeToLive)
            {
                AutoReset = false
            };

            this.TTLTimer.Elapsed += (_, __) =>
            {
                if (!this.Disconnected)
                    this.BecameInactive?.Invoke();
            };

            this.TTLTimer.Start();
        }
    }
}
