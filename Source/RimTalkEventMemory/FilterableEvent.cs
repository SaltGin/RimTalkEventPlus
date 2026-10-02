namespace RimTalkEventPlus
{
    // Represents a filterable event with display and identification information.
    public class FilterableEvent
    {
        // Def name for a category that explicitly supports type filtering.
        public string defName;

        // Human-readable name for display (e.g., "Hospitality Refugee Chased").
        public string displayName;

        // Generated instance name for quests (e.g., "Pickles the Destitute").
        public string instanceName;

        // Event type category.
        public EventCategory category;

        // For instance filtering: unique instance ID (e.g., quest.id.ToString()).
        public string instanceID;

        public FilterableEvent(
            string defName,
            string displayName,
            string instanceName,
            EventCategory category,
            string instanceID = null)
        {
            this.defName = defName;
            this.displayName = displayName;
            this.instanceName = instanceName;
            this.category = category;
            this.instanceID = instanceID;
        }
    }
}
