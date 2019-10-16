using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Games.Common;
using Roki.Modules.Games.Services;
using Roki.Services;

namespace Roki.Modules.Games
{
    public partial class Games
    {
        [Group]
        public class TriviaCommands : RokiSubmodule<TriviaService>
        {
            private readonly ICurrencyService _currency;
            private readonly DiscordSocketClient _client;
            private static readonly IEmote LetterA = new Emoji("🇦");
            private static readonly IEmote LetterB = new Emoji("🇧");
            private static readonly IEmote LetterC = new Emoji("🇨");
            private static readonly IEmote LetterD = new Emoji("🇩");
            private static readonly IEmote True = new Emoji("✔️");
            private static readonly IEmote False = new Emoji("❌");

            private static readonly IEmote[] MultipleChoice =
            {
                LetterA,
                LetterB,
                LetterC,
                LetterD
            };

            private static readonly IEmote[] TrueFalse =
            {
                True,
                False,
            };
            
            private static readonly Dictionary<string, int> Categories = new Dictionary<string, int>
            {
                {"All", 0},
                {"General Knowledge", 9},
                {"Books", 10},
                {"Film", 11},
                {"Music", 12},
//                {"Musicals & Theatres", 13},
                {"Musicals", 13},
                {"Theatre", 13},
                {"Television", 14},
                {"Video Games", 15},
                {"Board Games", 16},
//                {"Science & Nature", 17},
                {"Science", 17},
                {"Nature", 17},
                {"Computers", 18},
                {"Mathematics", 19},
                {"Mythology", 20},
                {"Sports", 21},
                {"Geography", 22},
                {"History", 23},
                {"Politics", 24},
                {"Art", 25},
                {"Celebrities", 26},
                {"Animals", 27},
                {"Vehicles", 28},
                {"Comics", 29},
                {"Gadgets", 30},
//                {"Japanese Anime & Manga", 31},
                {"Anime", 31},
                {"Manga", 31},
//                {"Cartoons & Animations", 32},
                {"Cartoons", 32},
                {"Animations", 32}
            };

            public class PlayerScore
            {
                public int Correct { get; set; } = 0;
                public int Incorrect { get; set; } = 0;
                public long Amount { get; set; } = 0;
            }

