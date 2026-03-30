using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YSHSteamNet
{
    public class CustomPeer
    {
        [JsonPropertyName("id")]
        public ulong Id      { get; init; }

        [JsonPropertyName("name")]
        public string Name   { get; init; } = "";

        [JsonPropertyName("address")]
        public string Address { get; init; } = "127.0.0.1";

        [JsonPropertyName("port")]
        public int Port      { get; init; }
    }

    public class CustomConfig
    {
        [JsonPropertyName("localId")]
        public ulong LocalId      { get; init; }

        [JsonPropertyName("name")]
        public string Name        { get; init; } = "";

        [JsonPropertyName("listenPort")]
        public int ListenPort     { get; init; }

        [JsonPropertyName("peers")]
        public List<CustomPeer> Peers { get; init; } = new();

        public static CustomConfig Load(string path)
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CustomConfig>(json)
                ?? throw new Exception($"[CustomConfig] Failed to parse {path}");
        }
    }
}
