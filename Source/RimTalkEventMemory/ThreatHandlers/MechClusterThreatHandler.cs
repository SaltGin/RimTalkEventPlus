using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    // Mech clusters share the generic Lord-backed record, but their native
    // defend toil has a clear player-facing meaning that the generic fallback
    // cannot infer.
    internal static class MechClusterThreatHandler
    {
        private const string MechClusterIncidentDefName = "MechCluster";

        public static bool Matches(GenericLordThreatRecord record)
        {
            return record?.core?.incidentDefName == MechClusterIncidentDefName;
        }

        public static OngoingEventSnapshot BuildSnapshot(GenericLordThreatRecord record, Map map)
        {
            return GenericLordThreatHandler.BuildSnapshotCore(record, map, DescribePhase);
        }

        private static string DescribePhase(ThreatRecordCore core)
        {
            return ThreatUtility.DescribePhaseAcrossLords(core, DescribeMechClusterLordPhase);
        }

        private static string DescribeMechClusterLordPhase(Lord lord)
        {
            LordToil toil = lord?.CurLordToil;
            if (toil is LordToil_Sleep)
                return "dormant";
            if (toil is LordToil_DefendPoint)
                return "defending the cluster";
            if (toil is LordToil_AssaultColony)
                return "assaulting";

            return GenericLordThreatHandler.DescribeDefaultLordPhase(lord);
        }
    }
}
