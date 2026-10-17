#if RW1_5
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimTalkEventPlus
{
    // Included only in the 1.5 build. Newer versions keep using their native APIs.
    internal static class RimWorld15Compat
    {
        internal static List<Quest> GetActiveQuests(QuestManager manager)
        {
            return manager?.QuestsListForReading?.FindAll(
                quest => quest != null && quest.State == QuestState.Ongoing);
        }

        internal static Lord GetLord(Pawn pawn)
        {
            return pawn?.GetLord();
        }

        internal static bool IsActiveThreatToPlayer(IAttackTarget target)
        {
            // 1.5 has no canBeFogged argument; preserve its native threat rules.
            return GenHostility.IsActiveThreatToPlayer(target);
        }
    }
}
#endif
