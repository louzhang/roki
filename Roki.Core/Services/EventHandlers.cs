using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using NLog;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Modules.Xp.Common;
using StackExchange.Redis;

namespace Roki.Core.Services
{
    public class EventHandlers : IRokiService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly IDatabase _cache;

        private readonly Logger _log;

        public EventHandlers(DbService db, DiscordSocketClient client, IRedisCache cache)
        {
            _db = db;
            _cache = cache.Redis.GetDatabase();
            _client = client;

            _log = LogManager.GetCurrentClassLogger();
        }

        public async Task StartHandling()
        {
            _client.MessageReceived += MessageReceived;
            _client.MessageUpdated += MessageUpdated;
            _client.MessageDeleted += MessageDeleted;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            _client.UserJoined += UserJoined;
            _client.UserUpdated += UserUpdated;
            _client.JoinedGuild += JoinedGuild;
            _client.LeftGuild += LeftGuild;
            _client.GuildUpdated += GuildUpdated;
            _client.GuildAvailable += GuildAvailable;
            _client.GuildUnavailable += GuildUnavailable;
            _client.ChannelCreated += ChannelCreated;
            _client.ChannelUpdated += ChannelUpdated;
            _client.ChannelDestroyed += ChannelDestroyed;
            
            await Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot) return Task.CompletedTask;
            if (!(message.Channel is SocketTextChannel textChannel)) return Task.CompletedTask;

            UpdateXp(message).ConfigureAwait(false);
            var _ =  Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();

                if (!await uow.Channels.IsLoggingEnabled(textChannel)) return;