            public TriviaCommands(ICurrencyService currency, DiscordSocketClient client)
            {
                _currency = currency;
                _client = client;
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task Trivia(string category = "All")
            {
                if (_service.TriviaGames.ContainsKey(ctx.Channel.Id))
                {
                    await ctx.Channel.SendErrorAsync("Game already in progress in current channel.");
                    return;
                }
                if (!Categories.ContainsKey(category.ToTitleCase()))
                {
                    await ctx.Channel.SendErrorAsync("Unknown Category, use `.tc` to checkout the trivia categories.").ConfigureAwait(false);
                    return;
                }

                _service.TriviaGames.TryAdd(ctx.Channel.Id, ctx.User.Id);
                TriviaModel questions;
                var prizePool = 0;
                try
                {
                    await ctx.Channel.TriggerTypingAsync().ConfigureAwait(false);
                    questions = await _service.GetTriviaQuestionsAsync().ConfigureAwait(false);
                    foreach (var question in questions.Results)
                    {
                        if (question.Difficulty == "easy")
                            prizePool += 1;
                        else if (question.Difficulty == "medium")
                            prizePool += 3;
                        else
                            prizePool += 5;
                    }
                }
                catch (Exception e)
                {
                    _log.Warn(e);
                    await ctx.Channel.SendErrorAsync("Unable to start game, please try again.");
                    _service.TriviaGames.TryRemove(ctx.Channel.Id, out _);
                    return;
                }

                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle($"Trivia Game - {category}")
                        .WithDescription($"Starting new trivia game.\nYou can earn up to: **{prizePool}**!\nReact with the correct emote to answer questions."))
                    .ConfigureAwait(false);

                var count = 1;
                var playerScore = new Dictionary<ulong, PlayerScore>();
                foreach (var q in questions.Results)
                {
                    var playerChoice = new Dictionary<ulong, string>();
                    var shuffledAnswers = _service.RandomizeAnswersOrder(q.Correct, q.Incorrect);
                    var question = HttpUtility.HtmlDecode(q.Question);
                    var answer = shuffledAnswers.First(s => s.Contains(HttpUtility.HtmlDecode(q.Correct) ?? ""));
                    int difficultyBonus;
                    if (q.Difficulty == "easy")
                        difficultyBonus = 1;
                    else if (q.Difficulty == "medium")
                        difficultyBonus = 3;
                    else
                        difficultyBonus = 5;
                    IUserMessage msg;

                    var embed = new EmbedBuilder().WithOkColor()
                        .WithTitle($"Question {count++}: {q.Category.ToTitleCase()} - {q.Difficulty.ToTitleCase()}");
                    if (q.Type == "multiple")
                    {
                        embed.WithDescription($"Multiple Choice\n{question}\n{string.Join('\n', shuffledAnswers)}");
                        msg = await ctx.Channel.EmbedAsync(embed);
                        await msg.AddReactionsAsync(MultipleChoice).ConfigureAwait(false);
                    }
                    else
                    {
                        embed.WithDescription($"True or False\n{question}");
                        msg = await ctx.Channel.EmbedAsync(embed);
                        await msg.AddReactionsAsync(TrueFalse).ConfigureAwait(false);
                    }

                    using (msg.OnReaction(_client, AnswerAdded, AnswerRemoved))
                    {
                        await Task.Delay(20000).ConfigureAwait(false);
                    }

                    await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                            .WithTitle("Answer")
                            .WithDescription($"The correct answer is:\n{answer}"))
                        .ConfigureAwait(false);

                    foreach (var player in playerChoice)
                    {
                        if (player.Value != answer)
                        {
                            if (playerScore.ContainsKey(player.Key))
                                playerScore[player.Key].Incorrect += 1;
                            else
                                playerScore.Add(player.Key, new PlayerScore
                                {
                                    Incorrect = 1
                                });
                            continue;
                        }
                        if (playerScore.ContainsKey(player.Key))
                            playerScore[player.Key].Amount += difficultyBonus;
                        else
                            playerScore.Add(player.Key, new PlayerScore
                            {
                                Amount = difficultyBonus,
                                Correct = 1
                            });
                    }
                    
                    await Task.Delay(1000).ConfigureAwait(false);

                    async Task AnswerAdded(SocketReaction r)
                    {
                        if (r.Channel != ctx.Channel || r.User.Value.IsBot || r.Message.Value != msg)
                            await Task.CompletedTask;
                        if (MultipleChoice.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.UserId))
                            {
                                var rm = await ctx.Channel.SendErrorAsync($"{r.User.Value.Mention} You must remove your current choice first.").ConfigureAwait(false);
                                await msg.RemoveReactionAsync(r.Emote, r.User.Value).ConfigureAwait(false);
                                rm.DeleteAfter(3);
                            }
                            else
                                playerChoice.Add(r.UserId, shuffledAnswers[MultipleChoice.IndexOf(r.Emote)]);
                            await Task.CompletedTask;
                        }
                        if (TrueFalse.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.UserId))
                            {
                                var rm = await ctx.Channel.SendErrorAsync($"{r.User.Value.Mention} You must remove your current choice first.").ConfigureAwait(false);
                                await msg.RemoveReactionAsync(r.Emote, r.User.Value).ConfigureAwait(false);
                                rm.DeleteAfter(3);
                            }
                            else
                                playerChoice.Add(r.UserId, r.Emote.Name);
                            await Task.CompletedTask;
                        }
                    }

                    async Task AnswerRemoved(SocketReaction r)
                    {
                        if (r.Channel != ctx.Channel || r.User.Value.IsBot || r.Message.Value != msg)
                            await Task.CompletedTask;
                        if (MultipleChoice.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.UserId))
                                playerChoice.Remove(r.UserId, out _);
                            await Task.CompletedTask;
                        }
                        if (TrueFalse.Contains(r.Emote))
                        {
                            if (playerChoice.ContainsKey(r.UserId))
                                playerChoice.Remove(r.UserId, out _);
                            await Task.CompletedTask;
                        }
                    }
                }

                
                
                _service.TriviaGames.TryRemove(ctx.Channel.Id, out _);
            }

            [RokiCommand, Description, Usage, Aliases]
            public async Task TriviaCategories()
            {
                await ctx.Channel.EmbedAsync(new EmbedBuilder().WithOkColor()
                        .WithTitle("Trivia Categories")
                        .WithDescription(string.Join(", ", Categories.Keys)))
                    .ConfigureAwait(false);
            }
        }
    }
}