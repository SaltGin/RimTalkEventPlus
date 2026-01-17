using RimTalk.Service;
using RimTalk.Util;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RimTalkEventPlus
{
    // Utility for matching ongoing events against conversation context pawns.
    // Used to filter events and save tokens when appending to LLM prompts.
    public static class ContextPawnMatcher
    {
        // Collect all context-relevant pawn IDs from the conversation.
        // Includes:  pawns parameter (speaker/recipient) + nearby pawns from RimTalk's selector.
        public static HashSet<int> CollectContextPawnIds(List<Pawn> pawns, Pawn initiator, Pawn recipient)
        {
            var pawnIds = new HashSet<int>();

            // Add pawns from DecoratePrompt parameter (speaker, recipient, etc.)
            if (pawns != null)
            {
                foreach (var p in pawns)
                {
                    if (p != null)
                        pawnIds.Add(p.thingIDNumber);
                }
            }

            // Add nearby pawns using RimTalk's selector
            try
            {
                var nearbyPawns = PawnSelector.GetAllNearByPawns(initiator, recipient);
                if (nearbyPawns != null)
                {
                    foreach (var p in nearbyPawns)
                    {
                        if (p != null)
                            pawnIds.Add(p.thingIDNumber);
                    }
                }
            }
            catch
            {
                // PawnSelector may not be available, ignore
            }

            return pawnIds;
        }

        // Filter events to only those relevant to the conversation context.
        //
        // Non-Quest events (GameConditions, SiteParts, Threats): Always include
        // Quests with no pawns: Always include
        // Quests with pawns: Include only if any pawn overlaps with context
        public static List<OngoingEventSnapshot> FilterEventsByContext(
            List<OngoingEventSnapshot> events,
            HashSet<int> contextPawnIds)
        {
            if (events == null || events.Count == 0)
                return events;

            // If no context pawns, return all events (fallback)
            if (contextPawnIds == null || contextPawnIds.Count == 0)
                return events;

            var filtered = new List<OngoingEventSnapshot>();

            var questManager = Find.QuestManager;
            var questsById = new Dictionary<int, Quest>();

            if (questManager != null)
            {
                foreach (var quest in questManager.QuestsListForReading)
                {
                    if (quest != null)
                        questsById[quest.id] = quest;
                }
            }

            foreach (var evt in events)
            {
                if (ShouldIncludeEvent(evt, contextPawnIds, questsById))
                    filtered.Add(evt);
            }

            return filtered;
        }

        // Determine if an event should be included based on context pawns.
        private static bool ShouldIncludeEvent(
            OngoingEventSnapshot evt,
            HashSet<int> contextPawnIds,
            Dictionary<int, Quest> questsById)
        {
            // Always include threats
            if (evt.IsThreat)
                return true;

            // Always include non-quest events (GameConditions, SiteParts)
            if (evt.Kind == null || !evt.Kind.Equals("Quest"))
                return true;

            // For quests, we need to find the quest and check pawn overlap
            // Try to find the quest by matching SourceDefName against ongoing quests
            Quest matchedQuest = null;

            foreach (var kvp in questsById)
            {
                var quest = kvp.Value;
                if (quest == null || !QuestLinkUtil.IsQuestOngoing(quest))
                    continue;

                string questDefName = quest.root?.defName;
                if (questDefName != null && questDefName == evt.SourceDefName)
                {
                    // Additional check: match by label content to handle multiple quests with same def
                    string questLabel = QuestLinkUtil.TryGetQuestLabel(quest);
                    if (evt.Label != null && evt.Label.StartsWith(questLabel))
                    {
                        matchedQuest = quest;
                        break;
                    }
                }
            }

            if (matchedQuest == null)
            {
                // Could not find quest - include by default
                return true;
            }

            var questPawns = QuestLinkUtil.GetQuestKeyPawns(matchedQuest);

            // Quest has no pawns - always include
            if (questPawns == null || questPawns.Count == 0)
                return true;

            // Check if any quest pawn is in context
            foreach (var p in questPawns)
            {
                if (p != null && contextPawnIds.Contains(p.thingIDNumber))
                    return true;
            }

            // No overlap - filter out
            return false;
        }

        // Check if a specific quest involves any of the context pawns.
        public static bool QuestInvolvesContextPawns(Quest quest, HashSet<int> contextPawnIds)
        {
            if (quest == null || contextPawnIds == null || contextPawnIds.Count == 0)
                return true; // Default to include

            var questPawns = QuestLinkUtil.GetQuestKeyPawns(quest);

            // Quest has no pawns - consider it relevant
            if (questPawns == null || questPawns.Count == 0)
                return true;

            // Check overlap
            foreach (var p in questPawns)
            {
                if (p != null && contextPawnIds.Contains(p.thingIDNumber))
                    return true;
            }

            return false;
        }
    }
}