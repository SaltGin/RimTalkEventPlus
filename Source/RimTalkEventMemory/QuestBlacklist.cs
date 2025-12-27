using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RimTalkEventPlus
{
    // Legacy quest blacklist system.
    [System.Obsolete("This blacklist system is deprecated. Use EventFilterSettings instead. Kept for backward compatibility during migration.")]
    public static class QuestBlacklist
    {
        private static bool _initialized;
        private static HashSet<string> _blacklistedRoots;

        private static void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _blacklistedRoots = new HashSet<string>();

            // Gather all RimTalkQuestBlacklistDef instances, so users/modders can patch/add more.
            var defs = DefDatabase<RimTalkQuestBlacklistDef>.AllDefsListForReading;
            if (defs == null)
                return;

            foreach (var def in defs)
            {
                if (def?.blacklistedQuestRoots == null)
                    continue;

                foreach (var root in def.blacklistedQuestRoots)
                {
                    if (string.IsNullOrEmpty(root))
                        continue;

                    _blacklistedRoots.Add(root);
                }
            }
        }

        public static bool IsBlacklisted(Quest quest)
        {
            if (quest == null)
                return false;

            EnsureInitialized();

            if (_blacklistedRoots == null || _blacklistedRoots.Count == 0)
                return false;

            var root = quest.root;
            if (root == null)
                return false;

            var defName = root.defName;
            if (string.IsNullOrEmpty(defName))
                return false;

            return _blacklistedRoots.Contains(defName);
        }
    }
}
