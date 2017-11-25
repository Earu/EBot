﻿using Discord.WebSocket;
using EBot.Commands.Social;
using EBot.Logs;
using EBot.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EBot.Commands.Modules
{
    [CommandModule(Name="Social")]
    class SocialCommands : CommandModule,ICommandModule
    {
        private delegate string ActionCallback(SocketUser from, IReadOnlyList<SocketUser> to);

        private void Action(CommandContext ctx,string what,ActionCallback callback)
        {
            string result = "";
            List<SocketUser> users = new List<SocketUser>();
            foreach (string input in ctx.Arguments)
            {
                if (ctx.TryGetUser(input, out SocketUser u))
                {
                    users.Add(u);
                }
            }

            if (users.Count > 0)
            {
                result = callback(ctx.Message.Author, users);
                ctx.EmbedReply.Good(ctx.Message, what, result);
            }
            else
            {
                ctx.EmbedReply.Danger(ctx.Message, what, "You need to mention the persons you want to " + what.ToLower());
            }
        }

        [Command(Name="hug",Help="Hugs people",Usage="hug <@user>,<@user|nothing>,...")]
        private async Task Hug(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Hug", action.Hug);
        }

        [Command(Name = "boop", Help = "Boops people", Usage = "boop <@user>,<@user|nothing>,...")]
        private async Task Boop(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Boop", action.Boop);
        }

        [Command(Name = "slap", Help = "Slaps people", Usage = "slap <@user>,<@user|nothing>,...")]
        private async Task Slap(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Slap", action.Slap);
        }

        [Command(Name = "kiss", Help = "Kisses people", Usage = "kiss <@user>,<@user|nothing>,...")]
        private async Task Kiss(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Kiss", action.Kiss);
        }

        [Command(Name = "snuggle", Help = "Snuggles people", Usage = "snuggle <@user>,<@user|nothing>,...")]
        private async Task Snuggle(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Snuggle", action.Snuggle);
        }

        [Command(Name = "shoot",Help = "Shoots people", Usage = "shoot <@user>,<@user|nothing>,...")]
        private async Task Shoot(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Shoot", action.Shoot);
        }

        [Command(Name = "pet", Help = "Pets people", Usage = "pet <@user>,<@user|nothing>,...")]
        private async Task Pet(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Pet", action.Pet);
        }

        [Command(Name = "spank", Help = "Spanks people", Usage = "spank <@user>,<@user|nothing>,...")]
        private async Task Spank(CommandContext ctx)
        {
            Social.Action action = new Social.Action();
            Action(ctx, "Spank", action.Spank);
        }

        [Command(Name = "love", Help = "Gets a love percentage between two users", Usage = "love <@user>,<@user>")]
        private async Task Love(CommandContext ctx)
        {
            string endpoint = "https://love-calculator.p.mashape.com/getPercentage?";
            if(ctx.TryGetUser(ctx.Arguments[0],out SocketUser u1) && ctx.TryGetUser(ctx.Arguments[1],out SocketUser u2))
            {
                endpoint += "fname=" + u1.Username + "&";
                endpoint += "sname=" + u2.Username;

                string json = await HTTP.Fetch(endpoint, ctx.Log, null, req =>
                {
                    req.Headers[System.Net.HttpRequestHeader.Accept] = "text/plain";
                    req.Headers["X-Mashape-Key"] = EBotCredentials.MASHAPE_KEY;
                });

                LoveObject love = JSON.Deserialize<LoveObject>(json, ctx.Log);

                ctx.EmbedReply.Good(ctx.Message, "Love", u1.Mention + " & " + u2.Mention + "\n:heartbeat: \t" + love.percentage + "%\n" + love.result);
            }
            else
            {
                ctx.EmbedReply.Danger(ctx.Message, "Love", "You need to mention two persons!");
            }
        }

        public void Initialize(CommandHandler handler,BotLog log)
        {
            handler.LoadCommand(this.Hug);
            handler.LoadCommand(this.Boop);
            handler.LoadCommand(this.Slap);
            handler.LoadCommand(this.Love);
            handler.LoadCommand(this.Kiss);
            handler.LoadCommand(this.Snuggle);
            handler.LoadCommand(this.Shoot);
            handler.LoadCommand(this.Pet);
            handler.LoadCommand(this.Spank);

            log.Nice("Module", ConsoleColor.Green, "Initialized " + this.GetModuleName());
        }
    }
}
