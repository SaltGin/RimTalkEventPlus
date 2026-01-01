using HarmonyLib;
using System;
using Verse;

namespace RimTalkEventPlus
{
    [HarmonyPatch(typeof(Map), "FinalizeInit")]
    public static class Map_FinalizeInit_OngoingEventsDump_Patch
    {
        static void Postfix(Map __instance)
        {
            if (__instance == null) return;

            // Prewarm QuestAffectsMap cache for smoother first call.
            var quests = Find.QuestManager?.QuestsListForReading;
            if (quests != null)
            {
                for (int i = 0; i < quests.Count; i++)
                {
                    var q = quests[i];
                    if (q == null) continue;

                    try
                    {
                        QuestLinkUtil.QuestAffectsMap(q, __instance);
                    }
                    catch (Exception ex)
                    {
                        // Log but don't propagate - this is optional caching, not critical
                        if (Prefs.DevMode)
                            Log.Warning($"[RimTalk Event+] Failed to cache quest {q?.name}:  {ex.Message}");
                    }
                }
            }

            // Only dump/log the ongoing events list in DevMode.
            if (!Prefs.DevMode) return;

            var ongoing = OngoingEventsUtil.GetOngoingEventsNow(
                __instance,
                isInDanger: false,
                maxEvents: 5,
                maxThreatScanBack: 30
            );

            Log.Message($"[RimTalk Event+] Ongoing quests affecting this map at init: {ongoing.Count}");

            foreach (var e in ongoing)
            {
                var singleList = new System.Collections.Generic.List<OngoingEventSnapshot> { e };
                string body = OngoingEventsFormatter.FormatOngoingEventsBlock(singleList, maxChars: 800);
                Log.Message($"[RimTalk Event+] {(e.IsThreat ? "[THREAT]" : "[EVENT]")} {e.Label}\n{body}");
            }
        }
    }
}
