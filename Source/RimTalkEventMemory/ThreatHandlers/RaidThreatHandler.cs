using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    internal static class RaidThreatHandler
    {
        public static bool Matches(ThreatCaptureContext capture)
        {
            return capture != null && capture.worker is IncidentWorker_Raid;
        }

        public static RaidThreatRecord Create(ThreatCaptureContext capture)
        {
            if (!Matches(capture))
                return null;

            List<Lord> lords = ThreatUtility.CollectRecordableLords(capture.CapturedLords, capture.parms?.faction);
            if (lords.Count == 0)
                return null;

            Faction faction = capture.parms?.faction ?? ThreatUtility.GetThreatFaction(lords[0]);
            var record = new RaidThreatRecord
            {
                core = ThreatUtility.CreateCore(capture, lords, "Raid", "Raid"),
                factionName = ThreatUtility.GetFactionName(faction, lords),
                factionDefName = faction?.def?.defName,
                strategyLabel = ThreatUtility.GetDefLabel(capture.parms?.raidStrategy),
                arrivalDescription = ThreatUtility.BuildArrivalDescription(capture.parms),
                origin = ThreatUtility.BuildOrigin(capture.parms),
                initialForceSize = ThreatUtility.CountOwnedPawns(lords),
                initialGroupCount = lords.Count
            };

            // A raid's standard letter is replaced by its richer snapshot.
            ThreatUtility.AttachTargetedLetters(record.core, capture.CapturedLetters);
            return record;
        }

        public static OngoingEventSnapshot BuildSnapshot(RaidThreatRecord record, Map map)
        {
            if (record == null || !ThreatUtility.CoreHasLordOnMap(record.core, map))
                return null;

            if (!ThreatUtility.IsSafeToDescribe(record.core, map))
            {
                Letter originalLetter = ThreatUtility.FindOwnedArchivedLetter(record.core);
                if (originalLetter != null)
                {
                    return ThreatUtility.CreateLetterSnapshot(originalLetter);
                }

                return null;
            }

            string factionName = string.IsNullOrEmpty(record.factionName) ? "unknown faction" : record.factionName;
            var body = new StringBuilder();

            if (!string.IsNullOrEmpty(record.strategyLabel))
                ThreatUtility.AppendLine(body, "Raid type: " + record.strategyLabel + ".");
            else if (!string.IsNullOrEmpty(record.core.incidentLabel))
                ThreatUtility.AppendLine(body, "Raid type: " + record.core.incidentLabel + ".");

            if (!string.IsNullOrEmpty(record.arrivalDescription))
                ThreatUtility.AppendLine(body, "Arrival: " + record.arrivalDescription + ".");

            AppendInitialForce(body, record);

            // Storyteller incidents deliberately produce no origin. A named
            // quest is the only source context that reduces LLM ambiguity.
            if (!string.IsNullOrEmpty(record.origin))
                ThreatUtility.AppendLine(body, "Origin: " + record.origin + ".");

            ThreatUtility.AppendLiveState(body, record.core, "raider", DescribePhase(record.core));

            return new OngoingEventSnapshot
            {
                Category = EventCategory.Threat,
                SourceDefName = record.core.incidentDefName,
                Label = "Raid - " + factionName,
                Body = body.ToString(),
                IsThreat = true
            };
        }

        private static void AppendInitialForce(StringBuilder body, RaidThreatRecord record)
        {
            if (record.initialForceSize <= 0)
                return;

            var line = new StringBuilder();
            line.Append("Initial force: ").Append(record.initialForceSize).Append(" raider");
            if (record.initialForceSize != 1)
                line.Append('s');
            if (record.initialGroupCount > 0)
            {
                line.Append(" in ").Append(record.initialGroupCount).Append(" group");
                if (record.initialGroupCount != 1)
                    line.Append('s');
            }
            line.Append('.');
            ThreatUtility.AppendLine(body, line.ToString());
        }

        private static string DescribePhase(ThreatRecordCore core)
        {
            return ThreatUtility.DescribePhaseAcrossLords(
                core,
                GenericLordThreatHandler.DescribeDefaultLordPhase);
        }
    }
}
