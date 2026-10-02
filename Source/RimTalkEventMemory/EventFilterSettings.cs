using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimTalkEventPlus
{
    // Wrapper class for per-colony disabled instance IDs. 
    // Used as dictionary value for colony-specific instance filtering.
    public class DisabledInstanceSet : IExposable
    {
        public HashSet<string> ids = new HashSet<string>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref ids, "ids", LookMode.Value);
            if (ids == null)
            {
                ids = new HashSet<string>();
            }
        }

        public bool Contains(string id) => ids != null && ids.Contains(id);
        public void Add(string id) => ids.Add(id);
        public bool Remove(string id) => ids.Remove(id);
        public int Count => ids?.Count ?? 0;
    }

    // Settings container for RimTalk Event+ filtering system.
    public class EventFilterSettings : ModSettings
    {
        private const int CurrentFilterSchemaVersion = 2;

        // Reserved WIP preference. It is intentionally retained without a
        // formatter consumer, so it has no prompt-time runtime cost.
        public bool enableEventTextCompression = true;

        // Legacy raw type rules. They are read once and migrated into the
        // category-specific sets below, then cleared permanently.
        public HashSet<string> disabledEventDefNames = new HashSet<string>();

        // Supported type rules are stored with their category instead of being
        // reconstructed from an untyped Def name later.
        public HashSet<string> disabledQuestDefNames = new HashSet<string>();
        public HashSet<string> disabledMapConditionDefNames = new HashSet<string>();
        public HashSet<string> disabledSitePartDefNames = new HashSet<string>();

        // Versioned after DefDatabase is available. Version 2 removes all
        // legacy threat and unresolved raw type rules rather than guessing.
        public int filterSchemaVersion = CurrentFilterSchemaVersion;

        // Stores specific quest instance IDs that are filtered per-colony.
        // The serialized name remains unchanged for legacy settings files.
        // Key:  colony ID (permadeathModeUniqueName)
        // Value: set of local instance IDs disabled for that colony
        public Dictionary<string, DisabledInstanceSet> disabledEventInstances = new Dictionary<string, DisabledInstanceSet>();

        // Internal flag to track if XML blacklist migration has been completed. 
        public bool questBlacklistMigrated = false;

        // Quick category filters for UI
        public bool showQuests = true;
        public bool showMapConditions = true;
        public bool showThreats = true;
        public bool showSiteParts = true;

        // Manual override for Enhanced Prompt conflict lock.
        // false = keep current mandatory lock behavior (default)
        // true  = allow Event+ category filters even when Enhanced Prompt auto-capture is enabled
        public bool allowEnhancedPromptOverlap = false;

        // Effective lock state
        public bool IsEnhancedPromptLockActive =>
            EnhancedPromptDetector.IsAutoEventCaptureEnabled && !allowEnhancedPromptOverlap;

        // Effective values (consider Enhanced Prompt conflict lock + manual override)
        public bool ShowQuestsEffective => showQuests && !IsEnhancedPromptLockActive;
        public bool ShowMapConditionsEffective => showMapConditions && !IsEnhancedPromptLockActive;
        public bool ShowThreatsEffective => showThreats && !IsEnhancedPromptLockActive;
        public bool ShowSitePartsEffective => showSiteParts;

        public bool IsCategoryShown(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.Quest:
                    return ShowQuestsEffective;
                case EventCategory.MapCondition:
                    return ShowMapConditionsEffective;
                case EventCategory.Threat:
                    return ShowThreatsEffective;
                case EventCategory.SitePart:
                    return ShowSitePartsEffective;
                default:
                    return false;
            }
        }

        // When enabled, only append events involving pawns in the conversation context.
        // Threats, map conditions, and site parts are always included.
        public bool EnableContextFiltering = false;

        // Advanced Mode Settings
        public bool AppendToContext = true;

        public EventFilterSettings()
        {
            EnsureCollections();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(
                ref enableEventTextCompression,
                "enableEventTextCompression",
                true
            );

            Scribe_Collections.Look(
                ref disabledEventDefNames,
                "disabledEventDefNames",
                LookMode.Value
            );

            Scribe_Collections.Look(
                ref disabledQuestDefNames,
                "disabledQuestDefNames",
                LookMode.Value
            );

            Scribe_Collections.Look(
                ref disabledMapConditionDefNames,
                "disabledMapConditionDefNames",
                LookMode.Value
            );

            Scribe_Collections.Look(
                ref disabledSitePartDefNames,
                "disabledSitePartDefNames",
                LookMode.Value
            );

            Scribe_Values.Look(
                ref filterSchemaVersion,
                "filterSchemaVersion",
                0
            );

            Scribe_Collections.Look(
                ref disabledEventInstances,
                "disabledEventInstances",
                LookMode.Value,
                LookMode.Deep
            );

            Scribe_Values.Look(
                ref questBlacklistMigrated,
                "questBlacklistMigrated",
                false
            );

            Scribe_Values.Look(
                ref showQuests,
                "showQuests",
                true
            );

            Scribe_Values.Look(
                ref showMapConditions,
                "showMapConditions",
                true
            );

            Scribe_Values.Look(
                ref showThreats,
                "showThreats",
                true
            );

            Scribe_Values.Look(
                ref showSiteParts,
                "showSiteParts",
                true
            );

            Scribe_Values.Look(
                ref EnableContextFiltering,
                "EnableContextFiltering",
                false
            );

            Scribe_Values.Look(
                ref AppendToContext,
                "AppendToContext",
                true
            );

            Scribe_Values.Look(
                ref allowEnhancedPromptOverlap,
                "allowEnhancedPromptOverlap",
                false
            );

            EnsureCollections();
        }

        public static bool SupportsTypeFiltering(EventCategory category)
        {
            return category == EventCategory.Quest ||
                category == EventCategory.MapCondition ||
                category == EventCategory.SitePart;
        }

        public static bool SupportsInstanceFiltering(EventCategory category)
        {
            return category == EventCategory.Quest;
        }

        public bool IsTypeDisabled(EventCategory category, string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return false;

            HashSet<string> typeRules = GetTypeRules(category);
            return typeRules != null && typeRules.Contains(defName);
        }

        public bool DisableType(EventCategory category, string defName)
        {
            if (!SupportsTypeFiltering(category) || string.IsNullOrEmpty(defName))
                return false;

            return GetTypeRules(category).Add(defName);
        }

        public bool EnableType(EventCategory category, string defName)
        {
            if (!SupportsTypeFiltering(category) || string.IsNullOrEmpty(defName))
                return false;

            return GetTypeRules(category).Remove(defName);
        }

        public HashSet<string> GetDisabledTypeDefNames(EventCategory category)
        {
            return GetTypeRules(category);
        }

        public int ClearTypeFilters()
        {
            int count = disabledQuestDefNames.Count +
                disabledMapConditionDefNames.Count +
                disabledSitePartDefNames.Count;

            disabledQuestDefNames.Clear();
            disabledMapConditionDefNames.Clear();
            disabledSitePartDefNames.Clear();
            return count;
        }

        // Quest is the only category that supports instance filtering.
        public bool IsQuestInstanceDisabled(string colonyId, string localInstanceId)
        {
            if (string.IsNullOrEmpty(colonyId) || string.IsNullOrEmpty(localInstanceId))
                return false;
            if (disabledEventInstances == null)
                return false;
            if (!disabledEventInstances.TryGetValue(colonyId, out var instanceSet))
                return false;
            return instanceSet.Contains(localInstanceId);
        }

        // Gets or creates the disabled quest-instance set for a given colony.
        public DisabledInstanceSet GetOrCreateQuestInstanceSet(string colonyId)
        {
            if (string.IsNullOrEmpty(colonyId))
                return null;
            if (disabledEventInstances == null)
                disabledEventInstances = new Dictionary<string, DisabledInstanceSet>();
            if (!disabledEventInstances.TryGetValue(colonyId, out var instanceSet))
            {
                instanceSet = new DisabledInstanceSet();
                disabledEventInstances[colonyId] = instanceSet;
            }
            return instanceSet;
        }

        // Gets the disabled quest-instance set for a given colony, or null if none exists.
        public DisabledInstanceSet GetQuestInstanceSet(string colonyId)
        {
            if (string.IsNullOrEmpty(colonyId) || disabledEventInstances == null)
                return null;
            disabledEventInstances.TryGetValue(colonyId, out var instanceSet);
            return instanceSet;
        }

        // Called only after DefDatabase is populated. It preserves rules whose
        // category is provable and clears every raw rule that cannot be used
        // safely by the new schema, including legacy threat-letter keys.
        public bool TryMigrateLegacyTypeFilters()
        {
            EnsureCollections();
            if (filterSchemaVersion >= CurrentFilterSchemaVersion)
                return false;

            var legacyRules = new List<string>(disabledEventDefNames);
            for (int i = 0; i < legacyRules.Count; i++)
            {
                string defName = legacyRules[i];
                EventCategory? category = ResolveLegacyTypeCategory(defName);
                if (category.HasValue)
                    DisableType(category.Value, defName);
            }

            disabledEventDefNames.Clear();
            filterSchemaVersion = CurrentFilterSchemaVersion;

            return true;
        }

        // Drops newly stale rules as well, for example if the source mod is no
        // longer enabled. No runtime path may infer a category from the key.
        public bool PruneUnsupportedTypeRules()
        {
            EnsureCollections();
            bool changed = false;
            changed |= PruneTypeRules(disabledQuestDefNames, EventCategory.Quest);
            changed |= PruneTypeRules(disabledMapConditionDefNames, EventCategory.MapCondition);
            changed |= PruneTypeRules(disabledSitePartDefNames, EventCategory.SitePart);
            return changed;
        }

        private void EnsureCollections()
        {
            if (disabledEventDefNames == null)
                disabledEventDefNames = new HashSet<string>();
            if (disabledQuestDefNames == null)
                disabledQuestDefNames = new HashSet<string>();
            if (disabledMapConditionDefNames == null)
                disabledMapConditionDefNames = new HashSet<string>();
            if (disabledSitePartDefNames == null)
                disabledSitePartDefNames = new HashSet<string>();
            if (disabledEventInstances == null)
                disabledEventInstances = new Dictionary<string, DisabledInstanceSet>();
        }

        private HashSet<string> GetTypeRules(EventCategory category)
        {
            switch (category)
            {
                case EventCategory.Quest:
                    return disabledQuestDefNames;
                case EventCategory.MapCondition:
                    return disabledMapConditionDefNames;
                case EventCategory.SitePart:
                    return disabledSitePartDefNames;
                default:
                    return null;
            }
        }

        private static EventCategory? ResolveLegacyTypeCategory(string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return null;

            int matchCount = 0;
            EventCategory resolvedCategory = EventCategory.Quest;

            if (DefDatabase<QuestScriptDef>.GetNamedSilentFail(defName) != null)
            {
                resolvedCategory = EventCategory.Quest;
                matchCount++;
            }

            GameConditionDef conditionDef = DefDatabase<GameConditionDef>.GetNamedSilentFail(defName);
            if (conditionDef != null && conditionDef.displayOnUI)
            {
                resolvedCategory = EventCategory.MapCondition;
                matchCount++;
            }

            if (DefDatabase<SitePartDef>.GetNamedSilentFail(defName) != null)
            {
                resolvedCategory = EventCategory.SitePart;
                matchCount++;
            }

            return matchCount == 1 ? resolvedCategory : (EventCategory?)null;
        }

        private static bool PruneTypeRules(HashSet<string> typeRules, EventCategory category)
        {
            var toRemove = new List<string>();
            foreach (string defName in typeRules)
            {
                if (!IsSupportedTypeDef(category, defName))
                    toRemove.Add(defName);
            }

            for (int i = 0; i < toRemove.Count; i++)
                typeRules.Remove(toRemove[i]);

            return toRemove.Count > 0;
        }

        private static bool IsSupportedTypeDef(EventCategory category, string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return false;

            switch (category)
            {
                case EventCategory.Quest:
                    return DefDatabase<QuestScriptDef>.GetNamedSilentFail(defName) != null;
                case EventCategory.MapCondition:
                    GameConditionDef conditionDef = DefDatabase<GameConditionDef>.GetNamedSilentFail(defName);
                    return conditionDef != null && conditionDef.displayOnUI;
                case EventCategory.SitePart:
                    return DefDatabase<SitePartDef>.GetNamedSilentFail(defName) != null;
                default:
                    return false;
            }
        }
    }
}
