using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimTalkEventPlus
{
    // Per-game cache for quest-related lookups:
    // 1. FieldInfo for QuestPart subclass fields (avoids repeated Type.GetField calls)
    // 2. Quest-Map affinity results (avoids expensive QuestAffectsMap recomputation)
    public class QuestCacheComponent : GameComponent
    {
        // FieldInfo cache for QuestPart subclass fields
        private readonly Dictionary<(Type, string), FieldInfo> _fieldCache =
            new Dictionary<(Type, string), FieldInfo>();

        // Quest-Map affinity cache (key: questId << 32 | mapUniqueId)
        // Equivalent to the deleted QuestAffectsMapCacheComponent functionality
        private readonly Dictionary<long, bool> _questAffectsMapCache =
            new Dictionary<long, bool>();

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

        // Try to get cached quest-map affinity result.
        public bool TryGetQuestAffectsMap(int questId, int mapUniqueId, out bool affects)
        {
            long key = MakeQuestMapKey(questId, mapUniqueId);
            return _questAffectsMapCache.TryGetValue(key, out affects);
        }

        // Store quest-map affinity result in cache.
        public void StoreQuestAffectsMap(int questId, int mapUniqueId, bool affects)
        {
            long key = MakeQuestMapKey(questId, mapUniqueId);
            _questAffectsMapCache[key] = affects;
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