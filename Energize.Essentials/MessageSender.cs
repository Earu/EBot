﻿using Discord;
using Discord.WebSocket;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Energize.Essentials
{
    public enum ThumbnailType
    {
        None,
        //Error,
        //Warning,
        Music,
        Radio,
        Update,
        //NoResult,
    }

    public class MessageSender
    {
        private readonly HttpClient HttpClient;

        public MessageSender(Logger logger)
        {
            this.Logger = logger;
            this.HttpClient = new HttpClient();
        }

        public static Color SColorWarning { get; } = new Color(226, 123, 68);
        public static Color SColorDanger { get; } = new Color(226, 68, 68);
        public static Color SColorSpecial { get; } = new Color(165, 28, 21);

        public Logger Logger { get; }

        private void LogFailedMessage(IChannel chan, Exception ex)
        {
            string log = string.Empty;
            if (chan is IGuildChannel guildChan)
            {
                log += $"({guildChan.Guild.Name} - #{guildChan.Name}): {ex.Message}";
            }
            else
            {
                IDMChannel dmChan = (IDMChannel)chan;
                log += $"(DM) {dmChan.Recipient}: {ex.Message}";
            }
            this.Logger.Nice("MessageSender", ConsoleColor.Red, log);
        }

        private bool CanSendMessage(IChannel chan)
        {
            if (chan == null) return false;

            if (chan is SocketGuildChannel guildChannel)
            {
                SocketGuildUser botUser = guildChannel.Guild.CurrentUser;
                return botUser.GetPermissions(guildChannel).SendMessages;
            }

            return true;
        }

        public async Task TriggerTypingAsync(IChannel c)
        {
            try
            {
                IMessageChannel chan = c as IMessageChannel;
                if (!this.CanSendMessage(chan)) return;

                await chan.TriggerTypingAsync();
            }
            catch(Exception ex)
            {
                this.LogFailedMessage(c, ex);
            }
        }

        private bool TryGetThumbnail(ThumbnailType thumbnailType, out string fileName, out string filePath)
        {
            if (thumbnailType == ThumbnailType.None)
            {
                fileName = null;
                filePath = null;
                return false;
            }

            fileName = $"{thumbnailType.ToString().ToLower()}.png";
            filePath = $"volta/{fileName}";
            return true;
        }

        public async Task<IUserMessage> SendAsync(IChannel c, string header, string content, EmbedColorType colorType = EmbedColorType.Good, ThumbnailType thumbnailType = ThumbnailType.None, IMessage msg = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(content)) return null;
                IMessageChannel chan = c as IMessageChannel;
                if (!this.CanSendMessage(chan)) return null;

                EmbedBuilder builder = new EmbedBuilder();
                builder
                    .WithColorType(colorType)
                    .WithLimitedDescription(content)
                    .WithFooter(header);

                if (msg != null)
                    builder.WithAuthorNickname(msg);

                if (this.TryGetThumbnail(thumbnailType, out string fileName, out string filePath))
                {
                    builder.WithThumbnailUrl($"attachment://{fileName}");
                    return await chan.SendFileAsync(filePath, string.Empty, false, builder.Build());
                }

                return await chan.SendMessageAsync(string.Empty, false, builder.Build());
            }
            catch(Exception ex)
            {
                this.LogFailedMessage(c, ex);
            }

            return null;
        }

        public async Task<IUserMessage> SendAsync(IMessage msg, string header, string content, EmbedColorType colorType = EmbedColorType.Good, ThumbnailType thumbnailType = ThumbnailType.None)
            => await this.SendAsync(msg.Channel, header, content, colorType, thumbnailType, msg);

        public async Task<IUserMessage> SendAsync(IChannel chan, Embed embed, ThumbnailType thumbType = ThumbnailType.None)
        {
            EmbedBuilder builder = embed.ToEmbedBuilder();
            if (!embed.Color.HasValue)
                builder.Color = null;

            return await this.SendAsync(chan, builder, thumbType);
        }

        public async Task<IUserMessage> SendAsync(IChannel c, EmbedBuilder builder, ThumbnailType thumbType = ThumbnailType.None)
        {
            try
            {
                if (builder == null) return null;
                IMessageChannel chan = c as IMessageChannel;
                if (!this.CanSendMessage(chan)) return null;

                if (this.TryGetThumbnail(thumbType, out string fileName, out string filePath))
                {
                    builder.WithThumbnailUrl($"attachment://{fileName}");
                    return await chan.SendFileAsync(filePath, string.Empty, false, builder.Build());
                }

                return await chan.SendMessageAsync(string.Empty, false, builder.Build());
            }
            catch(Exception ex)
            {
                this.LogFailedMessage(c, ex);
            }

            return null;
        }

        public async Task<IUserMessage> SendAsync(IMessage msg, EmbedBuilder builder, ThumbnailType thumbType = ThumbnailType.None)
            => await this.SendAsync(msg.Channel, builder, thumbType);

        public async Task<IUserMessage> SendAsync(IMessage msg, Embed embed, ThumbnailType thumbType = ThumbnailType.None)
        {
            EmbedBuilder builder = embed.ToEmbedBuilder();
            if (!embed.Color.HasValue)
                builder.Color = null;

            return await this.SendAsync(msg, builder, thumbType);
        }

        public async Task<IUserMessage> SendRawAsync(IChannel c, string content)
        {
            try
            {
                IMessageChannel chan = c as IMessageChannel;
                if (!this.CanSendMessage(chan)) return null;
                return await chan.SendMessageAsync(content);
            }
            catch(Exception ex)
            {
                this.LogFailedMessage(c, ex);
            }

            return null;
        }

        public async Task<IUserMessage> SendRawAsync(IMessage msg, string content)
            => await this.SendRawAsync(msg.Channel, content);

        public async Task<IUserMessage> RepostMessageAsync(IChannel c, IMessage msg, Embed embed = null)
        {
            try
            {
                if (msg == null) return null;
                IMessageChannel chan = c as IMessageChannel;
                if (!this.CanSendMessage(chan)) return null;

                if (msg.Attachments.Count > 0)
                {
                    IAttachment attachment = msg.Attachments.First();
                    using (Stream stream = await this.HttpClient.GetStreamAsync(attachment.ProxyUrl))
                        return await chan.SendFileAsync(stream, attachment.Filename, msg.Content, embed: embed);
                }
                else
                {
                    return await chan.SendMessageAsync(msg.Content, embed: embed);
                }
            }
            catch (Exception ex)
            {
                this.LogFailedMessage(c, ex);
            }

            return null;
        }

        public async Task<IUserMessage> SendWarningAsync(IMessage msg, string header, string content, ThumbnailType thumbType = ThumbnailType.None)
            => await this.SendAsync(msg, header, content, EmbedColorType.Warning, thumbType);

        public async Task<IUserMessage> SendWarningAsync(IChannel chan, string header, string content, ThumbnailType thumbType = ThumbnailType.None)
            => await this.SendAsync(chan, header, content, EmbedColorType.Warning, thumbType);

        public async Task<IUserMessage> SendDangerAsync(IMessage msg, string header, string content, ThumbnailType thumbType = ThumbnailType.None)
            => await this.SendAsync(msg, header, content, EmbedColorType.Danger, thumbType);

        public async Task<IUserMessage> SendDangerAsync(IChannel chan, string header, string content, ThumbnailType thumbType = ThumbnailType.None)
            => await this.SendAsync(chan, header, content, EmbedColorType.Danger, thumbType);

        public async Task<IUserMessage> SendGoodAsync(IMessage msg, string header, string content, ThumbnailType thumbType = ThumbnailType.None)
            => await this.SendAsync(msg, header, content, EmbedColorType.Good, thumbType);

        public async Task<IUserMessage> SendGoodAsync(IChannel chan, string header, string content, ThumbnailType thumbType = ThumbnailType.None)
            => await this.SendAsync(chan, header, content, EmbedColorType.Good, thumbType);
    }
}