                string content;
                if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count == 0)
                    content = message.Content;
                else if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count > 0)
                    content = message.Content + "\n" + string.Join("\n", message.Attachments.Select(a => a.Url));
                else if (message.Attachments.Count > 0)
                    content = string.Join("\n", message.Attachments.Select(a => a.Url));
                else
                    content = "";
                
                uow.Context.Messages.Add(new Message
                {
                    AuthorId = message.Author.Id,
                    Author = message.Author.Username,
                    ChannelId = message.Channel.Id,
                    Channel = message.Channel.Name,
                    GuildId = message.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                    Guild = message.Channel is ITextChannel ch ? ch.Guild.Name : null,
                    MessageId = message.Id,
                    Content = content,
                    EditedTimestamp = message.EditedTimestamp?.ToUniversalTime(),
                    Timestamp = message.Timestamp.ToUniversalTime()
                });

                await uow.SaveChangesAsync().ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> cache, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after.Author.IsBot) return Task.CompletedTask;
            if (string.IsNullOrWhiteSpace(after.Author.Username)) return Task.CompletedTask;
            if (after.EditedTimestamp == null) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                if (after.Channel is SocketTextChannel textChannel)
                    if (!await uow.Channels.IsLoggingEnabled(textChannel)) return;
                string content;
                if (!string.IsNullOrWhiteSpace(after.Content) && after.Attachments.Count == 0)
                    content = after.Content;
                else if (!string.IsNullOrWhiteSpace(after.Content) && after.Attachments.Count > 0)
                    content = after.Content + "\n" + string.Join("\n", after.Attachments.Select(a => a.Url));
                else if (after.Attachments.Count > 0)
                    content = string.Join("\n", after.Attachments.Select(a => a.Url));
                else
                    content = "";
                uow.Context.Messages.Add(new Message
                {
                    AuthorId = after.Author.Id,
                    Author = after.Author.Username,
                    ChannelId = after.Channel.Id,
                    Channel = after.Channel.Name,
                    GuildId = after.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                    Guild = after.Channel is ITextChannel ch ? ch.Guild.Name : null,
                    MessageId = after.Id,
                    Content = content,
                    EditedTimestamp = after.EditedTimestamp?.ToUniversalTime(),
                    Timestamp = after.Timestamp.ToUniversalTime()
                });

                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
                
            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            if (cache.HasValue && cache.Value.Author.IsBot) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                if (channel is SocketTextChannel textChannel)
                    if (!await uow.Channels.IsLoggingEnabled(textChannel).ConfigureAwait(false)) return;
                await uow.Messages.MessageDeleted(cache.Id).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> caches, ISocketMessageChannel channel)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                if (channel is SocketTextChannel textChannel)
                    if (!await uow.Channels.IsLoggingEnabled(textChannel).ConfigureAwait(false)) return;
                foreach (var cache in caches)
                {
                    if (cache.HasValue && cache.Value.Author.IsBot) continue;
                    await uow.Messages.MessageDeleted(cache.Id).ConfigureAwait(false);
                }
            });
            
            return Task.CompletedTask;
        }
        
        private Task GuildAvailable(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var g = await uow.Guilds.GetOrCreateGuildAsync(guild).ConfigureAwait(false);
                g.Available = true;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task GuildUnavailable(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var g = await uow.Guilds.GetOrCreateGuildAsync(guild).ConfigureAwait(false);
                g.Available = false;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task GuildUpdated(SocketGuild before, SocketGuild after)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var guild = await uow.Guilds.GetOrCreateGuildAsync(before).ConfigureAwait(false);
                guild.Name = after.Name;
                guild.ChannelCount = after.Channels.Count;
                guild.EmoteCount = after.Emotes.Count;
                guild.IconId = after.IconId;
                guild.MemberCount = after.MemberCount;
                guild.RegionId = after.VoiceRegionId;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var ch = await uow.Channels.GetOrCreateChannelAsync(textChannel).ConfigureAwait(false);
                ch.Deleted = true;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private Task ChannelUpdated(SocketChannel before, SocketChannel after)
        {
            if (!(before is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var channel = await uow.Channels.GetOrCreateChannelAsync(textChannel).ConfigureAwait(false);
                channel.Name = textChannel.Name;
                channel.GuildName = textChannel.Guild.Name;
                channel.UserCount = textChannel.Users.Count;
                channel.IsNsfw = textChannel.IsNsfw;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel channel)
        {
            if (!(channel is SocketTextChannel textChannel)) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                await uow.Channels.GetOrCreateChannelAsync(textChannel).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            var _ = Task.Run(async () =>
            {
                _log.Info("Joined server: {0} [{1}]", guild?.Name, guild?.Id);
                using var uow = _db.GetDbContext();
                await uow.Guilds.GetOrCreateGuildAsync(guild).ConfigureAwait(false);
                await guild.DownloadUsersAsync().ConfigureAwait(false);
                var users = guild.Users;
                foreach (var user in users)
                {
                    await uow.Users.GetOrCreateUserAsync(user);
                }
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task LeftGuild(SocketGuild guild)
        {
            var _ = Task.Run(() => { _log.Info("Left server: {0} [{1}]", guild?.Name, guild?.Id); });
            return Task.CompletedTask;
        }

        private Task UserUpdated(SocketUser before, SocketUser after)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var user = await uow.Context.Users.FirstAsync(u => u.UserId == before.Id).ConfigureAwait(false);
                user.Username = after.Username;
                user.Discriminator = after.Discriminator;
                user.AvatarId = after.AvatarId;
                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task UserJoined(SocketGuildUser user)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                await uow.Users.GetOrCreateUserAsync(user).ConfigureAwait(false);
            });
            return Task.CompletedTask;
        }

        private Task UpdateXp(SocketMessage message)
        {
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var user = await uow.Users.GetOrCreateUserAsync(message.Author).ConfigureAwait(false);
                var doubleXp = uow.Subscriptions.DoubleXpIsActive(message.Author.Id);
                var fastXp = uow.Subscriptions.FastXpIsActive(message.Author.Id);
                var oldLevel = new XpLevel(await GetCachedXp(message.Author.Id));
                var newXp = 0;
                if (fastXp)
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(Roki.Properties.XpFastCooldown))
                    {
                        newXp = await UpdateCacheXp(message.Author.Id, Roki.Properties.XpPerMessage, doubleXp).ConfigureAwait(false);
                        await uow.Users.UpdateXp(user, doubleXp).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(Roki.Properties.XpCooldown))
                    {
                        newXp = await UpdateCacheXp(message.Author.Id, Roki.Properties.XpPerMessage, doubleXp).ConfigureAwait(false);
                        await uow.Users.UpdateXp(user, doubleXp).ConfigureAwait(false);
                    }
                }
                
                if (newXp == 0)
                    return;

                var newLevel = new XpLevel(newXp);
                
                if (newLevel.Level > oldLevel.Level)
                {
                    var textChannel = (SocketTextChannel) message.Channel;
                    var rewards = await uow.Guilds.GetXpRewardsAsync(textChannel.Guild.Id, newLevel.Level).ConfigureAwait(false);
                    if (rewards != null && rewards.Count != 0)
                    {
                        foreach (var reward in rewards)
                        {
                            if (reward.Type == "currency")
                            {
                                var amount = int.Parse(reward.Reward);
                                await uow.Users.UpdateCurrencyAsync(user.UserId, amount).ConfigureAwait(false);
                                uow.Transaction.Add(new CurrencyTransaction
                                {
                                    Amount = amount,
                                    Reason = "XP Level Up Reward",
                                    To = user.UserId,
                                    From = 0,
                                    GuildId = textChannel.Guild.Id,
                                    ChannelId = textChannel.Id,
                                    MessageId = message.Id
                                });
                            }
                            else
                            {
                                var role = textChannel.Guild.GetRole(ulong.Parse(reward.Reward));
                                if (role == null) continue;
                                var guildUser = (IGuildUser) message.Author;
                                await guildUser.AddRoleAsync(role).ConfigureAwait(false);
                            }
                        }

                        var dm = await message.Author.GetOrCreateDMChannelAsync().ConfigureAwait(false);
                        try
                        {
                            await dm.EmbedAsync(new EmbedBuilder().WithOkColor()
                                    .WithTitle($"Level `{newLevel.Level}` Rewards")
                                    .WithDescription("Here are your rewards:\n" + string.Join("\n", rewards
                                                         .Select(r => r.Type == "currency"
                                                             ? $"+ `{int.Parse(r.Reward):N0}` {Roki.Properties.CurrencyIcon}"
                                                             : $"+ <@&{r.Reward}>"))))
                                .ConfigureAwait(false);
                            await dm.CloseAsync().ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // unable to send dm to user
                            // ignored
                        }
                    }
                }
            });
            
            return Task.CompletedTask;
        }

        private async Task<int> GetCachedXp(ulong userId)
        {
            var xp = await _cache.StringGetAsync($"xp:{userId}").ConfigureAwait(false);
            if (xp.HasValue)
                return (int) xp;
            
            using var uow = _db.GetDbContext();
            var user = await uow.Users.GetUserAsync(userId).ConfigureAwait(false);
            return user.TotalXp;
        }

        private async Task<int> UpdateCacheXp(ulong userId, int add, bool boost = false)
        {
            var xp = await _cache.StringGetAsync($"xp:{userId}").ConfigureAwait(false);
            if (xp.HasValue)
            {
                if (boost)
                {
                    return (int) await _cache.StringIncrementAsync($"xp:{userId}", add * 2).ConfigureAwait(false);
                }

                return (int) await _cache.StringIncrementAsync($"xp:{userId}", add).ConfigureAwait(false);
            }

            using var uow = _db.GetDbContext();
            var user = await uow.Users.GetUserAsync(userId).ConfigureAwait(false);
            var currentXp = user.TotalXp;
            if (boost)
            {
                await _cache.StringSetAsync($"xp:{userId}", currentXp + add * 2).ConfigureAwait(false);
                return currentXp + add * 2;
            }

            await _cache.StringSetAsync($"xp:{userId}", currentXp + add).ConfigureAwait(false);
            return currentXp + add;
        }
    }
}