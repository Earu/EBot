﻿using DSharpPlus;
using DSharpPlus.Entities;
using EBot.Commands;
using EBot.Logs;
using EBot.MachineLearning;
using EBot.MemoryStream;
using EBot.Utils;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EBot
{
    public class EBotClient
    {
        public static EBotClient CLIENT;

        private string _Prefix;
        private DiscordClient _Discord;
        private CommandsHandler _Handler;
        private CommandSource _Source;
        private BotLog _Log;
        private LogEvent _Event;
        private SpyLog _Spy;

        public EBotClient(string token,string prefix)
        {
            Console.Clear();
            Console.Title = "EBot's Logs";

            this._Prefix = prefix;
            this._Log = new BotLog();
            this._Event = new LogEvent();
            this._Spy = new SpyLog();
            this._Handler = new CommandsHandler();
            this._Source = new CommandSource(this._Handler,this._Log);
            this._Discord = new DiscordClient(new DiscordConfiguration
            {
                Token = token,
                TokenType = TokenType.Bot,
            });

            this._Log.Nice("Config", ConsoleColor.Yellow, "Token used => [ " + token + " ]");
            this._Log.Notify("Initializing");

            this._Handler.Client = this._Discord;
            this._Handler.Log = this._Log;
            this._Handler.Prefix = this._Prefix;
            this._Handler.Source = this._Source;
            this._Handler.EmbedReply.Log = this._Log;
            this._Handler.Initialize();

            this._Spy.Client = this._Discord;
            this._Spy.Log = this._Log;
            this._Spy.WatchWords(new string[] { "yara", "earu" });

            this._Event.Client = this._Discord;
            this._Event.Prefix = this._Prefix;
            this._Event.Log = this._Log;
            this._Event.InitEvents();

            CLIENT = this;
        }

        public string Prefix { get => this._Prefix; set => this._Prefix = value; }
        public DiscordClient Discord { get => this._Discord; set => this._Discord = value; }
        public CommandsHandler Handler { get => this._Handler; set => this._Handler = value; }
        public CommandSource Source { get => this._Source; set => this._Source = value; }
        public BotLog Log { get => this._Log; set => this._Log = value; }
        public LogEvent Event { get => this._Event; set => this._Event = value; }
        public SpyLog Spy { get => this._Spy; set => this._Spy = value; }

        private async Task AskAsync(DiscordMessage msg,bool ismention,bool isprefix)
        {
            string mention = "<@" + EBotCredentials.BOT_ID_MAIN + ">";
            DiscordChannel chan = msg.Channel;
            string username = msg.Author.Username;
            string input = msg.Content;

            if (ismention)
            {
                if (isprefix)
                {
                    input = input.Remove(0, mention.Length + 1);
                }
                else
                {
                    input = input.Replace(mention, "EBot");
                }
            }

            string result = await ChatBot.Ask(chan, input, this._Log);

            await this._Handler.EmbedReply.Normal(msg, username, result);
        }

        private async Task OnChatAsync(DiscordMessage msg)
        {
            string mention = "<@" + EBotCredentials.BOT_ID_MAIN + "> ";
            bool answered = false;

            if (msg.Content.StartsWith(mention))
            {
                answered = true;
                await this.AskAsync(msg, true, true);
            }

            if (!answered)
            {
                bool mentionned = false;
                foreach(DiscordUser user in msg.MentionedUsers)
                {
                    if(user.Id == EBotCredentials.BOT_ID_MAIN)
                    {
                        mentionned = true;
                    }
                }

                if (mentionned)
                {
                    answered = true;
                    await this.AskAsync(msg, true, false);
                }
            }

            if (answered)
            {
                string name = "(" + msg.Channel.Guild.Name + " - #" + msg.Channel.Name + ") ";
                this._Log.Nice("ChatBot", ConsoleColor.DarkGreen, name + "Answered " + msg.Author.Username + "#" + msg.Author.Discriminator);
            }

            if (!msg.Author.IsBot && !msg.Channel.IsNSFW)
            {
                await MarkovHandler.Learn(msg.Content);
            }
        }

        public async Task ConnectAsync()
        {
            try
            {
                await this._Discord.ConnectAsync();

                this._Discord.Ready += async e =>
                {
                    DiscordGame game = new DiscordGame(this._Prefix + "help")
                    {
                        StreamType = GameStreamType.Twitch,
                        Url = EBotCredentials.TWITCH_URL
                    };

                    await this._Discord.UpdateStatusAsync(game, UserStatus.Online); //fancy streaming mode
                    ClientMemoryStream.Initialize(this);
                    LuaEnv.Initialize(this);
                };

                this._Discord.MessageCreated += async e =>
                {
                    this.OnChatAsync(e.Message).RunSynchronously();
                };

                Timer gctimer = new Timer(arg =>
                {
                    long mb = GC.GetTotalMemory(false)/1024/1024; //b to mb
                    GC.Collect();
                    this._Log.Nice("GC", ConsoleColor.Gray, "Collected " + mb + "MB of garbage");
                });

                int hour = 1000 * 60 * 60;
                gctimer.Change(hour, hour);
                
            }
            catch (Exception e)
            {
                this._Log.Nice("Init", ConsoleColor.Red, "Failed to connect");
                this._Log.Error(e.ToString());
            }
        }
    }
}
