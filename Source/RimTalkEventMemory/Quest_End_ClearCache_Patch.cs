using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalkEventPlus
{
    // Ended quests are excluded from prompt collection, but clearing their runtime
    // entries prevents the per-game cache from retaining completed quest data.
    [HarmonyPatch(typeof(Quest), "End")]
    public static class Quest_End_ClearCache_Patch
    {
        static void Postfix(Quest __instance)
        {
            if (__instance?.id < 0)
                return;

            Current.Game?.GetComponent<QuestCacheComponent>()?.InvalidateQuest(__instance.id);
        }
    }
}
