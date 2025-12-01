using System;
using System.Collections.Generic;
using HarmonyLib;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.Util;
using RimWorld;
using Verse;

namespace RimTalkEventPlus
{
    /// Postfix that appends ongoing events to RimTalk's prompt.
    /// NOTE: patched manually by PromptService_OngoingEventsPatcher.
    public static class PromptService_OngoingEventsPatch
    {
        // Must match: void DecoratePrompt(TalkRequest talkRequest, List<Pawn> pawns, string status)
        public static void Postfix(TalkRequest talkRequest, List<Pawn> pawns, string status)
        {
            try
            {
                if (talkRequest == null)
                    return;

                Pawn initiator = talkRequest.Initiator;
                if (initiator == null || initiator.Map == null)
                    return;

                Map map = initiator.Map;

                // Only compute danger/threat state on player home maps.
                bool isInDanger = false;

                if (map.IsPlayerHome)
                {
                    // Recompute isInDanger in the same way RimTalk's TalkService does:
                    // using GetPawnStatusFull on nearby pawns.
                    List<Pawn> nearbyPawns = PawnSelector.GetAllNearByPawns(initiator);
                    if (talkRequest.Recipient != null && talkRequest.Recipient.IsPlayer())
                    {
                        nearbyPawns.Insert(0, talkRequest.Recipient);
                    }

                    try
                    {
                        // GetPawnStatusFull returns (string statusText, bool isInDanger)
                        var statusResult = initiator.GetPawnStatusFull(nearbyPawns);
                        isInDanger = statusResult.Item2;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk Event+] Failed to recompute danger flag: {ex}");
                    }
                }

                var ongoingEvents = OngoingEventsUtil.GetOngoingEventsNow(
                    map,
                    isInDanger,
                    maxEvents: 5,
                    maxThreatScanBack: 30
                );

                if (ongoingEvents == null || ongoingEvents.Count == 0)
                    return;

                string block = OngoingEventsFormatter.FormatOngoingEventsBlock(
                    ongoingEvents,
                    maxChars: 1200
                );

                if (block.NullOrEmpty())
                    return;

                if (talkRequest.Prompt.NullOrEmpty())
                {
                    talkRequest.Prompt = block;
                }
                else
                {
                    talkRequest.Prompt += "\n\n" + block;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Event+] Error while appending ongoing events: {ex}");
            }
        }

    }

    /// Manual patcher: attaches our postfix to PromptService.DecoratePrompt at startup,
    /// avoiding early static initialization issues with RimTalk.Data.Constant.
    [StaticConstructorOnStartup]
    public static class PromptService_OngoingEventsPatcher
    {
        static PromptService_OngoingEventsPatcher()
        {
            try
            {
                var harmony = new Harmony("saltgin.rimtalkeventmemory.prompt");

                var promptServiceType = AccessTools.TypeByName("RimTalk.Service.PromptService");
                var talkRequestType = AccessTools.TypeByName("RimTalk.Data.TalkRequest");

                if (promptServiceType == null || talkRequestType == null)
                {
                    Log.Warning("[RimTalk Event+] Could not find RimTalk types (PromptService / TalkRequest); skipping prompt patch.");
                    return;
                }

                var method = AccessTools.Method(
                    promptServiceType,
                    "DecoratePrompt",
                    new[] { talkRequestType, typeof(List<Pawn>), typeof(string) }
                );

                if (method == null)
                {
                    Log.Warning("[RimTalk Event+] Could not find PromptService.DecoratePrompt; skipping prompt patch.");
                    return;
                }

                var postfix = AccessTools.Method(typeof(PromptService_OngoingEventsPatch), "Postfix");
                if (postfix == null)
                {
                    Log.Warning("[RimTalk Event+] Could not find Postfix method; skipping prompt patch.");
                    return;
                }

                harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                Log.Message("[RimTalk Event+] Patched RimTalk.Service.PromptService.DecoratePrompt successfully.");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimTalk Event+] Failed to patch PromptService.DecoratePrompt: {ex}");
            }
        }
    }
}
