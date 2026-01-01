using System.Collections.Generic;
using Verse;

namespace RimTalkEventPlus
{
    // Wrapper class for per-colony disabled instance IDs. 
    // Used as dictionary value for colony-specific instance filtering.
    public class DisabledInstanceSet : IExposable
    {
        public HashSet<string> ids = new HashSet<string>();

        public DisabledInstanceSet() { }

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
        public void Clear() => ids.Clear();
        public int Count => ids?.Count ?? 0;
    }

    // Settings container for RimTalk Event+ filtering system.
    public class EventFilterSettings : ModSettings
    {
        // If true, Event+ will compress quest text using XML templates
        // instead of always sending the full original description. 
        public bool enableEventTextCompression = true;

        // Stores event def names that are permanently filtered (type-based filtering).
        // Key: event def name (e.g., "Hospitality_Refugee")
        // Scope: Global (all colonies)
        public HashSet<string> disabledEventDefNames = new HashSet<string>();

        // Stores specific event instance IDs that are filtered per-colony (instance-based filtering).
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

        // Effective values (consider Enhanced Prompt conflict)
        public bool ShowQuestsEffective => showQuests && ! EnhancedPromptDetector.IsAutoEventCaptureEnabled;
        public bool ShowMapConditionsEffective => showMapConditions && !EnhancedPromptDetector.IsAutoEventCaptureEnabled;
        public bool ShowThreatsEffective => showThreats && !EnhancedPromptDetector.IsAutoEventCaptureEnabled;
        public bool ShowSitePartsEffective => showSiteParts;

        public EventFilterSettings()
        {
            if (disabledEventDefNames == null)
                disabledEventDefNames = new HashSet<string>();
            if (disabledEventInstances == null)
                disabledEventInstances = new Dictionary<string, DisabledInstanceSet>();
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

            // Ensure collections are initialized after loading
            if (disabledEventDefNames == null)
                disabledEventDefNames = new HashSet<string>();
            if (disabledEventInstances == null)
                disabledEventInstances = new Dictionary<string, DisabledInstanceSet>();
        }

        // Checks if an event def name is disabled (type-based filtering).
        public bool IsEventDefDisabled(string defName)
        {
            if (string.IsNullOrEmpty(defName))
                return false;
            return disabledEventDefNames != null && disabledEventDefNames.Contains(defName);
        }

        // Checks if a specific event instance is disabled for the given colony (instance-based filtering).
        public bool IsEventInstanceDisabled(string colonyId, string localInstanceId)
        {
            if (string.IsNullOrEmpty(colonyId) || string.IsNullOrEmpty(localInstanceId))
                return false;
            if (disabledEventInstances == null)
                return false;
            if (!disabledEventInstances.TryGetValue(colonyId, out var instanceSet))
                return false;
            return instanceSet.Contains(localInstanceId);
        }

        // Gets or creates the DisabledInstanceSet for a given colony. 
        public DisabledInstanceSet GetOrCreateInstanceSet(string colonyId)
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

        // Gets the DisabledInstanceSet for a given colony, or null if none exists.
        public DisabledInstanceSet GetInstanceSet(string colonyId)
        {
            if (string.IsNullOrEmpty(colonyId) || disabledEventInstances == null)
                return null;
            disabledEventInstances.TryGetValue(colonyId, out var instanceSet);
            return instanceSet;
        }
    }
}