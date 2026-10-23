using System;
using System.Collections.Generic;
using System.Globalization;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS
{
    public static class RuntimeText
    {
        [Serializable] private sealed class Entry { public string Key; public string Value; }
        [Serializable] private sealed class Table { public Entry[] Entries; }
        private static Dictionary<string, string> values;

        static void EnsureLoaded()
        {
            if (values == null)
            {
                var asset = Resources.Load<TextAsset>("ui-ru");
                if (asset == null) throw new InvalidOperationException("Required ui-ru localization is missing.");
                var table = JsonUtility.FromJson<Table>(asset.text);
                if (table?.Entries == null) throw new InvalidOperationException("Localization requires an Entries array.");
                var loaded = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var item in table.Entries)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Key) || item.Value == null || loaded.ContainsKey(item.Key))
                        throw new InvalidOperationException("Localization contains an invalid or duplicate entry.");
                    loaded.Add(item.Key, item.Value);
                }
                values = loaded;
            }
        }
        public static string Get(string key) => GetOrDefault(key, key);
        public static string GetOrDefault(string key, string fallback)
        {
            EnsureLoaded();
            return values.TryGetValue(key, out var value) ? value : fallback;
        }
        public static string Format(string key, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
        public static string Cost(int minerals, int gas) => gas > 0 ? Format("cost.both", minerals, gas) : Format("cost.minerals", minerals);
        public static string ResearchReason(string reason, UpgradeDefinition upgrade, Func<string,string> entityName)
        {
            string producer = string.IsNullOrEmpty(upgrade.ProducerId) ? "" : entityName(upgrade.ProducerId);
            string prerequisite = string.IsNullOrEmpty(upgrade.PrerequisiteId) ? "" : entityName(upgrade.PrerequisiteId);
            return string.Format(CultureInfo.CurrentCulture, GetOrDefault("reason."+reason,Get("reason.unknown")), producer, prerequisite);
        }
    }
}
