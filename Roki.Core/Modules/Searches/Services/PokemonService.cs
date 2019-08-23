using System.Linq;
using Discord;
using PokeApiNet.Models;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Searches.Services
{
    public class PokemonService : INService
    {
        public Color GetColorOfPokemon(string color)
        {
            switch (color)
            {
                case "black":
                    return new Color(0x000000);
                case "blue":
                    return new Color(0x257CFF);
                case "brown":
                    return new Color(0xA3501A);
                case "gray":
                    return new Color(0x808080);
                case "green":
                    return new Color(0x008000);
                case "pink":
                    return new Color(0xFF65A5);
                case "purple":
                    return new Color(0xA63DE8);
                case "red":
                    return new Color(0xFF3232);
                case "white":
                    return new Color(0xFFFFFF);
                case "yellow":
                    return new Color(0xFFF359);
                default:
                    return new Color(0x000000);
            }
        }

        public string GetPokemonSprite(string pokemon)
        {
            var url = "https://play.pokemonshowdown.com/sprites/xyani/";
            switch (pokemon)
            {
                case "deoxys-normal":
                    return url + "deoxys.gif";
                case "deoxys-attack":
                    return url + pokemon + ".gif";
                case "deoxys-speed":
                    return url + pokemon + ".gif";
                case "deoxys-defense":
                    return url + pokemon + ".gif";
                case "zygarde-10":
                    return url + pokemon + ".gif";
                case "zygarde-complete":
                    return url + pokemon + ".gif";
                case "necrozma-ultra":
                    return url + pokemon + ".gif";
            }

            return url + pokemon.Replace("-", "") + ".gif";
        }

        public string GetPokemonEvolutionChain(string pokemon, EvolutionChain evoChain)
        {
            // hardcode wurmple?
            if (evoChain.Chain.EvolvesTo.Count < 1)
                return "No Evolutions";
            var evoStr = "";
            evoStr += evoChain.Chain.Species.Name.ToTitleCase();
            foreach (var evo in evoChain.Chain.EvolvesTo)
            {
                evoStr += " > " + evo.Species.Name.ToTitleCase();
                if (evo.EvolvesTo.Count <= 0) continue;
                evoStr += evo.EvolvesTo.Aggregate("", (current, evo1) => current + (" > " + evo1.Species.Name.ToTitleCase() + "\n"));
            }
            
            return evoStr;
        }
    }
}