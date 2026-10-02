using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    internal static class SightstealerThreatHandler
    {
        private const string FallbackIncidentName = "SightstealerThreat";

        public static SightstealerThreatRecord TryCreate(ThreatCaptureContext capture)
        {
            if (capture == null)
                return null;

            List<Lord> lords = ThreatUtility.CollectSightstealerLords(capture.CapturedLords);
            if (lords.Count == 0)
                return null;

            var record = new SightstealerThreatRecord
            {
                core = ThreatUtility.CreateCore(capture, lords, FallbackIncidentName, "Sightstealer threat"),
                disclosureObserved = false
            };

            // The swarm's initial Distant shriek is intentionally retained as
            // the player-facing wording instead of being converted to data.
            Letter warning = ThreatUtility.FindTargetedLetter(capture.CapturedLetters, record.core);
            if (warning != null)
                record.warningLetterId = warning.ID;

            ThreatUtility.AttachTargetedLetters(record.core, capture.CapturedLetters);
            return record;
        }

        public static SightstealerThreatRecord CreateUnscoped(Lord lord)
        {
            if (!ThreatUtility.IsRecordableLordThreat(lord) || !ThreatUtility.IsSightstealerLord(lord))
                return null;

            var lords = new List<Lord> { lord };
            var record = new SightstealerThreatRecord
            {
                core = ThreatUtility.CreateCore(null, lords, FallbackIncidentName, "Sightstealer threat")
            };

            // This one-time live check is used only when adopting a group that
            // existed before this tracker saw its incident. It never polls on
            // prompt construction and never exposes a hidden group.
            record.disclosureObserved = ThreatUtility.HasPlayerVisibleSightstealer(record.core, lord.Map);
            return record;
        }

        public static OngoingEventSnapshot BuildSnapshot(SightstealerThreatRecord record, Map map)
        {
            if (record == null || !ThreatUtility.CoreHasLordOnMap(record.core, map))
                return null;

            if (!record.disclosureObserved)
            {
                Letter warning = ThreatUtility.FindArchivedLetter(record.warningLetterId);
                if (warning != null)
                {
                    return ThreatUtility.CreateLetterSnapshot(warning);
                }

                // This handler owns the instance even when there is nothing
                // safe to append. Generic fallback must not reveal it.
                return null;
            }

            return new OngoingEventSnapshot
            {
                Category = EventCategory.Threat,
                SourceDefName = record.core.incidentDefName,
                Label = "Threat - Sightstealers",
                Body = "Sightstealers have been detected and remain active.",
                IsThreat = true
            };
        }

        public static bool OwnsLetter(SightstealerThreatRecord record, int letterId)
        {
            return record != null && letterId > 0 &&
                (record.warningLetterId == letterId || record.core.OwnsLetter(letterId));
        }
    }

    // CompSightstealer uses this path when ordinary detection or attack makes
    // the pawn visible. Notify_ForcedVisible is intentionally not treated as
    // equivalent: the game does not send the Sightstealer reveal letter there.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Notify_BecameVisible))]
    internal static class Pawn_NotifyBecameVisible_SightstealerThreatPatch
    {
        private static void Postfix(Pawn __instance)
        {
            if (ThreatUtility.IsSightstealer(__instance))
                Current.Game?.GetComponent<ThreatTrackerComponent>()?.NotifySightstealerBecameVisible(__instance);
        }
    }
}
