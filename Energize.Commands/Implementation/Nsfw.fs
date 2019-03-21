﻿namespace Energize.Commands.Implementation

open Energize.Commands.Command

[<CommandModule("NSFW")>]
module Nsfw =
    open Energize.Commands.Context
    open Energize.Commands.AsyncHelper
    open Energize.Essentials
    open Discord
    open System.Xml
    open System
    open Energize.Interfaces.Services.Senders
    open System.Net

    let private buildNsfwEmbed (builder : EmbedBuilder) (ctx : CommandContext) (pic : string) (url : string) = 
        ctx.messageSender.BuilderWithAuthor(ctx.message,builder)
        builder
            .WithColor(ctx.messageSender.ColorGood)
            .WithImageUrl(pic)
            .WithFooter(ctx.commandName)
            .WithDescription(sprintf "\n[**CHECK ON %s**](%s)" (ctx.commandName.ToUpper()) url)
            |> ignore

    type E621Obj = { sample_url : string; id : string }
    [<NsfwCommand>]
    [<CommandParameters(1)>]
    [<Command("e621", "Searches e621", "e621 <tag|search>")>]
    let e621 (ctx : CommandContext) = async {
        let endpoint = "https://e621.net/post/index.json?tags=" + WebUtility.UrlEncode(ctx.input)
        let json = awaitResult (HttpClient.GetAsync(endpoint, ctx.logger))
        let e621Objs = JsonPayload.Deserialize<E621Obj list>(json, ctx.logger)
        return 
            if e621Objs |> List.isEmpty then
                [ ctx.sendWarn None "Nothing was found" ]
            else
                let paginator = ctx.serviceManager.GetService<IPaginatorSenderService>("Paginator")
                [ awaitResult (paginator.SendPaginator(ctx.message, ctx.commandName, e621Objs, Action<E621Obj, EmbedBuilder>(fun obj builder ->
                    buildNsfwEmbed builder ctx obj.sample_url (sprintf "https://e621.net/post/show/%s/" obj.id)
                ))) ]
    }

    let private getDApiResult (ctx : CommandContext) (uri : string) = 
        let endpoint = 
            sprintf "http://%s/index.php?page=dapi&s=post&q=index&tags=%s" uri (WebUtility.UrlEncode(ctx.input))
        let xml = awaitResult (HttpClient.GetAsync(endpoint, ctx.logger))
        if String.IsNullOrWhiteSpace xml then
            None
        else
            let doc = XmlDocument()
            doc.LoadXml(xml)
            let nodes = doc.SelectNodes("//post")
            if nodes.Count < 1 then
                None
            else
                let results =
                    nodes 
                    |> Seq.cast<XmlNode>
                    |> Seq.map (fun node -> 
                        let url = 
                            let n = node.SelectSingleNode("@file_url")
                            if n.Value.StartsWith("//") then
                                sprintf "http:%s" n.Value
                            else 
                                n.Value
                        let id = node.SelectSingleNode("@id").Value
                        let page = sprintf "http://%s/index.php?page=post&s=view&id=%s" uri id
                        (url, page)
                    )
                    |> Seq.distinct
                    |> Seq.toList

                Some results

    let private callDApiCmd (ctx : CommandContext) (uri : string) = async {
        let result = getDApiResult ctx uri
        return 
            match result with
            | Some results ->
                let paginator = ctx.serviceManager.GetService<IPaginatorSenderService>("Paginator")
                [ awaitResult (paginator.SendPaginator(ctx.message, ctx.commandName, results, Action<(string * string), EmbedBuilder>(fun (url, page) builder ->
                     buildNsfwEmbed builder ctx url page
                ))) ]
            | None ->
                [ ctx.sendWarn None "Nothing was found" ]
    }

    [<NsfwCommand>]
    [<CommandParameters(1)>]
    [<Command("furb", "Searches furrybooru", "furb <tags|search>")>]
    let furb (ctx : CommandContext) = 
        callDApiCmd ctx "furry.booru.org"

    [<NsfwCommand>]
    [<CommandParameters(1)>]
    [<Command("r34", "Searches r34", "r34 <tags|search>")>]
    let r34 (ctx : CommandContext) =
        callDApiCmd ctx "rule34.xxx"

    [<NsfwCommand>]
    [<CommandParameters(1)>]
    [<Command("gelb", "Searches gelbooru", "gelb <tags|search>")>]
    let gelb (ctx : CommandContext) =
        callDApiCmd ctx "gelbooru.com"