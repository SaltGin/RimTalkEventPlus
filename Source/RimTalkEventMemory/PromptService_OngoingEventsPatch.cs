using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.Util;
using RimWorld;
using Verse;

namespace RimTalkEventPlus
{
    // Postfix that appends ongoing events to RimTalk's context.
    // NOTE: patched manually by PromptService_OngoingEventsPatcher.
    public static class PromptService_OngoingEventsPatch
    {
        // Cached property accessor - resolved once at startup
        private static PropertyInfo _contextProperty;
        private static bool _contextPropertyResolved;

        internal static void ResolveContextProperty()
        {
            if (_contextPropertyResolved)
                return;

            _contextPropertyResolved = true;

            try
            {
                var talkRequestType = AccessTools.TypeByName("RimTalk.Data.TalkRequest");
                if (talkRequestType != null)
                {
                    _contextProperty = AccessTools.Property(talkRequestType, "Context");

                    if (_contextProperty != null && Prefs.DevMode)
                    {
                        Log.Message("[RimTalk Event+] Found TalkRequest.Context property - using Context injection.");
                    }
                    else if (Prefs.DevMode)
                    {
                        Log.Message("[RimTalk Event+] TalkRequest.Context not found - falling back to Prompt injection.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Event+] Failed to resolve TalkRequest.Context: {ex.Message}");
            }
        }

        // Must match:  void DecoratePrompt(TalkRequest talkRequest, List<Pawn> pawns, string status)
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
                bool isInDanger = map.IsPlayerHome &&
                    map.dangerWatcher?.DangerRating != StoryDanger.None;

                var ongoingEvents = OngoingEventsUtil.GetOngoingEventsNow(
                    map,
                    isInDanger,
                    maxEvents: 5,
                    maxThreatScanBack: 30
                );

                if (ongoingEvents == null || ongoingEvents.Count == 0)
                    return;

                // Apply context filtering if enabled
                var settings = RimTalkEventPlus.Settings;
                if (settings != null && settings.EnableContextFiltering)
                {
                    var contextPawnIds = ContextPawnMatcher.CollectContextPawnIds(
                        pawns,
                        initiator,
                        talkRequest.Recipient);

                    ongoingEvents = ContextPawnMatcher.FilterEventsByContext(
                        ongoingEvents,
                        contextPawnIds);

                    if (ongoingEvents == null || ongoingEvents.Count == 0)
                        return;
                }

                string block = OngoingEventsFormatter.FormatOngoingEventsBlock(
                    ongoingEvents,
                    maxChars: 1200
                );

                if (block.NullOrEmpty())
                    return;

                // Try to inject to Context (new RimTalk), fall back to Prompt (old RimTalk)
                if (_contextProperty != null)
                {
                    // New RimTalk:  inject to Context (System Instruction)
                    string currentContext = _contextProperty.GetValue(talkRequest) as string ?? string.Empty;

                    if (currentContext.NullOrEmpty())
                    {
                        _contextProperty.SetValue(talkRequest, block);
                    }
                    else
                    {
                        _contextProperty.SetValue(talkRequest, currentContext + "\n\n" + block);
                    }
                }
                else
                {
                    // Old RimTalk: fall back to Prompt (User Message)
                    if (talkRequest.Prompt.NullOrEmpty())
                    {
                        talkRequest.Prompt = block;
                    }
                    else
                    {
                        talkRequest.Prompt += "\n\n" + block;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Event+] Error while appending ongoing events: {ex}");
            }
        }
    }

    // Manual patcher: attaches our postfix to PromptService.DecoratePrompt at startup,
    // avoiding early static initialization issues with RimTalk.Data.Constant.
    [StaticConstructorOnStartup]
    public static class PromptService_OngoingEventsPatcher
    {
        static PromptService_OngoingEventsPatcher()
        {
            try
            {
                // Resolve Context property first
                PromptService_OngoingEventsPatch.ResolveContextProperty();

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