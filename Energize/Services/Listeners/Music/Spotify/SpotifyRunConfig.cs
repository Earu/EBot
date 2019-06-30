using Energize.Essentials;
using Energize.Services.Listeners.Music.Spotify.Helpers;
using SpotifyAPI.Web;
using Victoria;

namespace Energize.Services.Listeners.Music.Spotify
{
    internal class SpotifyRunConfig
    {
        public LavaRestClient LavaRest { get; set; }

        public SpotifyWebAPI Api { get; set; }

        public SpotifyConfig Config { get; set; }

        public SpotifyTrackConverter TrackConverter { get; set; }

        public SpotifyRunConfig(
            LavaRestClient lavaRest,
            SpotifyWebAPI api,
            SpotifyConfig config,
            SpotifyTrackConverter trackConverter)
        {
            LavaRest = lavaRest;
            Api = api;
            Config = config;
            TrackConverter = trackConverter;
        }
    }
}