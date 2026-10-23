using System;
using System.Collections.Generic;
using System.Linq;
using ColonyRTS.Simulation;
using UnityEngine;

namespace ColonyRTS.Presentation
{
    [Serializable]
    public sealed class UiCommandLayout
    {
        [Serializable] public sealed class Shortcut { public string DefinitionId; public string Key; }
        public string TrainKey;
        public string[] BuildOrder = new string[0];
        public string[] TrainOrder = new string[0];
        public string[] HiddenBuildDefinitions = new string[0];
        public Shortcut[] BuildShortcuts = new Shortcut[0];
    }

    // Presentation ordering and keys live in resources. Producer relationships
    // always come from RuleSet; this class never owns costs or command eligibility.
    public sealed class UiCommandCatalog
    {
        readonly Dictionary<string, EntityDefinition[]> training = new Dictionary<string, EntityDefinition[]>(StringComparer.Ordinal);
        readonly Dictionary<string, KeyCode> buildKeys = new Dictionary<string, KeyCode>(StringComparer.Ordinal);
        public IReadOnlyList<EntityDefinition> Buildings { get; }
        public KeyCode TrainKey { get; }
        public IEnumerable<KeyValuePair<string, KeyCode>> BuildShortcuts => buildKeys;
        public readonly struct TrainingChoice
        {
            public readonly int ProducerId;
            public readonly string DefinitionId;
            public bool Available => DefinitionId != null;
            public TrainingChoice(int producerId, string definitionId) { ProducerId = producerId; DefinitionId = definitionId; }
        }

        public static UiCommandCatalog Load(RuleSet rules)
        {
            var asset = Resources.Load<TextAsset>("rts-ui");
            if (asset == null) throw new InvalidOperationException("Required rts-ui bindings are missing.");
            return new UiCommandCatalog(rules, JsonUtility.FromJson<UiCommandLayout>(asset.text));
        }

        public UiCommandCatalog(RuleSet rules, UiCommandLayout layout)
        {
            if (rules?.Definitions == null || layout == null) throw new ArgumentException("UI bindings require a catalog and layout.");
            var definitions = rules.Definitions.ToDictionary(d => d.Id, StringComparer.Ordinal);
            var buildOrder = ValidateOrder(layout.BuildOrder, definitions, true);
            var trainOrder = ValidateOrder(layout.TrainOrder, definitions, false);
            var hidden = ValidateOrder(layout.HiddenBuildDefinitions, definitions, true);
            Buildings = rules.Definitions.Where(d => d.IsBuilding && !d.IsResource && !hidden.Contains(d.Id))
                .OrderBy(d => Rank(buildOrder, d.Id)).ToArray();
            foreach (var group in rules.Definitions.Where(d => !d.IsBuilding && !d.IsResource && !string.IsNullOrEmpty(d.ProducerId)).GroupBy(d => d.ProducerId))
                training.Add(group.Key, group.OrderBy(d => Rank(trainOrder, d.Id)).ToArray());
            TrainKey = ParseKey(layout.TrainKey);
            var usedKeys = new HashSet<KeyCode> { TrainKey };
            foreach (var shortcut in layout.BuildShortcuts ?? new UiCommandLayout.Shortcut[0])
            {
                if (shortcut == null || !Buildings.Any(d => d.Id == shortcut.DefinitionId)) throw new ArgumentException("Build shortcut references a hidden or unknown building.");
                var key = ParseKey(shortcut.Key);
                if (!usedKeys.Add(key) || buildKeys.ContainsKey(shortcut.DefinitionId)) throw new ArgumentException("Duplicate UI shortcut.");
                buildKeys.Add(shortcut.DefinitionId, key);
            }
        }
        static string[] ValidateOrder(string[] order, Dictionary<string, EntityDefinition> definitions, bool building)
        {
            order = order ?? new string[0];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in order)
                if (id == null || !seen.Add(id) || !definitions.TryGetValue(id, out var definition) || definition.IsResource || definition.IsBuilding != building)
                    throw new ArgumentException("UI ordering references an unknown, duplicate or incompatible definition.");
            return order;
        }
        static int Rank(string[] order, string id) { int index = Array.IndexOf(order, id); return index < 0 ? int.MaxValue : index; }
        static KeyCode ParseKey(string value)
        {
            if (string.IsNullOrEmpty(value) || !char.IsLetter(value[0]) || !Enum.TryParse(value, out KeyCode key) || !Enum.IsDefined(typeof(KeyCode), key) || key == KeyCode.None)
                throw new ArgumentException("UI shortcut must name a valid KeyCode.");
            return key;
        }
        public IReadOnlyList<EntityDefinition> TrainingFor(string producerId) =>
            training.TryGetValue(producerId, out var definitions) ? definitions : Array.Empty<EntityDefinition>();
        public TrainingChoice ChooseTraining(PlayerObservation observation, ICollection<int> selection, string definitionId = null)
        {
            foreach (var entity in observation.Entities)
            {
                if (entity.Owner != observation.Player || !entity.Completed || !selection.Contains(entity.Id)) continue;
                foreach (var definition in TrainingFor(entity.DefinitionId))
                    if (definitionId == null || definition.Id == definitionId) return new TrainingChoice(entity.Id, definition.Id);
            }
            return default;
        }
        public string BuildCaption(EntityDefinition definition)
        {
            string label = RuntimeText.GetOrDefault("build." + definition.Id, definition.DisplayName);
            return buildKeys.TryGetValue(definition.Id, out var key) ? RuntimeText.Format("command.shortcut", label, key) : label;
        }
        public string TrainCaption(EntityDefinition definition)
        {
            string label = RuntimeText.GetOrDefault("train." + definition.Id, definition.DisplayName);
            if (string.IsNullOrEmpty(definition.ProducerId)) return label;
            var choices = TrainingFor(definition.ProducerId);
            return choices.Count > 0 && choices[0].Id == definition.Id ? RuntimeText.Format("command.shortcut", label, TrainKey) : label;
        }
    }
}
