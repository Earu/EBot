﻿using Discord;
using Energize.Essentials.TrackTypes;
using System;
using System.Threading.Tasks;
using Victoria.Entities;
using Victoria.Queue;

namespace Energize.Essentials.MessageConstructs
{
    public class TrackPlayer
    {

        private DateTime LastUpdate;

        public TrackPlayer(ulong guildid)
        {
            this.GuildID = guildid;
            this.LastUpdate = DateTime.MinValue;
        }

        public IUserMessage Message { get; set; }
        public Embed Embed { get; private set; }
        public ulong GuildID { get; private set; }

        private string FormattedTrack(ILavaTrack track)
        {
            string len, pos, line;

            if (!track.HasLength)
            {
                len = TimeSpan.Zero.ToString(@"hh\:mm\:ss");
                pos = len;
                line = new string('─', 20) + "⚪";
            }
            else
            {
                len = track.Length.ToString(@"hh\:mm\:ss");
                pos = track.Position.ToString(@"hh\:mm\:ss");
                double perc = (double)track.Position.Ticks / track.Length.Ticks * 100.0;
                int circlepos = Math.Clamp((int)Math.Ceiling(21.0 / 100.0 * perc), 0, 21); //Make sure its clamped
                if (circlepos > 0)
                    line = new string('─', circlepos - 1) + "⚪" + new string('─', 21 - circlepos);
                else
                    line = "⚪" + new string('─', 20);
            }

            return $"```http\n▶ {line} {pos} / {len}\n```";
        }

        private Embed BuildTrackEmbed(ILavaTrack track, int volume, bool paused, bool looping)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder
                .WithLimitedTitle(track.Title)
                .WithField("Author", track.Author)
                .WithField("Stream", track.IsStream)
                .WithField("Volume", $"{volume}%")
                .WithField("Paused", paused)
                .WithField("Looping", looping)
                .WithFooter("music player");

            string url = track.Uri.AbsoluteUri;
            if (url.Length < 1000)
                builder.WithDescription($"🎶 Now playing the **[following track]({url})**");
            else
                builder.WithDescription("🎶 Now playing the following track");

            builder.WithField("Length", this.FormattedTrack(track), false);

            return builder.Build();
        }

        private Embed BuildRadioEmbed(RadioTrack radio, int volume, bool paused)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder
                .WithDescription("📻 Playing radio")
                .WithField("Genre", radio.Genre)
                .WithField("Raw Stream", $"**{radio.StreamUrl}**")
                .WithField("Volume", $"{volume}%")
                .WithField("Paused", paused)
                .WithFooter("music player");

            return builder.Build();
        }

        private Embed BuildUnknownEmbed(IQueueObject obj, int volume, bool paused, bool looping)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder
                .WithColorType(EmbedColorType.Warning)
                .WithDescription("🎶 Playing unknown type of content")
                .WithField("ID", obj.Id)
                .WithField("Volume", $"{volume}%")
                .WithField("Paused", paused)
                .WithField("Looping", looping)
                .WithFooter("music player");

            return builder.Build();
        }

        private Embed BuildEmbed(IQueueObject obj, int volume, bool paused, bool looping)
        {
            switch (obj)
            {
                case ILavaTrack track:
                    return this.BuildTrackEmbed(track, volume, paused, looping);
                case RadioTrack radio:
                    return this.BuildRadioEmbed(radio, volume, paused);
                default:
                    return this.BuildUnknownEmbed(obj, volume, paused, looping);
            }
        }

        public async Task DeleteMessage()
        {
            if (this.Message == null) return;
            try
            {
                await this.Message.DeleteAsync();
                this.Message = null;
            }
            catch
            {
                this.Message = null;
            }
        }

        public async Task Update(IQueueObject track, int volume, bool paused, bool looping, bool modify = true)
        {
            if (track == null) return;

            if (!modify)
            {
                this.Embed = this.BuildEmbed(track, volume, paused, looping);
                return;
            }

            if (this.Message == null) return;

            this.Embed = this.BuildEmbed(track, volume, paused, looping);
            if ((DateTime.Now - this.LastUpdate).TotalSeconds < 2)
            {
                this.LastUpdate = DateTime.Now;
                return;
            }

            this.LastUpdate = DateTime.Now;
            await this.Message.ModifyAsync(prop => prop.Embed = this.Embed);
        }
    }
}
