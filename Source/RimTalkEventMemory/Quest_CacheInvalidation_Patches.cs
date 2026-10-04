using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalkEventPlus
{
    internal static class QuestCacheInvalidation
    {
        internal static void Invalidate(Quest quest)
        {
            if (quest == null || quest.id < 0)
                return;

            Current.Game?.GetComponent<QuestCacheComponent>()?.InvalidateQuest(quest.id);
        }
    }

    [HarmonyPatch(typeof(Quest), nameof(Quest.Notify_SignalReceived))]
    internal static class Quest_Signal_CacheInvalidationPatch
    {
        private static void Prefix(Quest __instance, Signal signal, out bool __state)
        {
            // Match the native dispatcher, including custom and global signals.
            __state = signal.global || (signal.tag != null &&
                signal.tag.StartsWith($"Quest{__instance.id}.", StringComparison.Ordinal));
        }

        private static void Postfix(Quest __instance, bool __state)
        {
            if (__state)
                QuestCacheInvalidation.Invalidate(__instance);
        }
    }

    [HarmonyPatch]
    internal static class Quest_Parts_CacheInvalidationPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Quest), nameof(Quest.AddPart), new[] { typeof(QuestPart) });
            yield return AccessTools.Method(typeof(Quest), nameof(Quest.RemovePart), new[] { typeof(QuestPart) });
        }

        private static void Prefix(Quest __instance, out int __state)
        {
            __state = __instance.PartsListForReading.Count;
        }

        private static void Postfix(Quest __instance, int __state)
        {
            // Skip ordinary quest construction, which cannot have an ongoing cache entry.
            if (__instance.State == QuestState.Ongoing && __instance.PartsListForReading.Count != __state)
                QuestCacheInvalidation.Invalidate(__instance);
        }
    }

    [HarmonyPatch]
    internal static class QuestPart_ReplacePawns_CacheInvalidationPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            var parameters = new[] { typeof(Pawn), typeof(Pawn) };
            // Discover loaded vanilla and mod overrides once. The empty base method
            // is insufficient because many implementations do not call it.
            foreach (Type type in AccessTools.AllTypes())
            {
                if (type == typeof(QuestPart) || !typeof(QuestPart).IsAssignableFrom(type) || type.ContainsGenericParameters)
                    continue;

                MethodInfo method = AccessTools.DeclaredMethod(type, nameof(QuestPart.ReplacePawnReferences), parameters);
                if (method != null && !method.IsAbstract && !method.IsStatic && !method.ContainsGenericParameters)
                    yield return method;
            }
        }

        private static void Prefix(QuestPart __instance, out Quest __state)
        {
            __state = __instance.quest;
        }

        private static void Postfix(Quest __state)
        {
            QuestCacheInvalidation.Invalidate(__state);
        }
    }
}
