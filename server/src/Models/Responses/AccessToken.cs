using Newtonsoft.Json;

namespace IsThisAMood.Models.Responses
{
    public class AccessToken
    {
        [JsonProperty("access_token")] public string Token { get; set; }

        [JsonProperty("access_type")] public string Type { get; set; }
    }
}