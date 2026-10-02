using Verse;

namespace RimTalkEventPlus
{
    /// Represents one "ongoing" situation that the LLM should know about right now.
    public class OngoingEventSnapshot
    {
        /// DefName of the underlying quest root / incident / condition, if known.
        public string SourceDefName;

        /// Stable ID of the source quest, or -1 for non-quest events.
        /// Used to match a snapshot back to its exact active quest.
        public int QuestId = -1;

        /// First-class category for filtering. A null value means the producer
        /// did not classify the snapshot, so filter UI should not guess.
        public EventCategory? Category;

        /// Short title.
        public string Label;

        /// Main body text.
        public string Body;

        /// True if this is a threat-type event (raid, big danger).
        public bool IsThreat;
    }
}
