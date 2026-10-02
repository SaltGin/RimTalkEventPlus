using Verse;

namespace RimTalkEventPlus
{
    [StaticConstructorOnStartup]
    public static class BlacklistMigrationStartup
    {
        static BlacklistMigrationStartup()
        {
            // Now DefDatabase is fully populated
            EventFilterSettings settings = RimTalkEventPlus.Settings;
            bool settingsChanged = BlacklistMigrationHelper.TryMigrateBlacklist(settings);
            settingsChanged |= settings.TryMigrateLegacyTypeFilters();
            settingsChanged |= settings.PruneUnsupportedTypeRules();

            if (settingsChanged)
            {
                RimTalkEventPlus.Instance.WriteSettings();
            }
        }
    }
}
