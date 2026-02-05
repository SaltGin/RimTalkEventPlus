using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimTalkEventPlus
{
    [StaticConstructorOnStartup]
    public static class RimTalkAPIIntegration
    {
        private const string MOD_ID = "rimtalkeventplus";

        private static readonly MethodInfo _registerContextVariableMethod;
        private static readonly PropertyInfo _contextMapProperty;
        private static readonly bool _apiAvailable;

        static RimTalkAPIIntegration()
        {
            try
            {
                var apiType = AccessTools.TypeByName("RimTalk.API.RimTalkPromptAPI");
                if (apiType == null)
                {
                    Log.Message("[RimTalk Event+] RimTalkPromptAPI not found.");
                    return;
                }

                var promptContextType = AccessTools.TypeByName("RimTalk.Prompt.PromptContext");
                if (promptContextType == null)
                {
                    Log.Warning("[RimTalk Event+] PromptContext type not found.");
                    return;
                }

                _contextMapProperty = promptContextType.GetProperty("Map");
                var funcType = typeof(Func<,>).MakeGenericType(promptContextType, typeof(string));

                _registerContextVariableMethod = AccessTools.Method(
                    apiType,
                    "RegisterContextVariable",
                    new[] { typeof(string), typeof(string), funcType, typeof(string), typeof(int) }
                );

                if (_registerContextVariableMethod == null)
                {
                    Log.Warning("[RimTalk Event+] RegisterContextVariable method not found.");
                    return;
                }

                _apiAvailable = true;
                RegisterVariables();
                Log.Message("[RimTalk Event+] Advanced Mode API integration successful.");
            }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk Event+] Failed to integrate with RimTalk API: {ex.Message}");
            }
        }

        private static Map GetMap(object ctx) => _contextMapProperty?.GetValue(ctx) as Map;

        private static void RegisterVariables()
        {
            Register("eventplus_all",
                "All enabled ongoing events combined",
                ctx =>
                {
                    var map = GetMap(ctx);
                    if (map == null) return string.Empty;
                    bool isInDanger = map.IsPlayerHome && map.dangerWatcher?.DangerRating != StoryDanger.None;
                    return Format(OngoingEventsUtil.GetOngoingEventsNow(map, isInDanger));
                });

            Register("eventplus_quests",
                "Active quests on current map",
                ctx =>
                {
                    var map = GetMap(ctx);
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    OngoingEventsUtil.TryAddOngoingQuestsForMap(map, result, 5);
                    return Format(result);
                });

            Register("eventplus_conditions",
                "Active game conditions on current map",
                ctx =>
                {
                    var map = GetMap(ctx);
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    OngoingEventsUtil.TryAddActiveGameConditionsForMap(map, result, 5);
                    return Format(result);
                });

            Register("eventplus_threats",
                "Ongoing threats on current map",
                ctx =>
                {
                    var map = GetMap(ctx);
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    bool isInDanger = map.IsPlayerHome && map.dangerWatcher?.DangerRating != StoryDanger.None;
                    if (isInDanger)
                        OngoingEventsUtil.TryAddMostRecentThreatLetter(result, 1, 30);
                    return Format(result);
                });

            Register("eventplus_location",
                "Current location description",
                ctx =>
                {
                    var map = GetMap(ctx);
                    if (map == null) return string.Empty;
                    var result = new List<OngoingEventSnapshot>();
                    if (!map.IsPlayerHome)
                        OngoingEventsUtil.TryAddSitePartEvents(map, result, 3);
                    return Format(result);
                });
        }

        private static void Register(string name, string description, Func<object, string> provider)
        {
            _registerContextVariableMethod.Invoke(null, new object[] {
                MOD_ID, name, provider, description, 100
            });
        }

        private static string Format(List<OngoingEventSnapshot> events)
        {
            if (events == null || events.Count == 0)
                return string.Empty;
            return OngoingEventsFormatter.FormatOngoingEventsBlock(events, maxChars: 1500, includeWrapper: false);
        }

        public static bool IsAPIAvailable => _apiAvailable;
    }
}