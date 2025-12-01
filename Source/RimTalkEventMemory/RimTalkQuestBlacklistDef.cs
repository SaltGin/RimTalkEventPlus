using System.Collections.Generic;
using Verse;

namespace RimTalkEventPlus
{
    // Simple Def so we can maintain the blacklist via XML and patch it.
    public class RimTalkQuestBlacklistDef : Def
    {
        // List of QuestScriptDef defNames to ignore in Event+.
        public List<string> blacklistedQuestRoots;
    }
}
