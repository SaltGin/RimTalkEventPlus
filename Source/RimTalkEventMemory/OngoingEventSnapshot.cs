using Verse;

namespace RimTalkEventPlus
{
    /// Represents one "ongoing" situation that the LLM should know about right now.
    public class OngoingEventSnapshot
    {
        /// DefName of the underlying quest root / incident / condition, if known.
        public string SourceDefName;

        /// Underlying type name, e.g. "Quest", "ChoiceLetter_ThreatBig".
        public string Kind;

        /// Short title.
        public string Label;

        /// Main body text.
        public string Body;

        /// Optional quest description (often same as Body for quests).
        public string QuestDescription;

        /// True if this is a threat-type event (raid, big danger).
        public bool IsThreat;
    }
}
