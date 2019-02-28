﻿using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Energize.Interfaces.Services
{
    public interface IPaginatorSenderService : IServiceImplementation
    {
        Task SendPaginator<T>(SocketMessage msg, string head, IEnumerable<T> data, Func<T, string> displaycallback) where T : class;

        Task SendPaginatorRaw<T>(SocketMessage msg, IEnumerable<T> data, Func<T, string> displaycallback) where T : class;
    }
}
