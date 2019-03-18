﻿using Energize.Interfaces.DatabaseModels;
using Energize.Interfaces.Services.Database;
using Energize.Services.Database.Models;
using Energize.Toolkit;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Energize.Services.Database
{
    public class Database : DbContext, IDatabase
    {
        private readonly string _ConnectionString;

        public DbSet<DiscordUser> Users { get; set; }
        public DbSet<DiscordGuild> Guilds { get; set; }
        public DbSet<DiscordChannel> Channels { get; set; }
        public DbSet<DiscordUserStats> Stats { get; set; }

        public Database(string connectionstring)
        {
            this._ConnectionString = connectionstring;
        }
    
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(this._ConnectionString);
        }

        public void Save()
            => this.SaveChanges(true);

        public async Task<IDiscordUser> GetOrCreateUser(ulong id)
        {
            DiscordUser user = await this.Users.FirstOrDefaultAsync(x => x.ID == id);
            if (user != null)
            {
                return user;
            }
            else
            {
                user = new DiscordUser(id);
                this.Users.Add(user);
                await this.SaveChangesAsync(true);

                return user;
            }
        }

        public async Task<IDiscordUserStats> GetOrCreateUserStats(ulong id)
        {
            DiscordUserStats stats = await this.Stats.FirstOrDefaultAsync(x => x.ID == id);
            if(stats != null)
            {
                return stats;
            }
            else
            {
                stats = new DiscordUserStats(id);
                this.Stats.Add(stats);
                await this.SaveChangesAsync(true);

                return stats;
            }
        }

        public async Task<IDiscordGuild> GetOrCreateGuild(ulong id)
        {
            DiscordGuild guild = await this.Guilds.FirstOrDefaultAsync(x => x.ID == id);
            if(guild != null)
            {
                return guild;
            }
            else
            {
                guild = new DiscordGuild(id);
                this.Guilds.Add(guild);
                await this.SaveChangesAsync(true);

                return guild;
            }
        }
    }

    [Service("Database")]
    public class DBContextPool : IDatabaseService
    {
        private readonly string _ConnectionString;
        private List<DBContext> _Pool = new List<DBContext>();

        public DBContextPool(EnergizeClient client)
        {
            this._ConnectionString = Config.Instance.DBConnectionString;

            for (uint i = 0; i < 10; i++)
            {
                _Pool.Add(new DBContext(this.Create(),client.Logger));
            }
        }

        public async Task<IDatabaseContext> GetContext()
        {
            for(int i = 0; i < this._Pool.Count; i++)
            {
                DBContext ctx = this._Pool[i];
                if(!ctx.IsUsed)
                {
                    ctx.IsUsed = true;
                    return ctx;
                }
            }

            //Wait a bit so we dont try again too early
            await Task.Delay(100);
            return await this.GetContext();
        }

        public void Initialize() { }

        public Task InitializeAsync()
            => Task.CompletedTask;

        private Database Create()
        {
            var context = new Database(Config.Instance.DBConnectionString);
            context.Database.EnsureCreated();

            return context;
        }
    }
}
