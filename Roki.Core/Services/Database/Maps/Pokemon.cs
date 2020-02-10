using System.Collections.Generic;

namespace Roki.Services.Database.Maps
{
    public class Pokemon
    {
        public string Id { get; set; }
        public int Num { get; set; }
        public string Species { get; set; }
        public List<string> Types { get; set; }
        public Dictionary<string, float> GenderRatio { get; set; }
        public Dictionary<string, int> BaseStats { get; set; }
        public Dictionary<string, string> Abilities { get; set; }
        public float Height { get; set; }
        public float Weight { get; set; }
        public string Color { get; set; }
        public List<string> Evos { get; set; }
        public List<string> EggGroups { get; set; }
    }

    public class PokemonItem
    {
        public string Id { get; set; }
        public string Game { get; set; }
        public int Gen { get; set; }
        public string Desc { get; set; }
        public Dictionary<string, object> Fling { get; set; }
    }

    public class Ability
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string ShortDesc { get; set; }
        public float Rating { get; set; }
    }

    public class Move
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string ShortDesc { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public int? Cccuracy { get; set; }
        public int Power { get; set; }
        public int Pp { get; set; }
        public int Priority { get; set; }
    }
}