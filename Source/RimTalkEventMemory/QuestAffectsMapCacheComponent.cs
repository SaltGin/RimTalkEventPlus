using System.Collections.Generic;
using Verse;

namespace RimTalkEventPlus
{
    // Per-save cache
    // Goal: avoid repeating QuestAffectsMap reflection scans every talk.
    public class QuestAffectsMapCacheComponent : GameComponent
    {
        private struct Entry
        {
            public bool affects;
        }

        private readonly Dictionary<long, Entry> _cache = new Dictionary<long, Entry>();

        public QuestAffectsMapCacheComponent(Game game) : base()
        {
        }

        private static long MakeKey(int questId, int mapUniqueId)
        {
            return ((long)questId << 32) | (uint)mapUniqueId;
        }

        public bool TryGet(int questId, int mapUniqueId, out bool affects)
        {
            affects = false;

            long key = MakeKey(questId, mapUniqueId);
            if (!_cache.TryGetValue(key, out var entry))
                return false;

            affects = entry.affects;

            return true;
        }

        public void Store(int questId, int mapUniqueId, bool affects)
        {
            long key = MakeKey(questId, mapUniqueId);
            _cache[key] = new Entry
            {
                affects = affects
            };
        }

        // For manually clearing cache
        public void Clear()
        {
            _cache.Clear();
        }
    }
}
