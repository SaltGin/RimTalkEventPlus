using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimTalkEventPlus
{
    // Per-game cache for quest-related lookups:
    // 1. FieldInfo for QuestPart subclass fields (avoids repeated Type.GetField calls)
    // 2. Known-positive quest-map affinity results (avoids expensive recomputation)
    // 3. Quest-Pawns extraction results (avoids repeated reflection on quest parts)
    public class QuestCacheComponent : GameComponent
    {
        // FieldInfo cache for QuestPart subclass fields
        private readonly Dictionary<(Type, string), FieldInfo> _fieldCache =
            new Dictionary<(Type, string), FieldInfo>();

        // Quest-Map affinity cache (key: questId << 32 | mapUniqueId)
        private readonly Dictionary<long, bool> _questAffectsMapCache =
            new Dictionary<long, bool>();

        // Quest-Pawns cache (key: questId, value: list of pawns involved in quest)
        private readonly Dictionary<int, List<Pawn>> _questPawnsCache =
            new Dictionary<int, List<Pawn>>();

        private const BindingFlags AllInstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public QuestCacheComponent(Game game) : base()
        {
        }

        #region FieldInfo Cache

        // Get cached FieldInfo for a QuestPart subclass field.
        // Returns null if field doesn't exist (result is cached to avoid repeated lookups).
        public FieldInfo GetField(Type type, string fieldName)
        {
            var key = (type, fieldName);
            if (_fieldCache.TryGetValue(key, out var cached))
                return cached;

            var field = type.GetField(fieldName, AllInstanceFlags);
            _fieldCache[key] = field;
            return field;
        }

        #endregion

        #region Quest-Map Affinity Cache

        private static long MakeQuestMapKey(int questId, int mapUniqueId)
        {
            return ((long)questId << 32) | (uint)mapUniqueId;
        }

        // Only positive results are cached. A missing entry means the relationship
        // must be checked again, so a quest that retargets later is not hidden.
        public bool TryGetQuestAffectsMap(int questId, int mapUniqueId, out bool affects)
        {
            long key = MakeQuestMapKey(questId, mapUniqueId);
            return _questAffectsMapCache.TryGetValue(key, out affects);
        }

        // Store a known-positive quest-map affinity result.
        public void StoreQuestAffectsMap(int questId, int mapUniqueId, bool affects)
        {
            if (!affects)
                return;

            long key = MakeQuestMapKey(questId, mapUniqueId);
            _questAffectsMapCache[key] = affects;
        }

        // Clear all runtime data for a quest once it has ended.
        public void InvalidateQuest(int questId)
        {
            if (questId < 0)
                return;

            List<long> keysToRemove = null;
            foreach (var entry in _questAffectsMapCache)
            {
                if ((int)(entry.Key >> 32) != questId)
                    continue;

                if (keysToRemove == null)
                    keysToRemove = new List<long>();
                keysToRemove.Add(entry.Key);
            }

            if (keysToRemove != null)
            {
                foreach (var key in keysToRemove)
                    _questAffectsMapCache.Remove(key);
            }

            _questPawnsCache.Remove(questId);
        }

        #endregion

        #region Map Prewarm

        // Called after the map-generation long event has completed, rather than
        // querying quest parts directly from Map.FinalizeInit.
        public void PrewarmActiveQuestsForMap(Map map)
        {
            if (map == null)
                return;

            var quests = Find.QuestManager?.ActiveQuestsListForReading;
            if (quests == null)
                return;

            for (int i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (!QuestLinkUtil.IsQuestOngoing(quest))
                    continue;

                try
                {
                    QuestLinkUtil.QuestAffectsMap(quest, map);
                }
                catch (Exception ex)
                {
                    // Prewarm is optional; never let it interfere with gameplay.
                    if (Prefs.DevMode)
                        Log.Warning($"[RimTalk Event+] Failed to prewarm quest {quest?.name}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Quest-Pawns Cache

        // Try to get cached quest pawns list.
        public bool TryGetQuestPawns(int questId, out List<Pawn> pawns)
        {
            return _questPawnsCache.TryGetValue(questId, out pawns);
        }

        // Store quest pawns list in cache.
        public void StoreQuestPawns(int questId, List<Pawn> pawns)
        {
            _questPawnsCache[questId] = pawns;
        }

        // Clear cached pawns for a specific quest (call when quest state changes).
        public void InvalidateQuestPawns(int questId)
        {
            _questPawnsCache.Remove(questId);
        }

        #endregion
    }

    // DEPRECATED STUB: Preserves backward compatibility with saves that reference
    // the old QuestAffectsMapCacheComponent class.
    public class QuestAffectsMapCacheComponent : GameComponent
    {
        public QuestAffectsMapCacheComponent(Game game) : base() { }
        public override void ExposeData() { }
    }
}
