using System.IO;
using Microsoft.EntityFrameworkCore;
using MySql.Data.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;
using Roki.Core.Services.Impl;

namespace Roki.Core.Services.Database
{
//    public class RokiContextFactory : IDesignTimeDbContextFactory<RokiContext>
//    {
//        public RokiContext CreateDbContext(string[] args)
//        {
//            var optionsBuilder = new DbContextOptionsBuilder<RokiContext>();
//            IRokiConfig config = new RokiConfig();
//            var builder = new SqliteConnectionStringBuilder(config.Db.ConnectionString);
//            builder.DataSource = Path.Combine(Directory.GetCurrentDirectory(), builder.DataSource);
//            optionsBuilder.UseSqlite(builder.ToString());
//            var ctx = new RokiContext(optionsBuilder.Options);
//            ctx.Database.SetCommandTimeout(60);
//            return ctx;
//        }
//    }

    public class RokiContext : DbContext
    {
        public RokiContext(DbContextOptions options) : base(options)
        {
        }

        public DbSet<Quote> Quotes { get; set; }

//        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//        {
//            optionsBuilder.UseMySQL("server=localhost;database=roki;user=roki;password=roki");
//        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region QUOTES

            modelBuilder.Entity<Quote>(entity =>
            {
                entity.HasIndex(x => x.GuildId);
                entity.HasIndex(x => x.Keyword);
            });

            #endregion
        }
    }
}