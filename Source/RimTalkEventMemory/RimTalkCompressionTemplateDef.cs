using Verse;

namespace RimTalkEventPlus
{
    /// DefName-based compression template for Event+.
    /// One instance per quest root / incident / condition you want to compress.
    public class RimTalkCompressionTemplateDef : Def
    {
        /// The source DefName this template applies to.
        /// For quests: quest.root.defName (e.g. "BFA_FallenAngel_Accept").
        /// For incidents/conditions: the IncidentDef/GameConditionDef defName
        /// (e.g. "RaidEnemy", "SolarFlare").
        public string sourceDefName;

        /// Optional categorization to disambiguate, e.g. "Quest", "Incident", "Condition".
        /// If null/empty, the template will ignore kind.
        public string kind;

        /// The compressed body text to send instead of the full description.
        public string compressedBody;
    }
}
