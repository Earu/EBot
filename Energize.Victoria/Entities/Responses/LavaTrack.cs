using Newtonsoft.Json;
using System;

namespace Victoria.Entities
{
    public sealed class LavaTrack : ILavaTrack
    {
        [JsonIgnore]
        public string Hash { get; set; }

        [JsonProperty("identifier")]
        public string Id { get; internal set; }

        [JsonProperty("isSeekable")]
        public bool IsSeekable { get; internal set; }

        [JsonProperty("author")]
        public string Author { get; internal set; }

        [JsonProperty("isStream")]
        public bool IsStream { get; internal set; }

        [JsonIgnore]
        public TimeSpan Position
        {
            get => new TimeSpan(this.TrackPosition);
            set => this.TrackPosition = value.Ticks;
        }

        [JsonProperty("position")]
        internal long TrackPosition { get; set; }

        [JsonIgnore]
        public TimeSpan Length
        {
            get {
                if (this.TrackLength <= 0)
                    return TimeSpan.Zero;
                
                if (this.TrackLength >= TimeSpan.MaxValue.Ticks || double.IsInfinity(this.TrackLength))
                    return TimeSpan.MaxValue;

                return TimeSpan.FromMilliseconds(this.TrackLength);
            }
            set => this.TrackLength = value.Milliseconds;
        }

        [JsonIgnore]
        public bool HasLength => this.Length < TimeSpan.MaxValue && this.Length > TimeSpan.Zero;

        [JsonProperty("length")]
        internal long TrackLength { get; set; }

        [JsonProperty("title")]
        public string Title { get; internal set; }

        [JsonProperty("uri")]
        public Uri Uri { get; internal set; }

        [JsonIgnore]
        public string Provider => this.Uri.GetProvider();

        /// <summary>
        /// 
        /// </summary>
        public void ResetPosition() 
            => this.Position = TimeSpan.Zero;
    }
}
