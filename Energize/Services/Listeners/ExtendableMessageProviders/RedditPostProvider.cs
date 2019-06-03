﻿using Discord;
using Discord.WebSocket;
using Energize.Essentials;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Energize.Services.Listeners.ExtendableMessageProviders
{
    class RedditPost
    {
        [JsonProperty("data")]
        public RedditPostData Data { get; set; }
    }

    class RedditPostData
    {
        [JsonProperty("children")]
        public RedditInnerPostData[] Children { get; set; }
    }

    class RedditInnerPostData
    {
        [JsonProperty("data")]
        public RedditInnerPost Data { get; set; }
    }

    class RedditInnerPost
    {
        [JsonProperty("post_hint")]
        public string Type { get; set; }

        [JsonProperty("subreddit")]
        public string SubReddit { get; set; }

        [JsonProperty("selftext")]
        public string Content { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("upvote_ratio")]
        public double UpvoteRatio { get; set; }

        [JsonProperty("locked")]
        public bool Locked { get; set; }

        [JsonProperty("author")]
        public string Author { get; set; }

        [JsonProperty("num_crossposts")]
        public long CrossPostCount { get; set; }

        [JsonProperty("num_comments")]
        public long CommentCount { get; set; }

        [JsonProperty("permalink")]
        public string PermaLink { get; set; }

        [JsonProperty("url")]
        public string URL { get; set; }

        [JsonProperty("subreddit_subscribers")]
        public long SubredditSubscriberCount { get; set; }

        [JsonProperty("is_video")]
        public bool IsVideo { get; set; }
    }

    class RedditPostProvider : BaseProvider
    {
        private readonly Logger Logger;

        public RedditPostProvider(Logger logger, string pattern) : base(pattern)
        {
            this.Logger = logger;
        }

        public override async Task BuildEmbedsAsync(List<Embed> embeds, IUserMessage msg, SocketReaction reaction)
        {
            foreach (Match match in this.Matches(msg.Content))
            {
                string json = await HttpClient.GetAsync($"{match.Value}.json", this.Logger);
                RedditPost[] posts = JsonPayload.Deserialize<RedditPost[]>(json, this.Logger);

                RedditPost post = posts.FirstOrDefault();
                if (post == null) continue;

                RedditInnerPost innerPost = post.Data.Children.FirstOrDefault()?.Data;
                if (innerPost == null) continue;

                string content = innerPost.Content;
                EmbedBuilder builder = new EmbedBuilder();
                builder
                    .WithAuthorNickname(msg)
                    .WithColorType(EmbedColorType.Good)
                    .WithField("SubReddit", $"r/{innerPost.SubReddit}/", false)
                    .WithField("Author", innerPost.Author)
                    .WithField("Upvote Percentage", $"{innerPost.UpvoteRatio * 100}%")
                    .WithField("Comments", innerPost.CommentCount)
                    .WithField("Cross-posts", innerPost.CrossPostCount)
                    .WithField("Locked", innerPost.Locked)
                    .WithField("SubReddit Subscribers", innerPost.SubredditSubscriberCount)
                    .WithUrl($"https://www.reddit.com/{innerPost.PermaLink}")
                    .WithTitle($"**{innerPost.Title}**");

                if (innerPost.IsVideo)
                    builder.WithDescription($"Video post, [**open in your browser**]({innerPost.URL}/DASH_720?source=fallback) to see it.");
                else
                    switch (innerPost.Type)
                    {
                        case "image":
                            builder.WithImageUrl(innerPost.URL);
                            break;
                        default:
                            builder.WithLimitedDescription(string.IsNullOrWhiteSpace(content) ? "Empty post." : content);
                            break;
                    }

                embeds.Add(builder.Build());
            }
        }
    }
}
