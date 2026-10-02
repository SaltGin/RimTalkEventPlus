using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    // The sole persisted state host. It owns lifecycle and indexing only;
    // matching and disclosure remain in the dedicated handlers.
    public class ThreatTrackerComponent : GameComponent
    {
        private const int ArchiveBindingIntervalTicks = 2500;

        private List<RaidThreatRecord> raidThreats = new List<RaidThreatRecord>();
        private List<SightstealerThreatRecord> sightstealerThreats = new List<SightstealerThreatRecord>();
        private List<GenericLordThreatRecord> genericLordThreats = new List<GenericLordThreatRecord>();
        private int nextArchiveBindingTick;
        private int lastPruneTick = -1;

        public ThreatTrackerComponent(Game game) : base()
        {
        }

        public override void ExposeData()
        {
            EnsureCollections();
            Scribe_Collections.Look(ref raidThreats, "raidThreats", LookMode.Deep);
            Scribe_Collections.Look(ref sightstealerThreats, "sightstealerThreats", LookMode.Deep);
            Scribe_Collections.Look(ref genericLordThreats, "genericLordThreats", LookMode.Deep);
            EnsureCollections();
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            raidThreats.Clear();
            sightstealerThreats.Clear();
            genericLordThreats.Clear();
            nextArchiveBindingTick = 0;
            lastPruneTick = -1;
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            ReconcileLoadedGame();
        }

        internal void RegisterCapturedThreat(ThreatCaptureContext capture)
        {
            if (capture == null)
                return;

            EnsureCollections();
            PruneInvalidRecords();
            RouteCapturedThreat(capture);
        }

        internal void RegisterUnscopedLord(Lord lord)
        {
            if (!ThreatUtility.IsRecordableLordThreat(lord))
                return;

            EnsureCollections();
            PruneInvalidRecords();
            if (IsLordTracked(lord))
                return;

            RouteUnscopedLord(lord);
        }

        internal void NotifySightstealerBecameVisible(Pawn pawn)
        {
            if (!ThreatUtility.IsSightstealer(pawn) || pawn.lord == null)
                return;

            EnsureCollections();
            PruneInvalidRecords();
            SightstealerThreatRecord record = FindSightstealerRecord(pawn.lord);
            if (record == null)
            {
                RegisterUnscopedLord(pawn.lord);
                record = FindSightstealerRecord(pawn.lord);
            }

            if (record != null)
                record.disclosureObserved = true;
        }

        // Called after RimWorld has removed the Lord, so the record can end
        // immediately instead of relying on prompt-time cleanup.
        internal void NotifyLordRemoved(Lord lord)
        {
            if (lord == null)
                return;

            EnsureCollections();
            RemoveLord(raidThreats, lord);
            RemoveLord(sightstealerThreats, lord);
            RemoveLord(genericLordThreats, lord);
        }

        public List<OngoingEventSnapshot> GetPromptSnapshotsForMap(Map map, int maxToAdd)
        {
            var result = new List<OngoingEventSnapshot>();
            if (map == null || maxToAdd <= 0)
                return result;

            EnsureCollections();
            PruneInvalidRecords();
            BindArchiveLettersIfDue(force: false);

            for (int i = 0; i < sightstealerThreats.Count && result.Count < maxToAdd; i++)
                AddSnapshot(result, SightstealerThreatHandler.BuildSnapshot(sightstealerThreats[i], map), maxToAdd);

            for (int i = 0; i < raidThreats.Count && result.Count < maxToAdd; i++)
                AddSnapshot(result, RaidThreatHandler.BuildSnapshot(raidThreats[i], map), maxToAdd);

            for (int i = 0; i < genericLordThreats.Count && result.Count < maxToAdd; i++)
            {
                GenericLordThreatRecord record = genericLordThreats[i];
                OngoingEventSnapshot snapshot = MechClusterThreatHandler.Matches(record)
                    ? MechClusterThreatHandler.BuildSnapshot(record, map)
                    : GenericLordThreatHandler.BuildSnapshot(record, map);
                AddSnapshot(result, snapshot, maxToAdd);
            }

            return result;
        }

        // The generic archive path is deliberately the final fallback. A
        // specialized record can own or suppress its letters without allowing
        // the generic path to reveal the same threat differently.
        public bool TryGetGenericLetterSnapshot(Letter letter, Map map, out OngoingEventSnapshot snapshot)
        {
            snapshot = null;
            if (!ThreatUtility.IsThreatLetter(letter) || map == null)
                return false;

            EnsureCollections();
            PruneInvalidRecords();
            if (OwnsLetter(letter.ID) || LetterTargetsTrackedThreat(letter))
                return false;

            if (!ThreatUtility.HasLiveThreatTarget(letter, map))
                return false;

            snapshot = ThreatUtility.CreateLetterSnapshot(letter);
            return snapshot != null;
        }

        // Ordered routing lives with the state owner. Future special handlers
        // add one explicit branch here without requiring a framework layer.
        private void RouteCapturedThreat(ThreatCaptureContext capture)
        {
            SightstealerThreatRecord sightstealers = SightstealerThreatHandler.TryCreate(capture);
            if (sightstealers != null)
            {
                AddRecord(sightstealerThreats, sightstealers);
                return;
            }

            if (RaidThreatHandler.Matches(capture))
            {
                RaidThreatRecord raid = RaidThreatHandler.Create(capture);
                if (raid != null)
                    AddRecord(raidThreats, raid);
                return;
            }

            GenericLordThreatRecord generic = GenericLordThreatHandler.Create(capture);
            if (generic != null)
                AddRecord(genericLordThreats, generic);
        }

        private void RouteUnscopedLord(Lord lord)
        {
            if (ThreatUtility.IsSightstealerLord(lord))
            {
                SightstealerThreatRecord sightstealers = SightstealerThreatHandler.CreateUnscoped(lord);
                if (sightstealers != null)
                    AddRecord(sightstealerThreats, sightstealers);
                return;
            }

            GenericLordThreatRecord generic = GenericLordThreatHandler.CreateUnscoped(lord);
            if (generic != null)
                AddRecord(genericLordThreats, generic);
        }

        private void AddRecord<T>(List<T> records, T record)
            where T : TrackedThreatRecord
        {
            if (PrepareForAdd(record))
                records.Add(record);
        }

        private void ReconcileLoadedGame()
        {
            EnsureCollections();
            PruneInvalidRecords(force: true);
            DiscoverUntrackedLords();
            BindArchiveLettersIfDue(force: true);
        }

        // One bounded scan on load adopts Lords created while this mod was not
        // observing their incident. Specialized handlers still get first say.
        private void DiscoverUntrackedLords()
        {
            if (Find.Maps == null)
                return;

            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                List<Lord> lords = Find.Maps[mapIndex]?.lordManager?.lords;
                if (lords == null)
                    continue;

                for (int lordIndex = 0; lordIndex < lords.Count; lordIndex++)
                {
                    Lord lord = lords[lordIndex];
                    if (ThreatUtility.IsRecordableLordThreat(lord) && !IsLordTracked(lord))
                        RouteUnscopedLord(lord);
                }
            }
        }

        private void BindArchiveLettersIfDue(bool force)
        {
            int now = ThreatUtility.CurrentTick;
            if (!force && now < nextArchiveBindingTick)
                return;

            List<IArchivable> archive = Find.Archive?.ArchivablesListForReading;
            if (archive == null)
                return;

            for (int archiveIndex = 0; archiveIndex < archive.Count; archiveIndex++)
            {
                Letter letter = archive[archiveIndex] as Letter;
                if (!ThreatUtility.IsThreatLetter(letter))
                    continue;

                BindLetterToSightstealers(letter);
                BindLetterToRecords(letter, raidThreats);
                BindLetterToRecords(letter, genericLordThreats);
            }

            nextArchiveBindingTick = now + ArchiveBindingIntervalTicks;
        }

        private void BindLetterToSightstealers(Letter letter)
        {
            for (int i = 0; i < sightstealerThreats.Count; i++)
            {
                SightstealerThreatRecord record = sightstealerThreats[i];
                if (record == null || !ThreatUtility.LetterTargetsCore(letter, record.core))
                    continue;

                // Only the known swarm incident can reinterpret a bound letter
                // as the initial Distant shriek. Other letters remain owned and
                // are replaced by the handler's safe result.
                if (record.warningLetterId == 0 &&
                    record.core.incidentDefName == "SightstealerSwarm")
                {
                    record.warningLetterId = letter.ID;
                }

                record.core.OwnLetter(letter.ID);
                return;
            }
        }

        private static void BindLetterToRecords<T>(Letter letter, List<T> records)
            where T : TrackedThreatRecord
        {
            for (int i = 0; i < records.Count; i++)
            {
                T record = records[i];
                if (record != null && ThreatUtility.LetterTargetsCore(letter, record.core))
                {
                    record.core.OwnLetter(letter.ID);
                    return;
                }
            }
        }

        private void PruneInvalidRecords(bool force = false)
        {
            int now = ThreatUtility.CurrentTick;
            if (!force && lastPruneTick == now)
                return;

            PruneRecords(raidThreats);
            PruneRecords(sightstealerThreats);
            PruneRecords(genericLordThreats);
            lastPruneTick = now;
        }

        private static void PruneRecords<T>(List<T> records)
            where T : TrackedThreatRecord
        {
            for (int recordIndex = records.Count - 1; recordIndex >= 0; recordIndex--)
            {
                T record = records[recordIndex];
                if (record == null || record.core == null)
                {
                    records.RemoveAt(recordIndex);
                    continue;
                }

                record.core.EnsureCollections();
                for (int lordIndex = record.core.lords.Count - 1; lordIndex >= 0; lordIndex--)
                {
                    if (!ThreatUtility.IsActiveLord(record.core.lords[lordIndex]))
                        record.core.lords.RemoveAt(lordIndex);
                }

                if (record.core.lords.Count == 0)
                    records.RemoveAt(recordIndex);
            }
        }

        private static void RemoveLord<T>(List<T> records, Lord lord)
            where T : TrackedThreatRecord
        {
            for (int recordIndex = records.Count - 1; recordIndex >= 0; recordIndex--)
            {
                T record = records[recordIndex];
                if (record?.core?.lords == null)
                {
                    records.RemoveAt(recordIndex);
                    continue;
                }

                record.core.lords.Remove(lord);
                if (record.core.lords.Count == 0)
                    records.RemoveAt(recordIndex);
            }
        }

        private bool PrepareForAdd(TrackedThreatRecord record)
        {
            if (record?.core == null)
                return false;

            record.core.EnsureCollections();
            for (int i = record.core.lords.Count - 1; i >= 0; i--)
            {
                Lord lord = record.core.lords[i];
                if (!ThreatUtility.IsActiveLord(lord) || IsLordTracked(lord))
                    record.core.lords.RemoveAt(i);
            }

            return record.core.lords.Count > 0;
        }

        private bool IsLordTracked(Lord lord)
        {
            return FindRecord(raidThreats, lord) != null ||
                   FindRecord(sightstealerThreats, lord) != null ||
                   FindRecord(genericLordThreats, lord) != null;
        }

        private SightstealerThreatRecord FindSightstealerRecord(Lord lord)
        {
            return FindRecord(sightstealerThreats, lord);
        }

        private static T FindRecord<T>(List<T> records, Lord lord)
            where T : TrackedThreatRecord
        {
            if (lord == null)
                return null;

            for (int i = 0; i < records.Count; i++)
            {
                T record = records[i];
                if (record?.core?.lords != null && record.core.lords.Contains(lord))
                    return record;
            }

            return null;
        }

        private bool OwnsLetter(int letterId)
        {
            if (letterId <= 0)
                return false;

            for (int i = 0; i < sightstealerThreats.Count; i++)
            {
                if (SightstealerThreatHandler.OwnsLetter(sightstealerThreats[i], letterId))
                    return true;
            }

            return ListOwnsLetter(raidThreats, letterId) || ListOwnsLetter(genericLordThreats, letterId);
        }

        private static bool ListOwnsLetter<T>(List<T> records, int letterId)
            where T : TrackedThreatRecord
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i]?.core != null && records[i].core.OwnsLetter(letterId))
                    return true;
            }

            return false;
        }

        private bool LetterTargetsTrackedThreat(Letter letter)
        {
            return LetterTargetsRecords(letter, sightstealerThreats) ||
                   LetterTargetsRecords(letter, raidThreats) ||
                   LetterTargetsRecords(letter, genericLordThreats);
        }

        private static bool LetterTargetsRecords<T>(Letter letter, List<T> records)
            where T : TrackedThreatRecord
        {
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i] != null && ThreatUtility.LetterTargetsCore(letter, records[i].core))
                    return true;
            }

            return false;
        }

        private static void AddSnapshot(List<OngoingEventSnapshot> destination, OngoingEventSnapshot snapshot, int maxToAdd)
        {
            if (destination.Count < maxToAdd && snapshot != null)
                destination.Add(snapshot);
        }

        private void EnsureCollections()
        {
            if (raidThreats == null)
                raidThreats = new List<RaidThreatRecord>();
            if (sightstealerThreats == null)
                sightstealerThreats = new List<SightstealerThreatRecord>();
            if (genericLordThreats == null)
                genericLordThreats = new List<GenericLordThreatRecord>();
        }
    }
}
