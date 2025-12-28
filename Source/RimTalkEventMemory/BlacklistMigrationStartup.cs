using Verse;

namespace RimTalkEventPlus
{
    [StaticConstructorOnStartup]
    public static class BlacklistMigrationStartup
    {
        static BlacklistMigrationStartup()
        {
            // Now DefDatabase is fully populated
            if (BlacklistMigrationHelper.TryMigrateBlacklist(RimTalkEventPlus.Settings))
            {
                RimTalkEventPlus.Instance.WriteSettings();
            }
        }
    }
}