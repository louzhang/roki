using System;
using System.Threading.Tasks;
using Discord;
using Roki.Extensions;
using Roki.Modules.Rsvp.Services;

namespace Roki.Modules.Rsvp
{
    public class Rsvp : RokiTopLevelModule<RsvpService>
    {
        private readonly Roki _roki;

        public Rsvp(Roki roki)
        {
            _roki = roki;
        }

        public async Task Events(string args)
        {
            var err = string.Format("`{0}events new/create`: Create a new event\n" +
                                    "`{0}events edit`: Edits an event\n" +
                                    "`{0}events list/ls`: Lists events in this server\n", _roki.Properties.Prefix);
            if (string.IsNullOrWhiteSpace(args))
            {
                await ctx.Channel.SendErrorAsync(err).ConfigureAwait(false);
                return;
            }

            if (args.Equals("new", StringComparison.OrdinalIgnoreCase) || args.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                
            }
            else if (args.Equals("edit", StringComparison.OrdinalIgnoreCase) || args.Equals("e", StringComparison.OrdinalIgnoreCase))
            {
                
            }
            else if (args.Equals("list", StringComparison.OrdinalIgnoreCase) || args.Equals("ls", StringComparison.OrdinalIgnoreCase))
            {
                
            }
            else
            {
                await ctx.Channel.SendErrorAsync(err).ConfigureAwait(false);
            }
        }
    }
}