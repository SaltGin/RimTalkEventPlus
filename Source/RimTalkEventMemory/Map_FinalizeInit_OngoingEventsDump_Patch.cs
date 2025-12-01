using HarmonyLib;
using Verse;

namespace RimTalkEventPlus
{
    [HarmonyPatch(typeof(Map), "FinalizeInit")]
    public static class Map_FinalizeInit_OngoingEventsDump_Patch
    {
        static void Postfix(Map __instance)
        {
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
