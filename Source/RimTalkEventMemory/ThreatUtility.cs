using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    // Departure is shared live state, not threat-specific presentation. Each
    // handler decides whether and how to render the resulting state.
    internal enum LordDepartureState
    {
        None,
        Fleeing,
        Withdrawing
    }

    internal static class ThreatUtility
    {
        public static int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        public static bool IsThreatLetter(Letter letter)
        {
            return letter != null &&
                (letter.def == LetterDefOf.ThreatBig || letter.def == LetterDefOf.ThreatSmall);
        }

        public static bool IsActiveLord(Lord lord)
        {
            if (lord?.lordManager?.map == null)
                return false;

            List<Lord> lords = lord.lordManager.lords;
            return lords != null && lords.Contains(lord);
        }

        public static bool IsRecordableLordThreat(Lord lord)
        {
            if (!IsActiveLord(lord) || !lord.AnyActivePawn)
                return false;

            if (lord.faction != null && lord.faction.HostileTo(Faction.OfPlayer))
                return true;

            return HasHostileOwnedPawn(lord);
        }

        public static List<Lord> CollectRecordableLords(List<Lord> candidates, Faction requiredFaction = null)
        {
            var result = new List<Lord>();
            if (candidates == null)
                return result;

            for (int i = 0; i < candidates.Count; i++)
            {
                Lord lord = candidates[i];
                if (!IsRecordableLordThreat(lord) || result.Contains(lord))
                    continue;
                if (requiredFaction != null && GetThreatFaction(lord) != requiredFaction)
                    continue;

                result.Add(lord);
            }

            return result;
        }

        public static bool IsSightstealer(Pawn pawn)
        {
            return pawn != null && pawn.kindDef == PawnKindDefOf.Sightstealer;
        }

        public static bool IsSightstealerLord(Lord lord)
        {
            if (lord == null)
                return false;

            if (lord.LordJob is LordJob_SightstealerAssault)
                return true;

            List<Pawn> pawns = lord.ownedPawns;
            if (pawns == null)
                return false;

            bool foundSightstealer = false;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                    continue;
                if (!IsSightstealer(pawn))
                    return false;

                foundSightstealer = true;
            }

            return foundSightstealer;
        }

        public static List<Lord> CollectSightstealerLords(List<Lord> candidates)
        {
            var result = new List<Lord>();
            if (candidates == null)
                return result;

            for (int i = 0; i < candidates.Count; i++)
            {
                Lord lord = candidates[i];
                if (IsRecordableLordThreat(lord) && IsSightstealerLord(lord) && !result.Contains(lord))
                    result.Add(lord);
            }

            return result;
        }

        public static Faction GetThreatFaction(Lord lord)
        {
            if (lord?.faction != null && lord.faction.HostileTo(Faction.OfPlayer))
                return lord.faction;

            List<Pawn> pawns = lord?.ownedPawns;
            if (pawns != null)
            {
                for (int i = 0; i < pawns.Count; i++)
                {
                    Pawn pawn = pawns[i];
                    if (pawn != null && pawn.Faction != null && pawn.HostileTo(Faction.OfPlayer))
                        return pawn.Faction;
                }
            }

            return lord?.faction;
        }

        public static string GetFactionName(Faction faction, List<Lord> lords = null)
        {
            if (faction != null && !string.IsNullOrEmpty(faction.Name))
                return faction.Name;

            if (lords != null)
            {
                for (int i = 0; i < lords.Count; i++)
                {
                    Faction fallback = GetThreatFaction(lords[i]);
                    if (fallback != null && !string.IsNullOrEmpty(fallback.Name))
                        return fallback.Name;
                }
            }

            return null;
        }

        public static ThreatRecordCore CreateCore(
            ThreatCaptureContext capture,
            List<Lord> lords,
            string fallbackDefName = null,
            string fallbackLabel = null)
        {
            return new ThreatRecordCore
            {
                lords = lords ?? new List<Lord>(),
                incidentDefName = capture?.incidentDefName ?? fallbackDefName,
                incidentLabel = capture?.incidentLabel ?? fallbackLabel,
                createdTick = CurrentTick
            };
        }

        public static void AttachTargetedLetters(ThreatRecordCore core, List<Letter> letters)
        {
            if (core == null || letters == null)
                return;

            for (int i = 0; i < letters.Count; i++)
            {
                Letter letter = letters[i];
                if (IsThreatLetter(letter) && LetterTargetsCore(letter, core))
                    core.OwnLetter(letter.ID);
            }
        }

        public static Letter FindTargetedLetter(List<Letter> letters, ThreatRecordCore core)
        {
            if (letters == null || core == null)
                return null;

            for (int i = 0; i < letters.Count; i++)
            {
                Letter letter = letters[i];
                if (IsThreatLetter(letter) && LetterTargetsCore(letter, core))
                    return letter;
            }

            return null;
        }

        public static bool LetterTargetsCore(Letter letter, ThreatRecordCore core)
        {
            if (letter?.lookTargets?.targets == null || core?.lords == null)
                return false;

            for (int targetIndex = 0; targetIndex < letter.lookTargets.targets.Count; targetIndex++)
            {
                Pawn pawn = letter.lookTargets.targets[targetIndex].Thing as Pawn;
                if (pawn == null)
                    continue;

                if (pawn.lord != null && core.lords.Contains(pawn.lord))
                    return true;

                for (int lordIndex = 0; lordIndex < core.lords.Count; lordIndex++)
                {
                    List<Pawn> ownedPawns = core.lords[lordIndex]?.ownedPawns;
                    if (ownedPawns != null && ownedPawns.Contains(pawn))
                        return true;
                }
            }

            return false;
        }

        public static Letter FindArchivedLetter(int letterId)
        {
            if (letterId <= 0)
                return null;

            List<IArchivable> archive = Find.Archive?.ArchivablesListForReading;
            if (archive == null)
                return null;

            for (int i = 0; i < archive.Count; i++)
            {
                Letter letter = archive[i] as Letter;
                if (letter != null && letter.ID == letterId)
                    return letter;
            }

            return null;
        }

        public static Letter FindOwnedArchivedLetter(ThreatRecordCore core)
        {
            if (core?.ownedLetterIds == null)
                return null;

            for (int i = 0; i < core.ownedLetterIds.Count; i++)
            {
                Letter letter = FindArchivedLetter(core.ownedLetterIds[i]);
                if (letter != null)
                    return letter;
            }

            return null;
        }

        public static OngoingEventSnapshot CreateLetterSnapshot(Letter letter)
        {
            if (letter == null)
                return null;

            IArchivable archivable = letter;
            string label;
            string body;
            try { label = archivable.ArchivedLabel ?? string.Empty; }
            catch { label = string.Empty; }
            try { body = archivable.ArchivedTooltip ?? string.Empty; }
            catch { body = string.Empty; }

            return new OngoingEventSnapshot
            {
                Category = EventCategory.Threat,
                SourceDefName = letter.def?.defName,
                Label = label,
                Body = body,
                IsThreat = true
            };
        }

        public static bool HasLiveThreatTarget(Letter letter, Map map)
        {
            if (letter?.lookTargets?.targets == null || letter.lookTargets.targets.Count == 0)
                return false;

            for (int i = 0; i < letter.lookTargets.targets.Count; i++)
            {
                Thing thing = letter.lookTargets.targets[i].Thing;
                IAttackTarget target = thing as IAttackTarget;
                if (thing == null || target == null || thing.Destroyed || !thing.Spawned || thing.Map != map)
                    continue;

                Pawn pawn = thing as Pawn;
                if (pawn != null && (pawn.IsPsychologicallyInvisible() || pawn.IsHiddenFromPlayer()))
                    continue;

                LordToil toil = pawn?.lord?.CurLordToil;
                if (toil is LordToil_PanicFlee || toil is LordToil_ExitMap ||
                    toil?.GetType().Name.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (GenHostility.IsActiveThreatToPlayer(target, canBeFogged: true))
                    return true;
            }

            return false;
        }

        public static bool CoreHasLordOnMap(ThreatRecordCore core, Map map)
        {
            if (core?.lords == null)
                return false;

            for (int i = 0; i < core.lords.Count; i++)
            {
                Lord lord = core.lords[i];
                if (IsActiveLord(lord) && lord.Map == map)
                    return true;
            }

            return false;
        }

        public static bool IsSafeToDescribe(ThreatRecordCore core, Map map)
        {
            if (core?.lords == null)
                return false;

            bool hasSpawnedMember = false;
            for (int lordIndex = 0; lordIndex < core.lords.Count; lordIndex++)
            {
                List<Pawn> pawns = core.lords[lordIndex]?.ownedPawns;
                if (pawns == null)
                    continue;

                for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                {
                    Pawn pawn = pawns[pawnIndex];
                    if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Map != map)
                        continue;

                    hasSpawnedMember = true;
                    if (pawn.IsPsychologicallyInvisible() || pawn.IsHiddenFromPlayer())
                        return false;
                }
            }

            return hasSpawnedMember;
        }

        public static bool HasPlayerVisibleSightstealer(ThreatRecordCore core, Map map)
        {
            if (core?.lords == null)
                return false;

            for (int lordIndex = 0; lordIndex < core.lords.Count; lordIndex++)
            {
                List<Pawn> pawns = core.lords[lordIndex]?.ownedPawns;
                if (pawns == null)
                    continue;

                for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                {
                    Pawn pawn = pawns[pawnIndex];
                    if (!IsSightstealer(pawn) || pawn.Dead || !pawn.Spawned || pawn.Map != map)
                        continue;

                    if (!pawn.IsPsychologicallyInvisible() && !pawn.IsHiddenFromPlayer())
                        return true;
                }
            }

            return false;
        }

        public static int CountOwnedPawns(List<Lord> lords)
        {
            int count = 0;
            if (lords == null)
                return count;

            for (int i = 0; i < lords.Count; i++)
                count += lords[i]?.ownedPawns?.Count ?? 0;
            return count;
        }

        public static int CountLivePawns(List<Lord> lords)
        {
            int count = 0;
            if (lords == null)
                return count;

            for (int lordIndex = 0; lordIndex < lords.Count; lordIndex++)
            {
                List<Pawn> pawns = lords[lordIndex]?.ownedPawns;
                if (pawns == null)
                    continue;

                for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                {
                    if (pawns[pawnIndex] != null && !pawns[pawnIndex].Dead)
                        count++;
                }
            }

            return count;
        }

        public static int CountActiveLords(List<Lord> lords)
        {
            int count = 0;
            if (lords == null)
                return count;

            for (int i = 0; i < lords.Count; i++)
            {
                if (IsActiveLord(lords[i]))
                    count++;
            }

            return count;
        }

        // Applies the shared departure rule first, then combines handler-owned
        // non-departure descriptions for each live Lord.
        public static string DescribePhaseAcrossLords(
            ThreatRecordCore core,
            Func<Lord, string> describeNonDeparturePhase)
        {
            var phases = new List<string>();
            if (core?.lords != null)
            {
                for (int i = 0; i < core.lords.Count; i++)
                {
                    Lord lord = core.lords[i];
                    if (!IsActiveLord(lord))
                        continue;

                    string phase = DescribeDepartureState(GetDepartureState(lord));
                    if (string.IsNullOrEmpty(phase) && describeNonDeparturePhase != null)
                        phase = describeNonDeparturePhase(lord);
                    if (!string.IsNullOrEmpty(phase) && !phases.Contains(phase))
                        phases.Add(phase);
                }
            }

            if (phases.Count == 0)
                return "unknown";
            if (phases.Count == 1)
                return phases[0];
            return "mixed (" + string.Join(", ", phases.ToArray()) + ")";
        }

        // Always derive departure from the live Lord. A member-level departure
        // state outranks broad toils, fixing common flee/stage errors.
        public static LordDepartureState GetDepartureState(Lord lord)
        {
            LordToil toil = lord?.CurLordToil;
            if (toil is LordToil_PanicFlee)
                return LordDepartureState.Fleeing;
            if (toil is LordToil_ExitMap)
                return LordDepartureState.Withdrawing;

            LordDepartureState memberDeparture = GetMemberDepartureState(lord);
            if (memberDeparture != LordDepartureState.None)
                return memberDeparture;

            string toilName = toil?.GetType().Name ?? string.Empty;
            if (toilName.IndexOf("Flee", StringComparison.OrdinalIgnoreCase) >= 0)
                return LordDepartureState.Fleeing;
            if (toilName.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
                return LordDepartureState.Withdrawing;
            return LordDepartureState.None;
        }

        public static string DescribeDepartureState(LordDepartureState state)
        {
            switch (state)
            {
                case LordDepartureState.Fleeing:
                    return "fleeing";
                case LordDepartureState.Withdrawing:
                    return "withdrawing";
                default:
                    return null;
            }
        }

        private static LordDepartureState GetMemberDepartureState(Lord lord)
        {
            List<Pawn> pawns = lord?.ownedPawns;
            if (pawns == null || pawns.Count == 0)
                return LordDepartureState.None;

            int liveCount = 0;
            int fleeingCount = 0;
            int exitingCount = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                    continue;

                liveCount++;
                if (pawn.MentalStateDef == MentalStateDefOf.PanicFlee)
                    fleeingCount++;

                string dutyName = pawn.mindState?.duty?.def?.defName ?? string.Empty;
                if (dutyName.IndexOf("ExitMap", StringComparison.OrdinalIgnoreCase) >= 0)
                    exitingCount++;
            }

            if (liveCount == 0)
                return LordDepartureState.None;

            int decisiveCount = (liveCount + 1) / 2;
            if (fleeingCount >= decisiveCount)
                return LordDepartureState.Fleeing;
            if (exitingCount >= decisiveCount)
                return LordDepartureState.Withdrawing;
            return LordDepartureState.None;
        }

        public static string GetDefLabel(Def def)
        {
            if (def == null)
                return null;
            if (!string.IsNullOrEmpty(def.label))
                return def.label.CapitalizeFirst();
            return def.defName;
        }

        public static string BuildOrigin(IncidentParms parms)
        {
            if (parms?.quest == null || string.IsNullOrEmpty(parms.quest.name))
                return null;

            return "quest " + parms.quest.name;
        }

        public static string BuildArrivalDescription(IncidentParms parms)
        {
            string arrivalMode = GetDefLabel(parms?.raidArrivalMode);
            Map map = parms?.target as Map;
            string direction = GetSpawnDirection(map, parms?.spawnCenter ?? IntVec3.Invalid);

            if (string.IsNullOrEmpty(arrivalMode))
                return direction;
            if (string.IsNullOrEmpty(direction))
                return arrivalMode;
            if (direction == "near the map center")
                return arrivalMode + " near the map center";
            return arrivalMode + " from the " + direction;
        }

        private static string GetSpawnDirection(Map map, IntVec3 spawnCenter)
        {
            if (map == null || !spawnCenter.IsValid)
                return null;

            IntVec3 center = map.Center;
            int deltaX = spawnCenter.x - center.x;
            int deltaZ = spawnCenter.z - center.z;
            int horizontalThreshold = Math.Max(1, map.Size.x / 6);
            int verticalThreshold = Math.Max(1, map.Size.z / 6);

            if (Math.Abs(deltaX) < horizontalThreshold && Math.Abs(deltaZ) < verticalThreshold)
                return "near the map center";

            string northSouth = Math.Abs(deltaZ) >= verticalThreshold ? (deltaZ > 0 ? "north" : "south") : null;
            string eastWest = Math.Abs(deltaX) >= horizontalThreshold ? (deltaX > 0 ? "east" : "west") : null;

            if (!string.IsNullOrEmpty(northSouth) && !string.IsNullOrEmpty(eastWest))
                return northSouth + "-" + eastWest;
            return northSouth ?? eastWest;
        }

        public static void AppendLine(StringBuilder body, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            if (body.Length > 0)
                body.AppendLine();
            body.Append(text);
        }

        public static void AppendLiveState(
            StringBuilder body,
            ThreatRecordCore core,
            string unit,
            string phase)
        {
            int activeForce = CountLivePawns(core?.lords);
            int activeGroups = CountActiveLords(core?.lords);
            var line = new StringBuilder();
            line.Append("Active force: ").Append(activeForce).Append(' ').Append(unit);
            if (activeForce != 1)
                line.Append('s');
            line.Append(" across ").Append(activeGroups).Append(" group");
            if (activeGroups != 1)
                line.Append('s');
            line.Append('.');
            AppendLine(body, line.ToString());
            AppendLine(body, "Current phase: " + (string.IsNullOrEmpty(phase) ? "unknown" : phase) + ".");
        }

        private static bool HasHostileOwnedPawn(Lord lord)
        {
            List<Pawn> pawns = lord?.ownedPawns;
            if (pawns == null)
                return false;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn != null && !pawn.Dead && pawn.HostileTo(Faction.OfPlayer))
                    return true;
            }

            return false;
        }
    }
}
