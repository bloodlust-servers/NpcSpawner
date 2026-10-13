using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NpcSpawner
{
    /// <summary>Одна группа НПС (night, blood, beasts, ...).</summary>
    internal sealed class SpawnGroup
    {
        /// <summary>Радиус в метрах вокруг выбранного игрока, где случайно появляются НПС.</summary>
        [JsonPropertyName("RandomAround")]
        public float RandomAround { get; set; } = 8f;

        /// <summary>id НПС -> количество.</summary>
        [JsonPropertyName("Prefabs")]
        public Dictionary<string, int> Prefabs { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>Одна запись лута: предмет, количество, шанс 0..1.</summary>
    internal sealed class LootEntry
    {
        [JsonPropertyName("ItemId")]
        public int ItemId { get; set; }

        [JsonPropertyName("Amount")]
        public int Amount { get; set; } = 1;

        [JsonPropertyName("Chance")]
        public float Chance { get; set; } = 1f;
    }
}
