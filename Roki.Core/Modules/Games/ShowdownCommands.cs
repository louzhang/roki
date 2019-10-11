using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Games.Services;
using Roki.Services;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class ShowdownCommands : RokiSubmodule<ShowdownService>
        {
            private readonly DiscordSocketClient _client;
            private readonly ICurrencyService _currency;

            public ShowdownCommands(DiscordSocketClient client, ICurrencyService currency)
            {
                _client = client;
                _currency = currency;
            }
            
            private enum BetPlayer
            {
                PLAYER1 = 1,
                P1 = 1,
                PLAYER2 = 2,
                P2 = 2,
            }

            private class PlayerBet
            {
                public long Amount { get; set; }
                public BetPlayer BetPlayer { get; set; }
            }
            
            [RokiCommand, Description, Aliases, Usage]
            [RequireContext(ContextType.Guild)]
            public async Task BetPokemonGame()
            {
                if (_service.Games.TryGetValue(ctx.Channel.Id, out _))
                {
                    await ctx.Channel.SendErrorAsync("Game already in progress in current channel.");
                    return;
                }
                
                _service.Games.TryAdd(ctx.Channel.Id, $"{ctx.User.Username}'s game");
                await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                await ctx.Channel.SendMessageAsync("Starting new Pokemon game.").ConfigureAwait(false);

                var gameText = await _service.StartAiGameAsync().ConfigureAwait(false);
                var index = gameText.IndexOf("|teampreview", StringComparison.Ordinal);
                var gameIntro = gameText.Substring(0, index);
                var gameTurns = gameText.Substring(index + 1);

                var intro = _service.ParseIntro(gameIntro);

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle($"Random Battle")
                        .WithDescription("A Pokemon battle is about to start!\nType `join bet_amount player` to join the bet.\ni.e. `join 15 p2`")
                        .AddField("Player 1", string.Join('\n', intro[0]), true)
                        .AddField("Player 2", string.Join('\n', intro[1]), true))
                    .ConfigureAwait(false);

                var joinedPlayers = new Dictionary<IUser, PlayerBet>();
                var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(30);
                _client.MessageReceived += message =>
                {
                    if (ctx.Channel.Id != message.Channel.Id)
                        return Task.CompletedTask;

                    var _ = Task.Run(async () =>
                    {
                        var args = message.Content.Split();
                        if (!string.Equals(args[0], "join", StringComparison.OrdinalIgnoreCase) || !long.TryParse(args[1], out var amount) ||
                            !Enum.TryParse<BetPlayer>(args[2].ToUpperInvariant(), out var betPlayer) || DateTime.UtcNow >= timeout) return Task.CompletedTask;
                        if (amount <= 0)
                        {
                            await ctx.Channel.SendErrorAsync("The minimum bet for this game is 1 stone").ConfigureAwait(false);
                            return Task.CompletedTask;
                        }

                        var removed = await _currency
                            .ChangeAsync(message.Author, "BetShowdown Entry", -amount, ctx.User.Id.ToString(), "Server", ctx.Guild.Id, ctx.Channel.Id,
                                ctx.Message.Id)
                            .ConfigureAwait(false);
                        if (!removed)
                        {
                            await ctx.Channel.SendErrorAsync("Not enough stones.").ConfigureAwait(false);
                            return Task.CompletedTask;
                        }

                        var added = joinedPlayers.TryAdd(message.Author, new PlayerBet
                        {
                            Amount = amount,
                            BetPlayer = betPlayer
                        });
                        if (added)
                        {
                            var joinedMsg = await ctx.Channel
                                .EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"{message.Author.Mention} has joined the bet."))
                                .ConfigureAwait(false);
                            joinedMsg.DeleteAfter(5);
                            return Task.CompletedTask;
                        }
                        var alreadyJoinedMsg = await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor().WithDescription("You are already in the bet."))
                            .ConfigureAwait(false);
                        alreadyJoinedMsg.DeleteAfter(5);
                        return Task.CompletedTask;
                    });
                    return Task.CompletedTask;
                };

                Thread.Sleep(TimeSpan.FromSeconds(30));
                if (joinedPlayers.Count == 0)
                {
                    await ctx.Channel.SendErrorAsync("Not enough players to start the bet.\nBet is cancelled");
                    return;
                }
                
//                var turns = _service.ParseTurns(gameTurns);
//                var win = turns.Last().Last();
                var win = _service.GetWinner(gameTurns);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor().WithDescription($"Player {win} has won the battle!")).ConfigureAwait(false);
                var result = win == "1" ? BetPlayer.P1 : BetPlayer.P2;

                var winStr = "";

                foreach (var (key, value) in joinedPlayers)
                {
                    if (result != value.BetPlayer) continue;
                    var won = value.Amount * 2;
                    winStr += $"{key.Username} won {value} stones\n";
                    await _currency.ChangeAsync(key, "BetShowdown Payout", won, "Server", ctx.User.Id.ToString(), ctx.Guild.Id,
                        ctx.Channel.Id, ctx.Message.Id);
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                    .WithDescription($"Congratulations! {winStr}\n")).ConfigureAwait(false);
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithErrorColor()
                    .WithDescription($"Everyone else better luck next time!")).ConfigureAwait(false);
                
                _service.Games.TryRemove(ctx.Channel.Id, out _);
            }
        }
    }
}