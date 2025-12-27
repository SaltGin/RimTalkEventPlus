using HarmonyLib;
using System;
using UnityEngine;
using Verse;

namespace RimTalkEventPlus
{
    // Main mod entry point for RimTalk Event+.
    // Still responsible for applying Harmony patches,
    // now also exposes a Mod Settings UI.
    public class RimTalkEventPlus : Mod
    {
        public static EventFilterSettings Settings;

        public RimTalkEventPlus(ModContentPack content) : base(content)
        {
            // Load settings
            Settings = GetSettings<EventFilterSettings>();

            // Migrate old blacklist if needed
            BlacklistMigrationHelper.TryMigrateBlacklist(Settings);

            // Existing behavior: Harmony patches + log
            var harmony = new Harmony("saltgin.rimtalkeventmemory");
            harmony.PatchAll();
            Log.Message("[RimTalk Event+] Loaded.");
        }

        public override string SettingsCategory()
        {
            // Label in Options → Mod Settings
            return "RimTalkEventPlus_SettingsCategory".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            EventFilterUI.DoFilteringUI(inRect, Settings);
        }
    }
}
