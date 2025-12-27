namespace RimTalkEventPlus
{
    // Represents a filterable event with display and identification information.
    public class FilterableEvent
    {
        // Stable def name for filtering (e.g., "Hospitality_Refugee_Chased").
        public string rootID;

        // Human-readable name for display (e.g., "Hospitality Refugee Chased").
        public string displayName;

        // Generated instance name for quests (e.g., "Pickles the Destitute").
        public string instanceName;

        // Event type category.
        public EventCategory category;

        // Source def name from the event.
        public string sourceDefName;

        // For instance filtering: unique instance ID (e.g., quest.id.ToString()).
        public string instanceID;

        public FilterableEvent(string rootID, string displayName, string instanceName, EventCategory category, string sourceDefName, string instanceID = null)
        {
            this.rootID = rootID;
            this.displayName = displayName;
            this.instanceName = instanceName;
            this.category = category;
            this.sourceDefName = sourceDefName;
            this.instanceID = instanceID;
        }
    }
}
