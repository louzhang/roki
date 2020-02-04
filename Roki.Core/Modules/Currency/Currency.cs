using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;
using Roki.Services.Database.Core;

namespace Roki.Modules.Currency
{
    public partial class Currency : RokiTopLevelModule
    {
        private readonly DbService _db;
        private readonly ICurrencyService _currency;

        public Currency(DbService db, ICurrencyService currency)
        {
            _db = db;
            _currency = currency;
        }

        private decimal GetInvAccount(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.Users.GetUserInvestingAccount(userId);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Cash([Leftover] IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{user.Mention}'s Cash Account:\n`{await _currency.GetCurrency(user.Id, Context.Guild.Id):N0}` {Roki.Properties.CurrencyIcon}")
                    .WithFooter(".$$ for Investing Account"))
                .ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Investing([Leftover] IUser user = null)
        {
            user ??= Context.User;
            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                    .WithDescription($"{user.Mention}'s Investing Account:\n`{GetInvAccount(user.Id):N2}` {Roki.Properties.CurrencyIcon}")
                    .WithFooter(".$ for Cash Account"))
                .ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Leaderboard([Leftover] int page = 1)
        {
            if (page <= 0)
                return;
            if (page > 0)
                page -= 1;
            using var uow = _db.GetDbContext();
            var list = uow.Users.GetCurrencyLeaderboard(Context.Client.CurrentUser.Id, page);
            var embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle("Currency Leaderboard");
            var i = 9 * page + 1;
            foreach (var user in list)
            {
                embed.AddField($"#{i++} {user.Username}#{user.Discriminator}", $"`{user.Currency:N0}` {Roki.Properties.CurrencyIcon}");
            }

            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Transfer(Account account, long amount)
        {
            if (amount == 0)
            {
                await Context.Channel.SendErrorAsync($"You must transfer at least `1` {Roki.Properties.CurrencyIcon}").ConfigureAwait(false);
                return;
            }

            using var uow = _db.GetDbContext();
            var success = false;
            var fromAcc = "";
            var toAcc = "";
            if ((int) account == 0)
            {
                success = await uow.Users.TransferToFromInvestingAccountAsync(Context.User.Id, -amount).ConfigureAwait(false);
                if (success)
                    await _currency.CacheChangeAsync(Context.User.Id, Context.Guild.Id, amount).ConfigureAwait(false);
                fromAcc = "Investing Account";
                toAcc = "Cash Account";
            }
            else if ((int) account == 1)
            {
                success = await uow.Users.TransferToFromInvestingAccountAsync(Context.User.Id, amount).ConfigureAwait(false);
                if (success)
                    await _currency.CacheChangeAsync(Context.User.Id, Context.Guild.Id, -amount).ConfigureAwait(false);
                fromAcc = "Cash Account";
                toAcc = "Investing Account";
            }
            
            if (!success)
            {
                await Context.Channel.SendErrorAsync($"You do not have enough {Roki.Properties.CurrencyIcon} in your `{fromAcc}` to transfer.").ConfigureAwait(false);
                return;
            }
            
            uow.Transaction.Add(new CurrencyTransaction
            {
                Amount = amount,
                ChannelId = Context.Channel.Id,
                From = Context.User.Id,
                To = Context.User.Id,
                GuildId = Context.Guild.Id,
                MessageId = Context.Message.Id,
                Reason = $"Transfer from {fromAcc} to {toAcc}",
                TransactionDate = DateTimeOffset.UtcNow
            });

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"{Context.User.Mention} You've successfully transferred `{amount:N0}` {Roki.Properties.CurrencyIcon}\nFrom `{fromAcc}` ➡️ `{toAcc}`")).ConfigureAwait(false);
            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Give(long amount, IGuildUser user, [Leftover] string message = null)
        {
            if (amount <= 0 || Context.User.Id == user.Id || user.IsBot)
                return;

            if (string.IsNullOrWhiteSpace(message))
                message = "No Message";
            message = $"Gift from {Context.User.Username} to {user.Username} - {message}";
            var success = await _currency.TransferAsync(Context.User.Id, user.Id, message, amount, Context.Guild.Id, Context.Channel.Id, Context.Message.Id)
                .ConfigureAwait(false);

            if (!success)
            {
                await Context.Channel.SendErrorAsync($"You do not have enough {Roki.Properties.CurrencyNamePlural} to give.").ConfigureAwait(false);
                return;
            }

            await Context.Channel.EmbedAsync(new EmbedBuilder().WithDynamicColor(Context)
                .WithDescription($"{Context.User.Username} gifted `{amount:N0}` {Roki.Properties.CurrencyNamePlural} to {user.Mention}")).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        [Priority(1)]
        public async Task CurrencyTransactions(int page = 1) =>
            await InternalCurrencyTransaction(Context.User.Id, page);
        
        [RokiCommand, Description, Usage, Aliases]
        [OwnerOnly]
        [Priority(0)]
        public async Task CurrencyTransactions(IUser user, int page = 1) =>
            await InternalCurrencyTransaction(user.Id, page);

        private async Task InternalCurrencyTransaction(ulong userId, int page)
        {
            if (--page < 0)
                return;

            List<CurrencyTransaction> trans;
            using (var uow = _db.GetDbContext())
            {
                trans = uow.Transaction.GetTransactions(userId, page);
            }
            
            var embed = new EmbedBuilder().WithDynamicColor(Context)
                .WithTitle($"{((SocketGuild) Context.Guild)?.GetUser(userId)?.Username ?? userId.ToString()}'s Transactions History");

            var desc = "";
            foreach (var tran in trans)
            {
                var type = tran.Amount > 0 ? "🔵" : "🔴";
                var amount = tran.Amount.ToString("N0");
                if (tran.Reason.StartsWith("Gift from") && tran.From == userId)
                {
                    type = "🔴";
                    amount = amount.Insert(0, "-");
                }
                var date = Format.Code($"{tran.TransactionDate.ToLocalTime():HH:mm yyyy-MM-dd}");
                desc += $"{type} {tran.Reason?.Trim()} {date}\n\t\t{Format.Code(amount)} {Roki.Properties.CurrencyIcon}\n";
            }

            embed.WithDescription(desc)
                .WithFooter($"Page {page + 1}");
            
            await Context.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }
        
        public enum Account
        {
            Cash = 0,
            Stone = 0,
            Debit = 0,
            Investing = 1,
            Invest = 1,
            Inv = 1,
            Trading = 1
        }
    }
}