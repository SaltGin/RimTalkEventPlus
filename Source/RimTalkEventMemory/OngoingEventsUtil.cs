using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimTalkEventPlus
{
    public static class OngoingEventsUtil
    {
        // 3 in-game hours * 2500 ticks per hour = 7500 ticks
        // Hell yeah no more magical numbers
        private const int ThreatLetterTimeoutTicks = 7500;

        // Helper method to check if an event should be filtered based on settings.
        private static bool IsEventFiltered(string defName, string instanceID, EventCategory? category, EventFilterSettings settings)
        {
            if (settings == null)
                return false;

            // Filter by category
            if (category.HasValue)
            {
                switch (category.Value)
                {
                    case EventCategory.Quest:
                        if (!settings.showQuests) return true;
                        break;
                    case EventCategory.MapCondition:
                        if (!settings.showMapConditions) return true;
                        break;
                    case EventCategory.Threat:
                        if (!settings.showThreats) return true;
                        break;
                    case EventCategory.SitePart:
                        if (!settings.showSiteParts) return true;
                        break;
                }
            }

            // Filter by type (def name)
            if (!string.IsNullOrEmpty(defName) && settings.IsEventDefDisabled(defName))
                return true;

            // Filter by instance ID (per-colony)
            if (!string.IsNullOrEmpty(instanceID))
            {
                var worldInfo = Find.World?.info;
                string colonyId = worldInfo != null ? $"{worldInfo.seedString}_{worldInfo.persistentRandomValue}" : null;

                if (!string.IsNullOrEmpty(colonyId) && settings.IsEventInstanceDisabled(colonyId, instanceID))
                    return true;
            }

            return false;
        }

        // Get a small list of "ongoing" situations on this map right now.
        // Stateless: reads QuestManager + archive each time.
        //
        // Priority order:
        // - Threat letter: at most one most-recent red threat letter, only if isInDanger == true.
        // - Game conditions: all active GameConditions on this map (solar flare, psychic drone, etc.).
        // - Quests: QuestManager-based, only quests that are ongoing and affect this map.
        public static List<OngoingEventSnapshot> GetOngoingEventsNow(
            Map map,
            bool isInDanger,
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

            // 1) Single active threat letter (raid/siege), only if caller says we're in danger.
            if (isInDanger && result.Count < maxEvents)
            {
                TryAddMostRecentThreatLetter(result, maxEvents, maxThreatScanBack);
            }

            // 2) Active game conditions on this map (solar flare, psychic drone, heat wave, etc.)
            int remaining = maxEvents - result.Count;
            if (remaining > 0)
            {
                TryAddActiveGameConditionsForMap(map, result, remaining);
            }

            // 3) Ongoing quests that affect this map (refugees, guild members, etc.)
            if (result.Count < maxEvents)
            {
                TryAddOngoingQuestsForMap(map, result, maxEvents);
            }

            return result;
        }

        // For non-home maps attached to a Site, add a compact description of the
        // current location based on the SitePartDefs (e.g. bandit camp, ancient ruins).
        private static void TryAddSitePartEvents(Map map, List<OngoingEventSnapshot> result, int maxEvents)
        {
            if (map == null || result == null)
                return;

            if (result.Count >= maxEvents)
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
                if (part == null || part.def == null)
                    continue;

                var def = part.def;

                // Check new filtering system using helper method
                if (IsEventFiltered(def.defName, null, EventCategory.SitePart, RimTalkEventPlus.Settings))
                    continue;

                string label = def.LabelCap;
                if (label.NullOrEmpty())
                {
                    label = def.label;
                }

                string desc = def.description ?? string.Empty;

                // Label: "[current location] ancient mercenaries"
                // Body:  "A hostile company of mercenaries hiding out in an ancient structure."
                result.Add(new OngoingEventSnapshot
                {
                    Kind = "SitePart_" + def.defName,
                    SourceDefName = def.defName,
                    Label = "[current location] " + label,
                    Body = desc,
                    QuestDescription = string.Empty,
                    IsThreat = false
                });
            }
        }


        // Quest side: use QuestManager, no letters.
        private static void TryAddOngoingQuestsForMap(Map map, List<OngoingEventSnapshot> result, int maxEvents)
        {
            if (Find.QuestManager == null)
                return;

            var quests = Find.QuestManager.QuestsListForReading;
            if (quests.NullOrEmpty())
                return;

            foreach (var quest in quests)
            {
                if (quest == null)
                    continue;

                // Check new filtering system using helper method
                string questDefName = quest.root?.defName;
                string questInstanceID = quest.id.ToString();
                if (IsEventFiltered(questDefName, questInstanceID, EventCategory.Quest, RimTalkEventPlus.Settings))
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
                    label = label + " [" + ageMarker + "]";
                }

                // Add key pawn short names if available
                string pawnNames = QuestLinkUtil.GetQuestKeyPawnNames(quest);
                if (!pawnNames.NullOrEmpty())
                {
                    // e.g. "... | characters: Pickles" or multiple names
                    label = label + " | characters: " + pawnNames;
                }

                string desc = QuestLinkUtil.TryGetQuestDescription(quest);

                // Quest root defName for compression templates
                string rootDefName = quest.root != null ? quest.root.defName : null;

                result.Add(new OngoingEventSnapshot
                {
                    Kind = "Quest",
                    SourceDefName = rootDefName,
                    Label = label,
                    Body = desc,
                    QuestDescription = desc,
                    IsThreat = false
                });

                if (result.Count >= maxEvents)
                    break;
            }
        }

        // Game conditions side: all active GameConditions on this map.
        // These are the same things shown in the top-right UI bar above the speed buttons.
        private static void TryAddActiveGameConditionsForMap(
            Map map,
            List<OngoingEventSnapshot> result,
            int maxToAdd)
        {
            if (maxToAdd <= 0 || map == null)
                return;

            var gcm = map.gameConditionManager;
            if (gcm == null)
                return;

            var conds = gcm.ActiveConditions;
            if (conds == null || conds.Count == 0)
                return;

            int added = 0;

            foreach (var cond in conds)
            {
                if (added >= maxToAdd)
                    break;
                if (cond == null || cond.def == null)
                    continue;

                // Skip game conditions that are explicitly hidden from the UI.
                // This corresponds to <displayOnUI>false</displayOnUI> in the GameConditionDef.
                if (!cond.def.displayOnUI)
                    continue;

                // Check new filtering system using helper method
                if (IsEventFiltered(cond.def.defName, null, EventCategory.MapCondition, RimTalkEventPlus.Settings))
                    continue;

                // Use the def's LabelCap/description so we don't depend on newer GameCondition APIs.
                string label = cond.def.LabelCap;
                string body = cond.def.description ?? string.Empty;

                result.Add(new OngoingEventSnapshot
                {
                    Kind = "GameCondition_" + cond.def.defName,
                    SourceDefName = cond.def.defName,
                    Label = label,
                    Body = body,
                    QuestDescription = string.Empty,
                    IsThreat = false
                });

                added++;
            }
        }

        // Threat side: at most one most-recent red threat letter,
        // only if isInDanger == true, and only if it's not too old
        // (currently within 3 in-game hours).
        private static void TryAddMostRecentThreatLetter(
            List<OngoingEventSnapshot> result,
            int maxEvents,
            int maxThreatScanBack)
        {
            if (Find.Archive == null)
                return;

            var list = Find.Archive.ArchivablesListForReading;
            if (list == null || list.Count == 0)
                return;

            // Current in-game time for age computation
            int nowTicks = (Find.TickManager != null) ? Find.TickManager.TicksGame : -1;

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

                // Check new filtering system using helper method
                if (IsEventFiltered(def.defName, null, EventCategory.Threat, RimTalkEventPlus.Settings))
                    continue;

                // Age filter: skip (and stop) if the newest threat is already too old.
                if (nowTicks >= 0 && ThreatLetterTimeoutTicks > 0)
                {
                    int createdTicks = 0;
                    try
                    {
                        createdTicks = a.CreatedTicksGame;
                    }
                    catch
                    {
                        // If we can't read CreatedTicksGame, fall back to old behavior (no age filter).
                        createdTicks = 0;
                    }

                    if (createdTicks > 0)
                    {
                        int ageTicks = nowTicks - createdTicks;
                        if (ageTicks > ThreatLetterTimeoutTicks)
                        {
                            // This is already older than our timeout; since we're scanning from newest
                            // to oldest, all remaining threat letters will be even older.
                            break;
                        }
                    }
                }

                string label;
                string tooltip;
                try { label = a.ArchivedLabel ?? string.Empty; }
                catch { label = string.Empty; }

                try { tooltip = a.ArchivedTooltip ?? string.Empty; }
                catch { tooltip = string.Empty; }

                result.Add(new OngoingEventSnapshot
                {
                    Kind = letter.GetType().Name,
                    SourceDefName = letter.def.defName,
                    Label = label,
                    Body = tooltip,
                    QuestDescription = string.Empty,
                    IsThreat = true
                });

                break; // only one threat event

            }
        }
    }
}
