using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RimTalkEventPlus
{
    // Helper class for migrating the old XML-based quest blacklist to the new EventFilterSettings system.
    // Migration happens during mod initialization after defs are loaded.
    public static class BlacklistMigrationHelper
    {
        // Migrates blacklisted quests from XML definitions to EventFilterSettings.
        // Only runs once per settings file based on the questBlacklistMigrated flag.
        // Safe to call during mod initialization as DefDatabase is already populated.
        public static void TryMigrateBlacklist(EventFilterSettings settings)
        {
            if (settings == null)
                return;

            // Check if migration has already been completed
            if (settings.questBlacklistMigrated)
                return;

            try
            {
                var blacklistedDefs = new List<string>();
                var validatedDefs = new List<string>();
                var skippedDefs = new List<string>();

                // Read all entries from DefDatabase<RimTalkQuestBlacklistDef>
                // This is safe at mod initialization time as defs are already loaded
                var defs = DefDatabase<RimTalkQuestBlacklistDef>.AllDefsListForReading;
                if (defs != null && defs.Count > 0)
                {
                    foreach (var def in defs)
                    {
                        if (def?.blacklistedQuestRoots == null)
                            continue;

                        foreach (var questDefName in def.blacklistedQuestRoots)
                        {
                            if (string.IsNullOrEmpty(questDefName))
                                continue;

                            blacklistedDefs.Add(questDefName);
                        }
                    }
                }

                // Validate each blacklisted quest def name
                foreach (var questDefName in blacklistedDefs)
                {
                    // Check if the corresponding QuestScriptDef exists
                    var questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(questDefName);
                    if (questDef != null)
                    {
                        // Quest def exists, migrate it
                        settings.disabledEventDefNames.Add(questDefName);
                        validatedDefs.Add(questDefName);
                    }
                    else
                    {
                        // Quest def doesn't exist in current mod loadout, skip it
                        skippedDefs.Add(questDefName);
                    }
                }

                // Mark migration as complete
                settings.questBlacklistMigrated = true;

                // Log migration results
                if (validatedDefs.Count > 0 || skippedDefs.Count > 0)
                {
                    Log.Message($"[RimTalk Event+] Blacklist migration completed:");
                    if (validatedDefs.Count > 0)
                    {
                        Log.Message($"  - Migrated {validatedDefs.Count} quest type(s) to new filter system: {string.Join(", ", validatedDefs)}");
                    }
                    if (skippedDefs.Count > 0)
                    {
                        Log.Message($"  - Skipped {skippedDefs.Count} quest type(s) not found in current mod loadout: {string.Join(", ", skippedDefs)}");
                    }
                }
                else
                {
                    Log.Message("[RimTalk Event+] Blacklist migration completed: No blacklist entries found.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimTalk Event+] Error during blacklist migration: {ex.Message}\n{ex.StackTrace}");
                // Still mark as migrated to avoid repeated failures
                settings.questBlacklistMigrated = true;
            }
        }
    }
}
