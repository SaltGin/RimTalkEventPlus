using HarmonyLib;
using System;
using UnityEngine;
using Verse;

namespace RimTalkEventPlus
{
    /// Settings container for RimTalk Event+.
    public class EventMemorySettings : ModSettings
    {
        /// <summary>
        /// If true, Event+ will compress quest text using XML templates
        /// instead of always sending the full original description.
        /// </summary>
        public bool enableEventTextCompression = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(
                ref enableEventTextCompression,
                "enableEventTextCompression",
                true
            );
        }
    }

    /// Main mod entry point for RimTalk Event+.
    /// Still responsible for applying Harmony patches,
    /// now also exposes a Mod Settings UI.
    public class RimTalkEventPlus : Mod
    {
        public static EventMemorySettings Settings;

        public RimTalkEventPlus(ModContentPack content) : base(content)
        {
            // Load settings
            Settings = GetSettings<EventMemorySettings>();

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
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label("RimTalkEventPlus_OptimizationHeader".Translate());
            listing.GapLine();

            listing.CheckboxLabeled(
                "RimTalkEventPlus_EnableCompression_Label".Translate(),
                ref Settings.enableEventTextCompression,
                "RimTalkEventPlus_EnableCompression_Tooltip".Translate()
            );

            var oldFont = Text.Font;
            Text.Font = GameFont.Tiny;

            listing.Label("RimTalkEventPlus_Compression_Description".Translate());

            Text.Font = oldFont;

            listing.End();
        }
    }
}
