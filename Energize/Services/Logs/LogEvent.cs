﻿using System;
using Discord.Rest;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;

namespace Energize.Services.Logs
{
    [Service("EventLogs")]
    public class LogEvent
    {
        private DiscordRestClient _RESTClient;
        private DiscordSocketClient _Client;
        private string _Prefix;
        private EnergizeLog _Log;

        public LogEvent(EnergizeClient eclient)
        {
            this._Client = eclient.Discord;
            this._RESTClient = eclient.DiscordREST;
            this._Prefix = eclient.Prefix;
            this._Log = eclient.Log;
        }

        public DiscordRestClient RESTClient { get => this._RESTClient; }
        public DiscordSocketClient Client { get => this._Client; }
        public string Prefix { get => this._Prefix; }
        public EnergizeLog Log { get => this._Log; }

        public bool AreLogsEnabled(SocketGuild guild)
        {
            return guild.Roles.Any(x => x.Name == "EnergizeLogs");
        }

        [Event("Connected")]
        public async Task OnConnected()
        {
            this._Log.Notify("Ready");
        }

        [Event("GuildAvailable")]
        public async Task OnGuildAvailable(SocketGuild guild)
        {
            this._Log.Nice("Guild", ConsoleColor.Magenta, "Online on " + guild.Name + " || ID => [ " + guild.Id + " ]");
        }

        [Event("GuildUnavailable")]
        public async Task OnGuildUnavailable(SocketGuild guild)
        {
            this._Log.Nice("Guild", ConsoleColor.Red, "Offline from " + guild.Name + " || ID => [ " + guild.Id + " ]");
        }

        [Event("JoinedGuild")]
        public async Task OnJoinedGuild(SocketGuild guild)
        {
            this._Log.Nice("Guild", ConsoleColor.Magenta, "Joined " + guild.Name + " || ID => [ " + guild.Id + " ]");
        }

        [Event("LeftGuild")]
        public async Task OnLeftGuild(SocketGuild guild)
        {
            this._Log.Nice("Guild", ConsoleColor.Red, "Left " + guild.Name + " || ID => [ " + guild.Id + " ]");
        }
    }
}