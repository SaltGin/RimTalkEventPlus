using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    internal static class GenericLordThreatHandler
    {
        private const string FallbackIncidentName = "GenericLordThreat";

        public static GenericLordThreatRecord Create(ThreatCaptureContext capture)
        {
            if (capture == null)
                return null;

            List<Lord> lords = ThreatUtility.CollectRecordableLords(capture.CapturedLords);
            if (lords.Count == 0)
                return null;

            Faction faction = ThreatUtility.GetThreatFaction(lords[0]);
            var record = new GenericLordThreatRecord
            {
                core = ThreatUtility.CreateCore(capture, lords, FallbackIncidentName, "Hostile group"),
                factionName = ThreatUtility.GetFactionName(faction, lords),
                factionDefName = faction?.def?.defName,
                initialForceSize = ThreatUtility.CountOwnedPawns(lords),
                initialGroupCount = lords.Count
            };

            ThreatUtility.AttachTargetedLetters(record.core, capture.CapturedLetters);
            return record;
        }

        public static GenericLordThreatRecord CreateUnscoped(Lord lord)
        {
            if (!ThreatUtility.IsRecordableLordThreat(lord))
                return null;

            var lords = new List<Lord> { lord };
            Faction faction = ThreatUtility.GetThreatFaction(lord);
            return new GenericLordThreatRecord
            {
                core = ThreatUtility.CreateCore(null, lords, FallbackIncidentName, "Hostile group"),
                factionName = ThreatUtility.GetFactionName(faction, lords),
                factionDefName = faction?.def?.defName,
                initialForceSize = ThreatUtility.CountOwnedPawns(lords),
                initialGroupCount = 1
            };
        }

        public static OngoingEventSnapshot BuildSnapshot(GenericLordThreatRecord record, Map map)
        {
            return BuildSnapshotCore(record, map, DescribePhase);
        }

        // Specialized generic-Lord handlers can reuse the common safety,
        // letter-ownership, and force formatting while supplying their own
        // live phase vocabulary.
        internal static OngoingEventSnapshot BuildSnapshotCore(
            GenericLordThreatRecord record,
            Map map,
            Func<ThreatRecordCore, string> describePhase)
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
            if (!string.IsNullOrEmpty(record.core.incidentLabel))
                ThreatUtility.AppendLine(body, "Threat type: " + record.core.incidentLabel + ".");

            if (record.initialForceSize > 0)
            {
                var initial = new StringBuilder();
                initial.Append("Initial force: ").Append(record.initialForceSize).Append(" hostile");
                if (record.initialForceSize != 1)
                    initial.Append('s');
                initial.Append('.');
                ThreatUtility.AppendLine(body, initial.ToString());
            }

            string phase = describePhase != null ? describePhase(record.core) : DescribePhase(record.core);
            ThreatUtility.AppendLiveState(body, record.core, "hostile", phase);

            return new OngoingEventSnapshot
            {
                Category = EventCategory.Threat,
                SourceDefName = record.core.incidentDefName,
                Label = "Threat - " + factionName,
                Body = body.ToString(),
                IsThreat = true
            };
        }

        private static string DescribePhase(ThreatRecordCore core)
        {
            return ThreatUtility.DescribePhaseAcrossLords(core, DescribeDefaultLordPhase);
        }

        // This is the conservative non-departure fallback for unknown or
        // modded hostile Lords. Departure is applied centrally before this
        // resolver is called.
        internal static string DescribeDefaultLordPhase(Lord lord)
        {
            string toilName = lord?.CurLordToil?.GetType().Name ?? string.Empty;
            if (toilName.IndexOf("Kidnap", StringComparison.OrdinalIgnoreCase) >= 0)
                return "attempting an abduction";
            if (toilName.IndexOf("Steal", StringComparison.OrdinalIgnoreCase) >= 0)
                return "looting";
            if (toilName.IndexOf("Travel", StringComparison.OrdinalIgnoreCase) >= 0)
                return "moving into position";
            if (toilName.IndexOf("Sleep", StringComparison.OrdinalIgnoreCase) >= 0)
                return "dormant";
            if (toilName.IndexOf("Siege", StringComparison.OrdinalIgnoreCase) >= 0)
                return "maintaining a siege";
            if (toilName.IndexOf("Sapper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                toilName.IndexOf("Breach", StringComparison.OrdinalIgnoreCase) >= 0)
                return "breaching defenses";
            if (toilName.IndexOf("Stage", StringComparison.OrdinalIgnoreCase) >= 0)
                return "staging";
            if (toilName.IndexOf("Assault", StringComparison.OrdinalIgnoreCase) >= 0)
                return "assaulting";

            string jobName = lord?.LordJob?.GetType().Name ?? string.Empty;
            if (toilName.IndexOf("PsychicRitual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                jobName.IndexOf("PsychicRitual", StringComparison.OrdinalIgnoreCase) >= 0)
                return "conducting a psychic ritual";
            if (jobName.IndexOf("Siege", StringComparison.OrdinalIgnoreCase) >= 0)
                return "maintaining a siege";
            if (jobName.IndexOf("Stage", StringComparison.OrdinalIgnoreCase) >= 0)
                return "staging";
            if (jobName.IndexOf("Assault", StringComparison.OrdinalIgnoreCase) >= 0)
                return "assaulting";
            return "active";
        }
    }
}
