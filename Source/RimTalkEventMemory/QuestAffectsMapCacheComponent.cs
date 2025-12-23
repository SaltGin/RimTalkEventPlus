using System.Collections.Generic;
using Verse;

namespace RimTalkEventPlus
{
    // Per-save cache
    // Goal: avoid repeating QuestAffectsMap reflection scans every talk.
    public class QuestAffectsMapCacheComponent : GameComponent
    {
        // Revalidate after this many ticks on access.
        // 60,000 ticks = 1 in-game day.
        private const int RevalidateIntervalTicks = 60000;

        private struct Entry
        {
            public bool affects;
            public int lastValidatedTick;
        }

        private readonly Dictionary<long, Entry> _cache = new Dictionary<long, Entry>();

        public QuestAffectsMapCacheComponent(Game game) : base()
        {
        }

        private static long MakeKey(int questId, int mapUniqueId)
        {
            return ((long)questId << 32) | (uint)mapUniqueId;
        }

        public bool TryGet(int questId, int mapUniqueId, int nowTick, out bool affects)
        {
            affects = false;

            long key = MakeKey(questId, mapUniqueId);
            if (!_cache.TryGetValue(key, out var entry))
                return false;

            if (nowTick - entry.lastValidatedTick > RevalidateIntervalTicks)
                return false;

            affects = entry.affects;

            return true;
        }

        public void Store(int questId, int mapUniqueId, int nowTick, bool affects)
        {
            long key = MakeKey(questId, mapUniqueId);
            _cache[key] = new Entry
            {
                affects = affects,
                lastValidatedTick = nowTick
            };
        }

        // For manually clearing cache
        public void Clear()
        {
            _cache.Clear();
        }
    }
}
