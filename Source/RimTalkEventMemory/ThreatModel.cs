using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    // State shared by every tracked hostile group. Handler-specific information
    // intentionally stays on the concrete record rather than accumulating here.
    public class ThreatRecordCore : IExposable
    {
        public List<Lord> lords = new List<Lord>();
        public List<int> ownedLetterIds = new List<int>();
        public string incidentDefName;
        public string incidentLabel;
        public int createdTick;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref lords, "lords", LookMode.Reference);
            Scribe_Collections.Look(ref ownedLetterIds, "ownedLetterIds", LookMode.Value);
            Scribe_Values.Look(ref incidentDefName, "incidentDefName");
            Scribe_Values.Look(ref incidentLabel, "incidentLabel");
            Scribe_Values.Look(ref createdTick, "createdTick", 0);
            EnsureCollections();
        }

        public void EnsureCollections()
        {
            if (lords == null)
                lords = new List<Lord>();
            if (ownedLetterIds == null)
                ownedLetterIds = new List<int>();
        }

        public void OwnLetter(int letterId)
        {
            if (letterId > 0 && !ownedLetterIds.Contains(letterId))
                ownedLetterIds.Add(letterId);
        }

        public bool OwnsLetter(int letterId)
        {
            return letterId > 0 && ownedLetterIds != null && ownedLetterIds.Contains(letterId);
        }
    }

    public abstract class TrackedThreatRecord : IExposable
    {
        public ThreatRecordCore core = new ThreatRecordCore();

        public virtual void ExposeData()
        {
            Scribe_Deep.Look(ref core, "core");
            if (core == null)
                core = new ThreatRecordCore();
            core.EnsureCollections();
        }
    }

    public class RaidThreatRecord : TrackedThreatRecord
    {
        public string factionName;
        public string factionDefName;
        public string strategyLabel;
        public string arrivalDescription;
        public string origin;
        public int initialForceSize;
        public int initialGroupCount;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref factionName, "factionName");
            Scribe_Values.Look(ref factionDefName, "factionDefName");
            Scribe_Values.Look(ref strategyLabel, "strategyLabel");
            Scribe_Values.Look(ref arrivalDescription, "arrivalDescription");
            Scribe_Values.Look(ref origin, "origin");
            Scribe_Values.Look(ref initialForceSize, "initialForceSize", 0);
            Scribe_Values.Look(ref initialGroupCount, "initialGroupCount", 0);
        }
    }

    public class SightstealerThreatRecord : TrackedThreatRecord
    {
        // The exact Distant shriek letter bound while its IncidentWorker scope
        // is active. It is player-facing context, not structured disclosure.
        public int warningLetterId;

        // Set only by a normal visible event, or by a safe live-state recovery
        // after loading. Forced visibility has deliberately separate semantics.
        public bool disclosureObserved;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref warningLetterId, "warningLetterId", 0);
            Scribe_Values.Look(ref disclosureObserved, "disclosureObserved", false);
        }
    }

    public class GenericLordThreatRecord : TrackedThreatRecord
    {
        public string factionName;
        public string factionDefName;
        public int initialForceSize;
        public int initialGroupCount;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref factionName, "factionName");
            Scribe_Values.Look(ref factionDefName, "factionDefName");
            Scribe_Values.Look(ref initialForceSize, "initialForceSize", 0);
            Scribe_Values.Look(ref initialGroupCount, "initialGroupCount", 0);
        }
    }

}
