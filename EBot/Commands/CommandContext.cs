﻿using Discord;
using Discord.Rest;
using Discord.WebSocket;
using EBot.Logs;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EBot.Commands
{
    public class CommandContext
    {
        private DiscordSocketClient _Client;
        private DiscordRestClient _RESTClient;
        private string _Cmd;
        private SocketMessage _Message;
        private List<string> _Args;
        private string _Prefix;
        private CommandReplyEmbed _EmbedReply;
        private string _LastPictureURL;
        private BotLog _Log;
        private Dictionary<string, Command> _Cmds;
        private List<SocketGuildUser> _GuildCachedUsers;
        private bool _IsPrivate;
        private CommandHandler _Handler;

        public DiscordSocketClient Client { get => this._Client; set => this._Client = value; }
        public DiscordRestClient RESTClient { get => this._RESTClient; set => this._RESTClient = value; }
        public string Command { get => this._Cmd; set => this._Cmd = value; }
        public SocketMessage Message { get => this._Message; set => this._Message = value; }
        public List<string> Arguments { get => this._Args; set => this._Args = value; }
        public string Prefix { get => this._Prefix; set => this._Prefix = value; }
        public CommandReplyEmbed EmbedReply { get => this._EmbedReply; set => this._EmbedReply = value; }
        public string LastPictureURL { get => this._LastPictureURL; set => this._LastPictureURL = value; }
        public BotLog Log { get => this._Log; set => this._Log = value; }
        public Dictionary<string,Command> Commands { get => this._Cmds; set => this._Cmds = value; }
        public bool IsPrivate { get => this._IsPrivate; set => this._IsPrivate = value; }
        public List<SocketGuildUser> GuildCachedUsers { get => this._GuildCachedUsers; set => this._GuildCachedUsers = value; }
        public CommandHandler Handler { get => this._Handler; set => this._Handler = value; }

        public bool TryGetUser(string input,out SocketUser user)
        {
            input = input.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(input))
            {
                user = null;
                return false;
            }

            if(this._Message.MentionedUsers.Count > 0)
            {
                foreach (SocketUser u in this._Message.MentionedUsers)
                {
                    if (input.Contains(@"<@" + u.Id + ">") || input.Contains(@"<@!" + u.Id + ">"))
                    {
                        user = u;
                        return true;
                    }
                }
            }
            else
            {
                if (!this._IsPrivate)
                {
                    foreach (SocketGuildUser u in this._GuildCachedUsers)
                    {
                        string nick = u.Nickname ?? u.Username;
                        if (nick.ToLower().Contains(input))
                        {
                            user = u;
                            return true;
                        }
                    }
                }
            }

            user = null;
            return false;
        }
    }
}
