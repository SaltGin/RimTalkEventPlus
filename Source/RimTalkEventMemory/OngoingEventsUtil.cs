using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimTalkEventPlus
{
    public static class OngoingEventsUtil
    {
        // Categories are checked once before collection; these rules apply per event.
        private static bool IsTypeOrInstanceFiltered(
            EventCategory category,
            string defName,
            string instanceID,
            EventFilterSettings settings)
        {
            if (settings == null)
                return false;

            if (!EventFilterSettings.SupportsTypeFiltering(category))
                return false;

            if (settings.IsTypeDisabled(category, defName))
                return true;

            if (EventFilterSettings.SupportsInstanceFiltering(category) &&
                !string.IsNullOrEmpty(instanceID))
            {
                var worldInfo = Find.World?.info;
                string colonyId = worldInfo != null ? $"{worldInfo.seedString}_{worldInfo.persistentRandomValue}" : null;

                if (!string.IsNullOrEmpty(colonyId) && settings.IsQuestInstanceDisabled(colonyId, instanceID))
                    return true;
            }

            return false;
        }

        // Get a small list of "ongoing" situations on this map right now.
        // Stateless: reads QuestManager + archive each time.
        //
        // Priority order:
        // - Detailed structured threats: live Lord-backed snapshots, with
        //   raids represented as their richer subtype.
        // - Generic threat letter: at most one red letter with a target that
        //   RimWorld currently considers an active threat on this map.
        // - Game conditions: all active GameConditions on this map (solar flare, psychic drone, etc.).
        // - Quests: QuestManager-based, only quests that are ongoing and affect this map.
        public static List<OngoingEventSnapshot> GetOngoingEventsNow(
            Map map,
            int maxEvents = 5,
            int maxThreatScanBack = 30)
        {
            var result = new List<OngoingEventSnapshot>();
            if (map == null || Current.Game == null)
                return result;

            // 0) For non-home maps (quest sites, temporary maps), prepend current location info
            //    from SitePartDefs if available (e.g. "ancient mercenaries").
            if (!map.IsPlayerHome)
            {
                TryAddSitePartEvents(map, result, maxEvents);
            }

            // 1) Lord-backed threats are independent from the letter path.
            if (result.Count < maxEvents)
            {
                TryAddActiveThreatsForMap(map, result, maxEvents);
            }

            // 2) Generic threats verify their own live target. DangerWatcher is
            // not used as truth because it is cached/map-wide and excludes
            // fleeing groups.
            if (result.Count < maxEvents)
            {
                TryAddMostRecentThreatLetter(map, result, maxEvents, maxThreatScanBack);
            }

            // 3) Active game conditions on this map (solar flare, psychic drone, heat wave, etc.)
            int remaining = maxEvents - result.Count;
            if (remaining > 0)
            {
                TryAddActiveGameConditionsForMap(map, result, remaining);
            }

            // 4) Ongoing quests that affect this map (refugees, guild members, etc.)
            if (result.Count < maxEvents)
            {
                TryAddOngoingQuestsForMap(map, result, maxEvents);
            }

            return result;
        }

        // For non-home maps attached to a Site, add a compact description of the
        // current location based on the SitePartDefs (e.g. bandit camp, ancient ruins).
        public static void TryAddSitePartEvents(Map map, List<OngoingEventSnapshot> result, int maxEvents)
        {
            if (map == null || result == null)
                return;

            if (result.Count >= maxEvents)
                return;

            var settings = RimTalkEventPlus.Settings;
            if (settings != null && !settings.IsCategoryShown(EventCategory.SitePart))
                return;

            MapParent parent = map.Parent;
            if (parent == null)
                return;

            // Only treat Site-based maps as special locations here.
            Site site = parent as Site;
            if (site == null)
                return;

            var parts = site.parts;
            if (parts == null || parts.Count == 0)
                return;

            foreach (var part in parts)
            {
                if (result.Count >= maxEvents)
                    break;
                if (part == null || part.def == null || part.hidden)
                    continue;

                var def = part.def;

                // Check new filtering system using helper method
                if (IsTypeOrInstanceFiltered(EventCategory.SitePart, def.defName, null, settings))
                    continue;

                string label = def.LabelCap;

                if (part == parts[0])
                    label = site.LabelCap;
                if (label.NullOrEmpty())
                {
                    label = def.label;
                }

                string desc = def.description ?? string.Empty;

                // Label: "[current location] ancient mercenaries"
                // Body:  "A hostile company of mercenaries hiding out in an ancient structure."
                result.Add(new OngoingEventSnapshot
                {
                    Category = EventCategory.SitePart,
                    SourceDefName = def.defName,
                    Label = "[current location] " + label,
                    Body = desc,
                    IsThreat = false
                });
            }
        }

        // Structured Lord threats provide richer live information than a letter
        // alone. Non-Lord threats (such as manhunter packs) use the live-target
        // letter fallback below.
        public static void TryAddActiveThreatsForMap(
            Map map,
            List<OngoingEventSnapshot> result,
            int maxEvents)
        {
            if (map == null || result == null || result.Count >= maxEvents || Current.Game == null)
                return;

            var settings = RimTalkEventPlus.Settings;
            if (settings != null && !settings.IsCategoryShown(EventCategory.Threat))
                return;

            ThreatTrackerComponent tracker = Current.Game.GetComponent<ThreatTrackerComponent>();
            if (tracker == null)
                return;

            List<OngoingEventSnapshot> threats = tracker.GetPromptSnapshotsForMap(map, int.MaxValue);
            for (int i = 0; i < threats.Count && result.Count < maxEvents; i++)
            {
                OngoingEventSnapshot threat = threats[i];
                if (threat == null)
                    continue;

                result.Add(threat);
            }
        }

        // Quest side: use QuestManager, no letters.
        public static void TryAddOngoingQuestsForMap(Map map, List<OngoingEventSnapshot> result, int maxEvents)
        {
            var settings = RimTalkEventPlus.Settings;
            if (settings != null && !settings.IsCategoryShown(EventCategory.Quest))
                return;

            if (Find.QuestManager == null)
                return;

            var quests = Find.QuestManager.ActiveQuestsListForReading;
            if (quests.NullOrEmpty())
                return;

            foreach (var quest in quests)
            {
                if (quest == null)
                    continue;

                // Skip endgame quests
                string rootDefName = quest.root?.defName;
                if (QuestLinkUtil.IsQuestRootExcluded(rootDefName))
                    continue;

                // Check new filtering system using helper method
                string questDefName = quest.root?.defName;
                string questInstanceID = quest.id.ToString();
                if (IsTypeOrInstanceFiltered(EventCategory.Quest, questDefName, questInstanceID, settings))
                    continue;

                if (QuestLinkUtil.IsQuestHidden(quest))
                    continue;

                if (!QuestLinkUtil.IsQuestOngoing(quest))
                    continue;

                if (!QuestLinkUtil.QuestAffectsMap(quest, map))
                    continue;

                // Base label + age marker
                string label = QuestLinkUtil.TryGetQuestLabel(quest);
                string ageMarker = QuestLinkUtil.GetQuestAcceptedAgeMarker(quest);
                if (!ageMarker.NullOrEmpty())
                {
                    // e.g. "Pickles the Destitute [accepted ~1.3 days ago]"
                    label = label + " [" + ageMarker + "; quest is active and underway]";
                }

                // Add key pawn short names if available
                string pawnNames = QuestLinkUtil.GetQuestKeyPawnNames(quest);
                if (!pawnNames.NullOrEmpty())
                {
                    // e.g. "... | characters: Pickles" or multiple names
                    label = label + " | characters: " + pawnNames;
                }

                string desc = QuestLinkUtil.TryGetQuestDescription(quest);

                result.Add(new OngoingEventSnapshot
                {
                    Category = EventCategory.Quest,
                    SourceDefName = rootDefName,
                    QuestId = quest.id,
                    Label = label,
                    Body = desc,
                    IsThreat = false
                });

                if (result.Count >= maxEvents)
                    break;
            }
        }

        // Game conditions side: all active GameConditions on this map.
        // These are the same things shown in the top-right UI bar above the speed buttons.
        public static void TryAddActiveGameConditionsForMap(
            Map map,
            List<OngoingEventSnapshot> result,
            int maxToAdd)
        {
            if (maxToAdd <= 0 || map == null)
                return;

            var settings = RimTalkEventPlus.Settings;
            if (settings != null && !settings.IsCategoryShown(EventCategory.MapCondition))
                return;

            var gcm = map.gameConditionManager;
            if (gcm == null)
                return;

            var conds = new List<GameCondition>();
            gcm.GetAllGameConditionsAffectingMap(map, conds);
            if (conds.Count == 0)
                return;

            int added = 0;

            foreach (var cond in conds)
            {
                if (added >= maxToAdd)
                    break;
                if (cond == null || cond.def == null)
                    continue;

                // The native collector checks map applicability, including world conditions.
                // Apply the remaining visibility rules used by RimWorld's condition UI.
                if (!cond.def.displayOnUI || cond.HiddenByOtherCondition(map))
                    continue;

                // Check new filtering system using helper method
                if (IsTypeOrInstanceFiltered(EventCategory.MapCondition, cond.def.defName, null, settings))
                    continue;

                // Use the instance's Label/Description
                string label = cond.Label;
                string body = cond.Description ?? string.Empty;

                result.Add(new OngoingEventSnapshot
                {
                    Category = EventCategory.MapCondition,
                    SourceDefName = cond.def.defName,
                    Label = label,
                    Body = body,
                    IsThreat = false
                });

                added++;
            }
        }

        // Gets the current colony identifier for per-colony instance filtering. 
        // Shared between OngoingEventsUtil and EventFilterUI.
        public static string GetCurrentColonyId()
        {
            if (Current.Game == null)
                return null;

            var worldInfo = Find.World?.info;
            if (worldInfo == null)
                return null;

            return $"{worldInfo.seedString ?? ""}_{worldInfo.persistentRandomValue}";
        }

        // Threat side: at most one most-recent red threat letter whose target
        // is currently a live threat on this exact map. Archive age is not a
        // lifecycle signal and is deliberately not used here.
        public static void TryAddMostRecentThreatLetter(
            Map map,
            List<OngoingEventSnapshot> result,
            int maxEvents,
            int maxThreatScanBack)
        {
            if (map == null || Find.Archive == null)
                return;

            var settings = RimTalkEventPlus.Settings;
            if (settings != null && !settings.IsCategoryShown(EventCategory.Threat))
                return;

            var list = Find.Archive.ArchivablesListForReading;
            if (list == null || list.Count == 0)
                return;

            ThreatTrackerComponent tracker = Current.Game?.GetComponent<ThreatTrackerComponent>();
            if (tracker == null)
                return;

            int count = list.Count;
            int scanned = 0;

            // Walk backwards: newest -> older, stop after the first suitable threat
            for (int i = count - 1; i >= 0 && scanned < maxThreatScanBack && result.Count < maxEvents; i--, scanned++)
            {
                IArchivable a = list[i];
                if (a == null)
                    continue;

                // Only Letters are interesting here
                if (!(a is Letter letter && letter.def != null))
                    continue;

                var def = letter.def;
                bool isThreatLetter = def == LetterDefOf.ThreatBig || def == LetterDefOf.ThreatSmall;
                if (!isThreatLetter)
                    continue;

                // The pipeline decides whether a specialized instance owns or
                // intentionally suppresses this letter before generic fallback.
                if (!tracker.TryGetGenericLetterSnapshot(letter, map, out OngoingEventSnapshot threat))
                    continue;

                result.Add(threat);

                break; // only one threat event

            }

        }

    }
}
