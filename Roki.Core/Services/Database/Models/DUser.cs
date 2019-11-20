using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roki.Core.Services.Database.Models
{
    [Table("users")]
    public class DUser : DbEntity
    {
        public DUser()
        {
            Lottery = new HashSet<Lottery>();
            Quotes = new HashSet<Quote>();
            Store = new HashSet<Listing>();
            Subscriptions = new HashSet<Subscriptions>();
            Trades = new HashSet<Trades>();
        }
        
        public ulong UserId { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string AvatarId { get; set; }
        public int TotalXp { get; set; }
        public DateTimeOffset LastLevelUp { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset LastXpGain { get; set; } = DateTimeOffset.UtcNow;
        public string NotificationLocation { get; set; } = "dm";
        public long Currency { get; set; } = 0;
        public string Inventory { get; set; } = null;
        [Column("investing")]
        public decimal InvestingAccount { get; set; } = 50000;
        public string Portfolio { get; set; } = null;
        
        public ICollection<Lottery> Lottery { get; set; }
        public ICollection<Quote> Quotes { get; set; }
        public ICollection<Listing> Store { get; set; }
        public ICollection<Subscriptions> Subscriptions { get; set; }
        public ICollection<Trades> Trades { get; set; }

        
        public override bool Equals(object obj) => 
            obj is DUser dUser && dUser.UserId == UserId;
        
        public override int GetHashCode() => 
            UserId.GetHashCode();
        
        public override string ToString() => 
            Username + "#" + Discriminator;
    }

    public class Item
    {
        public string Name { get; set; }
        public int Quantity { get; set; }
    }

    public class Investment
    {
        public string Symbol { get; set; }
        public string Position { get; set; }
        public long Shares { get; set; }
        public DateTime? InterestDate { get; set; } = null;
    }
}