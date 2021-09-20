using Newtonsoft.Json;

namespace SteamSteamich.Domain
{
    class Player
    {
        [JsonProperty("steamid")]
        public string SteamId { get; set; }     
    }
}
