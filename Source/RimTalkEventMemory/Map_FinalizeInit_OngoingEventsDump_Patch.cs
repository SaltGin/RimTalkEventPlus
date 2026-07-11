using HarmonyLib;
using Verse;

namespace RimTalkEventPlus
{
    [HarmonyPatch(typeof(Map), "FinalizeInit")]
    public static class Map_FinalizeInit_OngoingEventsDump_Patch
    {
        static void Postfix(Map __instance)
        {
            if (__instance == null) return;

            // Run once the current long event has completed. This keeps the
            // expensive reflection work out of Map.FinalizeInit while warming the
            // cache before the first player conversation on the map.
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                // The map can be removed before the deferred callback runs.
                if (Current.Game == null || Find.Maps == null || !Find.Maps.Contains(__instance))
                    return;

                Current.Game.GetComponent<QuestCacheComponent>()?.PrewarmActiveQuestsForMap(__instance);
            });

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
