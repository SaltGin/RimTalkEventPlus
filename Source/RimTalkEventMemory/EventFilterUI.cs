using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimTalkEventPlus
{
    // UI component for the event filtering system with two-section layout.
    public static class EventFilterUI
    {
        // UI state
        private static bool _showCurrentEventsOnly = true;
        private static Vector2 _scrollPosAvailableTypes = Vector2.zero;
        private static Vector2 _scrollPosDisabledTypes = Vector2.zero;
        private static Vector2 _scrollPosCurrentInstances = Vector2.zero;
        private static Vector2 _scrollPosHiddenInstances = Vector2.zero;

        // Selection tracking
        private static string _selectedAvailableType = null;
        private static string _selectedDisabledType = null;
        private static string _selectedCurrentInstance = null;
        private static string _selectedHiddenInstance = null;

        private const float SECTION_SPACING = 30f;
        private const float COLUMN_SPACING = 10f;
        private const float BUTTON_WIDTH = 30f;
        private const float HEADER_HEIGHT = 30f;
        private const float FILTER_CHECKBOX_HEIGHT = 24f;

        // Fixed section heights to ensure visibility
        private const float MIN_TYPE_SECTION_HEIGHT = 400f;
        private const float MIN_INSTANCE_SECTION_HEIGHT = 400f;

        // Outer scroll view state
        private static Vector2 _scrollPosOuter = Vector2.zero;

        // Self-correcting height measurement
        private static float _lastMeasuredTopSectionHeight = 250f;

        // Threat letter scan limits for current-type view
        private const int MAX_THREAT_SCAN_LETTERS_FOR_TYPES = 50;
        private const int THREAT_TIMEOUT_TICKS_FOR_TYPES = 7500; // 3 in-game hours

        // Subtitles (examples) for type rows, keyed by rootID (quests & threats only)
        private static readonly Dictionary<string, string> _typeSubtitles = new Dictionary<string, string>();

        // Helper method to ensure minimum content height for scrollbars to function
        private static float EnsureMinimumScrollHeight(float contentHeight, float scrollRectHeight)
        {
            float minContentHeight = scrollRectHeight + 1f;
            return contentHeight < minContentHeight ? minContentHeight : contentHeight;
        }

        // Helper to render the optimization section
        private static void RenderOptimizationSection(Listing_Standard listing, EventFilterSettings settings)
        {
            listing.Label("RimTalkEventPlus_OptimizationHeader".Translate());
            listing.GapLine();
            listing.CheckboxLabeled(
                "RimTalkEventPlus_EnableCompression_Label".Translate(),
                ref settings.enableEventTextCompression,
                "RimTalkEventPlus_EnableCompression_Tooltip".Translate()
            );

            using (new TextBlock(GameFont.Tiny))
            {
                listing.Label("RimTalkEventPlus_Compression_Description".Translate());
            }

            listing.Gap(SECTION_SPACING);
        }

        // Helper to render category filters section
        private static void RenderCategoryFiltersSection(Listing_Standard listing, EventFilterSettings settings)
        {
            listing.GapLine();
            listing.Gap(10f);

            using (new TextBlock(GameFont.Small))
            {
                listing.Label("RimTalkEventPlus_CategoryFilters".Translate());
            }

            using (new TextBlock(GameFont.Tiny))
            {
                listing.Label("RimTalkEventPlus_CategoryFilters_Desc".Translate());
            }

            listing.Gap(5f);
            Rect categoryRect = listing.GetRect(FILTER_CHECKBOX_HEIGHT * 2 + 4f);
            DoQuickCategoryFilters(categoryRect, settings);
            listing.Gap(10f);
            listing.GapLine();
            listing.Gap(10f);
        }

        // Renders the complete event filtering UI with both type-based and instance-based sections.
        public static void DoFilteringUI(Rect inRect, EventFilterSettings settings)
        {
            if (settings == null)
                return;

            // Calculate total content height using self-correcting measurement
            float totalContentHeight = _lastMeasuredTopSectionHeight
                + MIN_TYPE_SECTION_HEIGHT
                + SECTION_SPACING
                + MIN_INSTANCE_SECTION_HEIGHT
                + 80f; // Reset buttons + padding

            // Begin outer scroll view
            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, totalContentHeight);
            Widgets.BeginScrollView(inRect, ref _scrollPosOuter, viewRect, true);

            float currentY = 0f;

            // Top section (optimization + category filters)
            var listing = new Listing_Standard();
            listing.Begin(new Rect(0f, currentY, viewRect.width, 9999f));
            RenderOptimizationSection(listing, settings);
            RenderCategoryFiltersSection(listing, settings);
            _lastMeasuredTopSectionHeight = listing.CurHeight;
            listing.End();

            currentY += _lastMeasuredTopSectionHeight;

            // Type-based filtering section with fixed height
            Rect typeBasedRect = new Rect(0f, currentY, viewRect.width, MIN_TYPE_SECTION_HEIGHT);
            DoTypeBasedFilteringSection(typeBasedRect, settings);

            currentY += MIN_TYPE_SECTION_HEIGHT + SECTION_SPACING;

            // Draw divider line between sections
            DrawHorizontalDivider(viewRect.width, currentY - SECTION_SPACING / 2f - 5f);

            // Instance-based filtering section with fixed height
            Rect instanceBasedRect = new Rect(0f, currentY, viewRect.width, MIN_INSTANCE_SECTION_HEIGHT);
            DoInstanceBasedFilteringSection(instanceBasedRect, settings);

            currentY += MIN_INSTANCE_SECTION_HEIGHT + 30f;

            // Bottom row with both reset buttons
            DrawResetButtons(viewRect.width, currentY, settings);

            Widgets.EndScrollView();
        }

        // Helper to draw section headers consistently
        private static void DrawSectionHeader(Rect rect, string titleKey, string descKey)
        {
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, HEADER_HEIGHT);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
            {
                Widgets.Label(headerRect, titleKey.Translate());
            }

            Rect descRect = new Rect(rect.x, rect.y + HEADER_HEIGHT, rect.width, 20f);
            using (new TextBlock(GameFont.Tiny))
            {
                Widgets.Label(descRect, descKey.Translate());
            }
        }

        // Helper to draw horizontal divider
        private static void DrawHorizontalDivider(float width, float yPos)
        {
            Widgets.DrawLineHorizontal(0f, yPos, width);
        }

        // Helper to draw reset buttons
        private static void DrawResetButtons(float viewWidth, float yPos, EventFilterSettings settings)
        {
            float buttonSpacing = 20f;
            float leftMargin = 10f;
            float buttonHeight = 30f;
            float buttonPadding = 20f;

            string resetTypeText = "RimTalkEventPlus_ResetTypeFilters".Translate();
            string resetInstanceText = "RimTalkEventPlus_ResetInstanceFilters".Translate();

            float button1Width = Text.CalcSize(resetTypeText).x + buttonPadding;
            float button2Width = Text.CalcSize(resetInstanceText).x + buttonPadding;

            Rect resetTypeButtonRect = new Rect(leftMargin, yPos, button1Width, buttonHeight);
            Rect resetInstanceButtonRect = new Rect(leftMargin + button1Width + buttonSpacing, yPos, button2Width, buttonHeight);

            if (Widgets.ButtonText(resetTypeButtonRect, resetTypeText))
            {
                int count = settings.disabledEventDefNames.Count;
                settings.disabledEventDefNames.Clear();

                SoundDefOf.Click.PlayOneShotOnCamera(null);
                Messages.Message(
                    "RimTalkEventPlus_TypeFiltersCleared".Translate(count),
                    MessageTypeDefOf.PositiveEvent
                );
            }

            if (Widgets.ButtonText(resetInstanceButtonRect, resetInstanceText))
            {
                int totalCount = 0;
                if (settings.disabledEventInstances != null)
                {
                    foreach (var kvp in settings.disabledEventInstances)
                    {
                        if (kvp.Value != null)
                        {
                            totalCount += kvp.Value.Count;
                        }
                    }

                    settings.disabledEventInstances.Clear();
                }

                SoundDefOf.Click.PlayOneShotOnCamera(null);
                Messages.Message(
                    "RimTalkEventPlus_InstanceFiltersCleared".Translate(totalCount),
                    MessageTypeDefOf.PositiveEvent
                );
            }
        }

        // Helper struct to calculate two-column layout
        private struct TwoColumnLayout
        {
            public readonly Rect LeftColumn;
            public readonly Rect RightColumn;
            public readonly Rect ButtonsArea;

            public TwoColumnLayout(Rect parentRect, float yOffset)
            {
                float columnWidth = (parentRect.width - COLUMN_SPACING - BUTTON_WIDTH * 2) / 2f;
                float columnHeight = parentRect.height - yOffset;

                LeftColumn = new Rect(parentRect.x, parentRect.y + yOffset, columnWidth, columnHeight);
                RightColumn = new Rect(parentRect.x + columnWidth + COLUMN_SPACING + BUTTON_WIDTH * 2, parentRect.y + yOffset, columnWidth, columnHeight);
                ButtonsArea = new Rect(parentRect.x + columnWidth + COLUMN_SPACING, parentRect.y + yOffset, BUTTON_WIDTH * 2, columnHeight);
            }
        }

        // Renders the type-based filtering section. 
        private static void DoTypeBasedFilteringSection(Rect rect, EventFilterSettings settings)
        {
            DrawSectionHeader(rect, "RimTalkEventPlus_TypeBasedFiltering", "RimTalkEventPlus_TypeBasedFiltering_Desc");

            float yPos = HEADER_HEIGHT + 25f;

            // Radio buttons for event source selection
            DrawRadioButtons(rect, yPos);
            yPos += 70f;

            // Two-column layout
            var layout = new TwoColumnLayout(rect, yPos);

            // Get available and disabled events
            var allEvents = GetAvailableEventTypes(_showCurrentEventsOnly, settings);
            var availableEvents = allEvents.Where(e => !settings.disabledEventDefNames.Contains(e.rootID)).ToList();
            var disabledEvents = allEvents.Where(e => settings.disabledEventDefNames.Contains(e.rootID)).ToList();

            // Draw columns
            DoEventTypeColumn(layout.LeftColumn, "RimTalkEventPlus_AvailableTypes".Translate(), availableEvents, settings, ref _scrollPosAvailableTypes, false);
            DoEventTypeColumn(layout.RightColumn, "RimTalkEventPlus_DisabledTypes".Translate(), disabledEvents, settings, ref _scrollPosDisabledTypes, true);

            // Draw arrow buttons
            DoTypeFilterButtons(layout.ButtonsArea, availableEvents, disabledEvents, settings);
        }

        // Helper to draw radio buttons for event source selection
        private static void DrawRadioButtons(Rect parentRect, float yOffset)
        {
            float yPos = parentRect.y + yOffset;
            float leftMargin = parentRect.x + 10f;
            float radioSize = 24f;
            float labelGap = 8f;
            float optionSpacing = 300f;

            bool wasShowCurrentOnly = _showCurrentEventsOnly;

            string label1 = "RimTalkEventPlus_CurrentEventsOnly".Translate();
            Rect radio1Rect = new Rect(leftMargin, yPos, radioSize, radioSize);

            if (Widgets.RadioButton(radio1Rect.x, radio1Rect.y, _showCurrentEventsOnly))
            {
                _showCurrentEventsOnly = true;
            }

            Rect label1Rect = new Rect(leftMargin + radioSize + labelGap, yPos, 200f, 30f);
            Widgets.Label(label1Rect, label1);
            if (Widgets.ButtonInvisible(new Rect(radio1Rect.x, yPos, radioSize + labelGap + Text.CalcSize(label1).x, 30f)))
            {
                _showCurrentEventsOnly = true;
                if (!wasShowCurrentOnly)
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
            }

            string label2 = "RimTalkEventPlus_AllEventTypes".Translate();
            float option2X = leftMargin + optionSpacing;
            Rect radio2Rect = new Rect(option2X, yPos, radioSize, radioSize);

            if (Widgets.RadioButton(radio2Rect.x, radio2Rect.y, !_showCurrentEventsOnly))
            {
                _showCurrentEventsOnly = false;
            }

            Rect label2Rect = new Rect(option2X + radioSize + labelGap, yPos, 200f, 30f);
            Widgets.Label(label2Rect, label2);
            if (Widgets.ButtonInvisible(new Rect(radio2Rect.x, yPos, radioSize + labelGap + Text.CalcSize(label2).x, 30f)))
            {
                _showCurrentEventsOnly = false;
                if (wasShowCurrentOnly)
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera(null);
            }

            // Explanatory note below radio buttons
            Rect noteRect = new Rect(parentRect.x + 10f, parentRect.y + yOffset + 32f, parentRect.width - 20f, 30f);
            using (new TextBlock(GameFont.Tiny))
            using (new ColorBlock(new Color(0.7f, 0.7f, 0.7f)))
            {
                string note = _showCurrentEventsOnly
                    ? "RimTalkEventPlus_CurrentEventsNote".Translate()
                    : "RimTalkEventPlus_AllEventTypesNote".Translate();

                Widgets.Label(noteRect, note);
            }
        }

        // Renders the instance-based filtering section.
        private static void DoInstanceBasedFilteringSection(Rect rect, EventFilterSettings settings)
        {
            DrawSectionHeader(rect, "RimTalkEventPlus_InstanceBasedFiltering", "RimTalkEventPlus_InstanceBasedFiltering_Desc");

            float yPos = HEADER_HEIGHT + 30f;

            // Auto-cleanup:  Remove instances that are no longer active
            CleanupInactiveInstances(settings);

            // Two-column layout
            var layout = new TwoColumnLayout(rect, yPos);

            string colonyId = GetCurrentColonyId();
            var instanceSet = settings.GetInstanceSet(colonyId);

            // Get current and hidden instances
            var allInstances = GetCurrentEventInstances(settings);

            // Helper to check if category is disabled
            Func<FilterableEvent, bool> isCategoryDisabled = e =>
                (e.category == EventCategory.Quest && !settings.showQuests) ||
                (e.category == EventCategory.MapCondition && !settings.showMapConditions) ||
                (e.category == EventCategory.Threat && !settings.showThreats) ||
                (e.category == EventCategory.SitePart && !settings.showSiteParts);

            var currentInstances = allInstances.Where(e =>
                (instanceSet == null || !instanceSet.Contains(e.instanceID)) &&
                !settings.disabledEventDefNames.Contains(e.rootID) &&
                !isCategoryDisabled(e)
            ).ToList();

            // Hidden instances include manually hidden, globally disabled, AND category disabled
            var hiddenInstances = new List<FilterableEvent>();
            hiddenInstances.AddRange(allInstances.Where(e => instanceSet != null && instanceSet.Contains(e.instanceID)));
            hiddenInstances.AddRange(allInstances.Where(e =>
                settings.disabledEventDefNames.Contains(e.rootID) &&
                (instanceSet == null || !instanceSet.Contains(e.instanceID))
            ));
            hiddenInstances.AddRange(allInstances.Where(e =>
                isCategoryDisabled(e) &&
                !settings.disabledEventDefNames.Contains(e.rootID) &&
                (instanceSet == null || !instanceSet.Contains(e.instanceID))
            ));

            // Draw columns
            DoEventInstanceColumn(layout.LeftColumn, "RimTalkEventPlus_CurrentInstances".Translate(), currentInstances, settings, ref _scrollPosCurrentInstances, false);
            DoEventInstanceColumn(layout.RightColumn, "RimTalkEventPlus_HiddenInstances".Translate(), hiddenInstances, settings, ref _scrollPosHiddenInstances, true);

            // Draw arrow buttons
            DoInstanceFilterButtons(layout.ButtonsArea, currentInstances, hiddenInstances, settings);
        }

        // Draws quick category filter checkboxes in a 2x2 grid layout. 
        private static void DoQuickCategoryFilters(Rect rect, EventFilterSettings settings)
        {
            float cellPadding = 4f;
            float cellWidth = (rect.width - cellPadding) / 2f;
            float cellHeight = (rect.height - cellPadding) / 2f;
            float boxPadding = 2f;
            int borderThickness = 1;
            float checkboxSize = 24f;
            float checkboxLabelGap = 6f;
            Color boxColor = new Color(0.18f, 0.18f, 0.18f, 0.35f);
            Color borderColor = Color.gray;

            void DrawCheckboxBox(Rect outerRect, string label, ref bool value)
            {
                // Draw box background and border
                Widgets.DrawBoxSolid(outerRect, boxColor);
                using (new ColorBlock(borderColor))
                {
                    Widgets.DrawBox(outerRect, borderThickness);
                }

                Rect innerRect = new Rect(
                    outerRect.x + boxPadding,
                    outerRect.y + boxPadding,
                    outerRect.width - 2 * boxPadding,
                    outerRect.height - 2 * boxPadding
                );

                // Checkbox position (left side, vertically centered)
                float checkboxY = innerRect.y + (innerRect.height - checkboxSize) / 2f;

                // Label area (to the right of checkbox)
                Rect labelRect = new Rect(
                    innerRect.x + checkboxSize + checkboxLabelGap,
                    innerRect.y,
                    innerRect.width - checkboxSize - checkboxLabelGap,
                    innerRect.height
                );

                using (new TextBlock(GameFont.Small, TextAnchor.MiddleLeft))
                {
                    Widgets.Label(labelRect, label);
                }

                // Handle click on entire box area
                if (Widgets.ButtonInvisible(outerRect))
                {
                    value = !value;
                    if (value)
                        SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera(null);
                    else
                        SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera(null);
                }

                // Draw checkbox visual only (no click handling)
                Widgets.CheckboxDraw(innerRect.x, checkboxY, value, false, checkboxSize);
            }

            DrawCheckboxBox(
                new Rect(rect.x, rect.y, cellWidth, cellHeight),
                "RimTalkEventPlus_QuickFilter_Quests".Translate(),
                ref settings.showQuests
            );
            DrawCheckboxBox(
                new Rect(rect.x + cellWidth + cellPadding, rect.y, cellWidth, cellHeight),
                "RimTalkEventPlus_QuickFilter_MapConditions".Translate(),
                ref settings.showMapConditions
            );
            DrawCheckboxBox(
                new Rect(rect.x, rect.y + cellHeight + cellPadding, cellWidth, cellHeight),
                "RimTalkEventPlus_QuickFilter_Threats".Translate(),
                ref settings.showThreats
            );
            DrawCheckboxBox(
                new Rect(rect.x + cellWidth + cellPadding, rect.y + cellHeight + cellPadding, cellWidth, cellHeight),
                "RimTalkEventPlus_QuickFilter_Sites".Translate(),
                ref settings.showSiteParts
            );
        }

        // Draws a column of event types. 
        private static void DoEventTypeColumn(Rect rect, string title, List<FilterableEvent> events, EventFilterSettings settings, ref Vector2 scrollPos, bool isDisabled)
        {
            Widgets.DrawMenuSection(rect);

            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 25f);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleCenter))
            {
                Widgets.Label(titleRect, title);
            }

            var hiddenCategories = new List<EventCategory>();
            if (!settings.showQuests) hiddenCategories.Add(EventCategory.Quest);
            if (!settings.showMapConditions) hiddenCategories.Add(EventCategory.MapCondition);
            if (!settings.showThreats) hiddenCategories.Add(EventCategory.Threat);
            if (!settings.showSiteParts) hiddenCategories.Add(EventCategory.SitePart);

            float categoryIndicatorHeight = (isDisabled && hiddenCategories.Count > 0) ? hiddenCategories.Count * 30f : 0f;

            var filteredEvents = events.Where(e =>
                (e.category == EventCategory.Quest && settings.showQuests) ||
                (e.category == EventCategory.MapCondition && settings.showMapConditions) ||
                (e.category == EventCategory.Threat && settings.showThreats) ||
                (e.category == EventCategory.SitePart && settings.showSiteParts)
            ).ToList();

            var groupedEvents = filteredEvents.GroupBy(e => e.category).OrderBy(g => g.Key).ToList();

            float contentHeight = groupedEvents.Sum(g =>
            {
                float perGroup = 25f;
                foreach (var evt in g)
                {
                    bool hasSubtitle = evt.category == EventCategory.Quest || evt.category == EventCategory.Threat;
                    perGroup += hasSubtitle ? 41f : 25f; // 25 base + 16 subtitle
                }
                return perGroup;
            }) + categoryIndicatorHeight;

            Rect scrollRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            contentHeight = EnsureMinimumScrollHeight(contentHeight, scrollRect.height);

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect, true);

            float yOffset = 0f;

            if (isDisabled && hiddenCategories.Count > 0)
            {
                foreach (var category in hiddenCategories.OrderBy(c => c))
                {
                    yOffset += DrawCategoryFilterIndicator(viewRect.width, yOffset, category);
                }
            }

            foreach (var group in groupedEvents)
            {
                yOffset += DrawCategoryHeader(viewRect.width, yOffset, group.Key.ToString());

                foreach (var evt in group)
                {
                    bool isSelected = isDisabled ? (_selectedDisabledType == evt.rootID) : (_selectedAvailableType == evt.rootID);
                    string subtitle = null;
                    if (evt.category == EventCategory.Quest || evt.category == EventCategory.Threat)
                    {
                        _typeSubtitles.TryGetValue(evt.rootID, out subtitle);
                    }

                    yOffset += DrawSelectableItem(
                        viewRect.width,
                        yOffset,
                        evt.displayName,
                        subtitle,
                        isSelected,
                        () =>
                        {
                            if (isDisabled)
                                _selectedDisabledType = evt.rootID;
                            else
                                _selectedAvailableType = evt.rootID;
                        });
                }
            }

            Widgets.EndScrollView();
        }

        // Draws a column of event instances. 
        private static void DoEventInstanceColumn(Rect rect, string title, List<FilterableEvent> events, EventFilterSettings settings, ref Vector2 scrollPos, bool isHidden)
        {
            Widgets.DrawMenuSection(rect);

            Rect titleRect = new Rect(rect.x, rect.y, rect.width, 25f);
            using (new TextBlock(GameFont.Small, TextAnchor.MiddleCenter))
            {
                Widgets.Label(titleRect, title);
            }

            var groupedEvents = events.GroupBy(e => e.category).OrderBy(g => g.Key).ToList();

            float contentHeight = 0f;
            foreach (var group in groupedEvents)
            {
                contentHeight += 25f;
                foreach (var evt in group)
                {
                    contentHeight += 25f;
                    if (settings.disabledEventDefNames.Contains(evt.rootID))
                        contentHeight += 15f;
                }
            }

            Rect scrollRect = new Rect(rect.x, rect.y + 30f, rect.width, rect.height - 30f);
            contentHeight = EnsureMinimumScrollHeight(contentHeight, scrollRect.height);

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f, contentHeight);
            Widgets.BeginScrollView(scrollRect, ref scrollPos, viewRect, true);

            float yOffset = 0f;
            foreach (var group in groupedEvents)
            {
                yOffset += DrawCategoryHeader(viewRect.width, yOffset, group.Key.ToString());

                foreach (var evt in group)
                {
                    bool isGloballyDisabled = settings.disabledEventDefNames.Contains(evt.rootID);
                    bool isCategoryDisabled =
                        (evt.category == EventCategory.Quest && !settings.showQuests) ||
                        (evt.category == EventCategory.MapCondition && !settings.showMapConditions) ||
                        (evt.category == EventCategory.Threat && !settings.showThreats) ||
                        (evt.category == EventCategory.SitePart && !settings.showSiteParts);
                    bool isDisabled = isGloballyDisabled || isCategoryDisabled;
                    bool isSelected = isHidden ? (_selectedHiddenInstance == evt.instanceID) : (_selectedCurrentInstance == evt.instanceID);

                    string displayText = string.IsNullOrEmpty(evt.instanceName)
                        ? evt.rootID
                        : $"{evt.instanceName} ({evt.rootID})";

                    yOffset += DrawSelectableItem(
                        viewRect.width,
                        yOffset,
                        displayText,
                        null,
                        isSelected && !isDisabled,
                        () =>
                        {
                            if (isHidden)
                                _selectedHiddenInstance = evt.instanceID;
                            else
                                _selectedCurrentInstance = evt.instanceID;
                        },
                        isDisabled);

                    if (isDisabled)
                    {
                        yOffset += DrawDisabledNote(viewRect.width, yOffset);
                    }
                }
            }

            Widgets.EndScrollView();
        }

        // Helper to draw category header
        private static float DrawCategoryHeader(float width, float yPos, string category)
        {
            Rect categoryRect = new Rect(0f, yPos, width, 25f);
            using (new TextBlock(GameFont.Tiny))
            using (new ColorBlock(Color.gray))
            {
                Widgets.Label(categoryRect, $"— {category} —");
            }
            return 25f;
        }

        // Helper to draw category filter indicator
        private static float DrawCategoryFilterIndicator(float width, float yPos, EventCategory category)
        {
            Rect indicatorRect = new Rect(5f, yPos, width - 10f, 28f);

            Color bgColor = new Color(0.4f, 0.3f, 0.3f, 0.3f);
            Widgets.DrawBoxSolid(indicatorRect, bgColor);

            Rect labelRect = new Rect(10f, yPos + 2f, width - 20f, 24f);
            using (new TextBlock(GameFont.Tiny))
            using (new ColorBlock(new Color(0.7f, 0.7f, 0.7f)))
            {
                string label = $"All {category}s (hidden by category filter)";
                Widgets.Label(labelRect, label);
            }

            return 30f;
        }

        // Helper to draw selectable item
        private static float DrawSelectableItem(float width, float yPos, string label, string subtitle, bool isSelected, Action onSelect, bool isDisabled = false)
        {
            float subtitleHeight = subtitle.NullOrEmpty() ? 0f : 16f;
            float rowHeight = 25f + subtitleHeight;

            Rect itemRect = new Rect(10f, yPos, width - 10f, rowHeight);

            if (isSelected)
                Widgets.DrawHighlight(itemRect);

            if (Mouse.IsOver(itemRect) && !isDisabled)
                Widgets.DrawLightHighlight(itemRect);

            using (new ColorBlock(isDisabled ? Color.gray : Color.white))
            {
                if (!isDisabled && Widgets.ButtonInvisible(itemRect))
                    onSelect();

                Rect labelRect = new Rect(itemRect.x, itemRect.y, itemRect.width, 25f);
                Widgets.Label(labelRect, label);

                if (!subtitle.NullOrEmpty())
                {
                    using (new TextBlock(GameFont.Tiny))
                    using (new ColorBlock(new Color(0.7f, 0.7f, 0.7f)))
                    {
                        Rect subRect = new Rect(itemRect.x, itemRect.y + 14f, itemRect.width, 16f);
                        string subtitleText = $"(e.g. {subtitle})";
                        Widgets.Label(subRect, subtitleText);
                    }
                }
            }

            return rowHeight;
        }

        // Helper to draw "globally disabled" note
        private static float DrawDisabledNote(float width, float yPos, string noteKey = "RimTalkEventPlus_GloballyDisabled")
        {
            Rect noteRect = new Rect(20f, yPos, width - 20f, 15f);
            using (new TextBlock(GameFont.Tiny))
            using (new ColorBlock(Color.gray))
            {
                Widgets.Label(noteRect, "(" + noteKey.Translate() + ")");
            }
            return 15f;
        }

        // Draws arrow buttons for type filtering. 
        private static void DoTypeFilterButtons(Rect rect, List<FilterableEvent> available, List<FilterableEvent> disabled, EventFilterSettings settings)
        {
            float centerY = rect.y + rect.height / 2f;
            float centerX = rect.x + (rect.width - BUTTON_WIDTH) / 2f;

            Rect rightArrowRect = new Rect(centerX, centerY - 40f, BUTTON_WIDTH, 30f);
            bool canDisable = !string.IsNullOrEmpty(_selectedAvailableType);

            if (DrawArrowButton(rightArrowRect, "→", canDisable))
            {
                settings.disabledEventDefNames.Add(_selectedAvailableType);
                _selectedAvailableType = null;
            }

            Rect leftArrowRect = new Rect(centerX, centerY + 10f, BUTTON_WIDTH, 30f);
            bool canEnable = !string.IsNullOrEmpty(_selectedDisabledType);

            if (DrawArrowButton(leftArrowRect, "←", canEnable))
            {
                settings.disabledEventDefNames.Remove(_selectedDisabledType);
                _selectedDisabledType = null;
            }
        }

        // Draws arrow buttons for instance filtering. 
        private static void DoInstanceFilterButtons(Rect rect, List<FilterableEvent> current, List<FilterableEvent> hidden, EventFilterSettings settings)
        {
            float centerY = rect.y + rect.height / 2f;
            float centerX = rect.x + (rect.width - BUTTON_WIDTH) / 2f;

            string colonyId = GetCurrentColonyId();

            Rect rightArrowRect = new Rect(centerX, centerY - 40f, BUTTON_WIDTH, 30f);
            bool canHide = CanHideInstance(_selectedCurrentInstance, current, settings);

            if (DrawArrowButton(rightArrowRect, "→", canHide))
            {
                if (!string.IsNullOrEmpty(colonyId))
                {
                    var instanceSet = settings.GetOrCreateInstanceSet(colonyId);
                    instanceSet.Add(_selectedCurrentInstance);
                }
                _selectedCurrentInstance = null;
            }

            Rect leftArrowRect = new Rect(centerX, centerY + 10f, BUTTON_WIDTH, 30f);
            bool canUnhide = CanUnhideInstance(_selectedHiddenInstance, hidden, settings);

            if (DrawArrowButton(leftArrowRect, "←", canUnhide))
            {
                if (!string.IsNullOrEmpty(colonyId))
                {
                    var instanceSet = settings.GetInstanceSet(colonyId);
                    if (instanceSet != null)
                    {
                        instanceSet.Remove(_selectedHiddenInstance);
                        PruneEmptyInstanceSet(settings, colonyId);
                    }
                }
                _selectedHiddenInstance = null;
            }
        }

        // Helper to check if instance can be hidden
        private static bool CanHideInstance(string instanceID, List<FilterableEvent> events, EventFilterSettings settings)
        {
            if (string.IsNullOrEmpty(instanceID))
                return false;

            string colonyId = GetCurrentColonyId();
            if (string.IsNullOrEmpty(colonyId))
                return false;

            var selectedEvent = events.FirstOrDefault(e => e.instanceID == instanceID);
            return selectedEvent != null && !settings.disabledEventDefNames.Contains(selectedEvent.rootID);
        }

        // Helper to check if instance can be unhidden
        private static bool CanUnhideInstance(string instanceID, List<FilterableEvent> events, EventFilterSettings settings)
        {
            if (string.IsNullOrEmpty(instanceID))
                return false;

            string colonyId = GetCurrentColonyId();
            if (string.IsNullOrEmpty(colonyId))
                return false;

            var selectedEvent = events.FirstOrDefault(e => e.instanceID == instanceID);
            return selectedEvent != null && !settings.disabledEventDefNames.Contains(selectedEvent.rootID);
        }

        // Helper to draw arrow button with enable/disable state
        private static bool DrawArrowButton(Rect rect, string label, bool enabled)
        {
            if (!enabled)
            {
                using (new ColorBlock(Color.gray))
                {
                    Widgets.ButtonText(rect, label);
                }
                return false;
            }

            return Widgets.ButtonText(rect, label);
        }

        // Gets all available event types based on user selection. 
        private static List<FilterableEvent> GetAvailableEventTypes(bool currentOnly, EventFilterSettings settings)
        {
            var events = new List<FilterableEvent>();
            _typeSubtitles.Clear();

            if (currentOnly)
            {
                // Derive types from actual appendable instances
                var instances = GetCurrentEventInstances(settings);
                var addedDefs = new HashSet<string>();

                // Quest types from current instances, capture example instance names
                foreach (var inst in instances)
                {
                    if (!addedDefs.Add(inst.rootID))
                        continue;

                        // Try to get proper label from def database based on category
                    string displayName = inst.rootID;
                    if (inst.category == EventCategory.Quest)
                    {
                        var questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(inst.rootID);
                        if (questDef != null && !questDef.LabelCap.NullOrEmpty())
                            displayName = questDef.LabelCap;
                    }

                    events.Add(new FilterableEvent(
                        inst.rootID,
                        displayName,
                        null,
                        inst.category,
                        inst.sourceDefName
                    ));

                    if (inst.category == EventCategory.Quest && !inst.instanceName.NullOrEmpty())
                    {
                        _typeSubtitles[inst.rootID] = inst.instanceName;
                    }
                }

                // Current map conditions (displayOnUI) add their defs as types
                AddCurrentMapConditionTypes(events, addedDefs);
                // Current site parts on non-home maps add their defs as types
                AddCurrentSitePartTypes(events, addedDefs);

                // Current threat letters (ThreatBig/ThreatSmall) if recently present
                AddCurrentThreatLetterTypes(events, addedDefs, _typeSubtitles);
            }
            else
            {
                var questDefs = DefDatabase<QuestScriptDef>.AllDefsListForReading;
                if (questDefs != null)
                {
                    foreach (var def in questDefs)
                    {
                        if (def?.defName != null)
                        {
                            events.Add(new FilterableEvent(
                                def.defName,
                                (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                                null,
                                EventCategory.Quest,
                                def.defName
                            ));
                        }
                    }
                }

                var conditionDefs = DefDatabase<GameConditionDef>.AllDefsListForReading;
                if (conditionDefs != null)
                {
                    foreach (var def in conditionDefs)
                    {
                        if (def?.defName != null && def.displayOnUI)
                        {
                            events.Add(new FilterableEvent(
                                def.defName,
                                (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                                null,
                                EventCategory.MapCondition,
                                def.defName
                            ));
                        }
                    }
                }

                var sitePartDefs = DefDatabase<SitePartDef>.AllDefsListForReading;
                if (sitePartDefs != null)
                {
                    foreach (var def in sitePartDefs)
                    {
                        if (def?.defName != null)
                        {
                            events.Add(new FilterableEvent(
                                def.defName,
                                (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                                null,
                                EventCategory.SitePart,
                                def.defName
                            ));
                        }
                    }
                }

                // Threat types: letters only (ThreatBig / ThreatSmall)
                var threatLetters = new[] { LetterDefOf.ThreatBig, LetterDefOf.ThreatSmall };
                foreach (var def in threatLetters)
                {
                    if (def?.defName != null)
                    {
                        events.Add(new FilterableEvent(
                            def.defName,
                            (def.LabelCap.NullOrEmpty() ? def.defName : (string)def.LabelCap),
                            null,
                            EventCategory.Threat,
                            def.defName
                        ));
                    }
                }
            }

            return events;
        }

        // Adds ThreatBig/ThreatSmall types to the current-only list if recent threat letters exist.
        private static void AddCurrentThreatLetterTypes(List<FilterableEvent> events, HashSet<string> addedDefs, Dictionary<string, string> subtitles)
        {
            bool anyMapInDanger = false;
            var maps = Find.Maps;
            if (maps != null)
            {
                anyMapInDanger = maps.Any(m => m != null && m.dangerWatcher != null && m.dangerWatcher.DangerRating != StoryDanger.None);
            }

            if (!anyMapInDanger)
                return;

            if (Find.Archive == null)
                return;

            var list = Find.Archive.ArchivablesListForReading;
            if (list == null)
                return;

            int nowTicks = Find.TickManager?.TicksGame ?? -1;
            int scanned = 0;

            for (int i = list.Count - 1; i >= 0 && scanned < MAX_THREAT_SCAN_LETTERS_FOR_TYPES; i--, scanned++)
            {
                if (list[i] is Letter letter && letter.def != null)
                {
                    bool isThreatLetter = letter.def == LetterDefOf.ThreatBig || letter.def == LetterDefOf.ThreatSmall;
                    if (!isThreatLetter)
                        continue;

                    if (nowTicks >= 0 && THREAT_TIMEOUT_TICKS_FOR_TYPES > 0)
                    {
                        int ageTicks = nowTicks - (int)letter.arrivalTime;
                        if (ageTicks > THREAT_TIMEOUT_TICKS_FOR_TYPES)
                            continue;
                    }

                    string defName = letter.def.defName;
                    if (!addedDefs.Add(defName))
                        continue;

                    string label = letter.def.LabelCap.NullOrEmpty() ? defName : (string)letter.def.LabelCap;

                    events.Add(new FilterableEvent(
                        defName,
                        label,
                        null,
                        EventCategory.Threat,
                        defName
                    ));

                    // Subtitle: example threat letter label
                    var archivable = letter as IArchivable;
                    string archivedLabel = GetArchivedLabelSafe(archivable);
                    if (!archivedLabel.NullOrEmpty())
                    {
                        subtitles[defName] = archivedLabel;
                    }
                }
            }
        }

        // Adds currently active map condition defs (displayOnUI) to the current-only list.
        private static void AddCurrentMapConditionTypes(List<FilterableEvent> events, HashSet<string> addedDefs)
        {
            var maps = Find.Maps;
            if (maps == null)
                return;

            foreach (var map in maps)
            {
                if (map == null) continue;

                var gcm = map.gameConditionManager;
                if (gcm == null) continue;

                var conds = gcm.ActiveConditions;
                if (conds == null) continue;

                foreach (var cond in conds)
                {
                    if (cond?.def?.defName == null || !cond.def.displayOnUI)
                        continue;

                    string defName = cond.def.defName;
                    if (!addedDefs.Add(defName))
                        continue;

                    string label = cond.def.LabelCap.NullOrEmpty() ? defName : (string)cond.def.LabelCap;

                    events.Add(new FilterableEvent(
                        defName,
                        label,
                        null,
                        EventCategory.MapCondition,
                        defName
                    ));
                }
            }
        }

        // Adds current site part defs (non-home site maps) to the current-only list.
        private static void AddCurrentSitePartTypes(List<FilterableEvent> events, HashSet<string> addedDefs)
        {
            var maps = Find.Maps;
            if (maps == null)
                return;

            foreach (var map in maps)
            {
                if (map == null) continue;
                if (map.IsPlayerHome) continue;

                var parent = map.Parent;
                if (parent is Site site && site.parts != null)
                {
                    foreach (var part in site.parts)
                    {
                        if (part?.def?.defName == null)
                            continue;

                        string defName = part.def.defName;
                        if (!addedDefs.Add(defName))
                            continue;

                        string label = part.def.LabelCap.NullOrEmpty() ? defName : (string)part.def.LabelCap;

                        events.Add(new FilterableEvent(
                            defName,
                            label,
                            null,
                            EventCategory.SitePart,
                            defName
                        ));
                    }
                }
            }
        }

        // Gets all current event instances from the game state.
        private static List<FilterableEvent> GetCurrentEventInstances(EventFilterSettings settings)
        {
            var instances = new List<FilterableEvent>();

            if (Current.Game == null)
                return instances;

            if (Find.QuestManager != null)
            {
                var quests = Find.QuestManager.QuestsListForReading;
                if (quests != null)
                {
                    foreach (var quest in quests)
                    {
                        if (quest == null ||
                            quest.State != QuestState.Ongoing ||
                            QuestLinkUtil.IsQuestHidden(quest))
                            continue;

                        string questDefName = quest.root?.defName ?? "Unknown";
                        string questLabel = QuestLinkUtil.TryGetQuestLabel(quest);
                        string instanceID = quest.id.ToString();

                        instances.Add(new FilterableEvent(
                            questDefName,
                            questDefName,
                            questLabel,
                            EventCategory.Quest,
                            questDefName,
                            instanceID
                        ));
                    }
                }
            }
            return instances;
        }

        // Removes instance filters for events that are no longer active.
        private static void CleanupInactiveInstances(EventFilterSettings settings)
        {
            if (settings.disabledEventInstances == null || settings.disabledEventInstances.Count == 0)
                return;

            if (Current.Game == null || Find.QuestManager == null)
                return;

            string colonyId = GetCurrentColonyId();
            if (string.IsNullOrEmpty(colonyId))
                return;

            var instanceSet = settings.GetInstanceSet(colonyId);
            if (instanceSet == null || instanceSet.Count == 0)
            {
                PruneEmptyInstanceSet(settings, colonyId);
                return;
            }

            var activeInstanceIDs = new HashSet<string>();

            var quests = Find.QuestManager.QuestsListForReading;
            if (quests != null)
            {
                foreach (var quest in quests)
                {
                    if (quest != null && quest.State == QuestState.Ongoing)
                    {
                        activeInstanceIDs.Add(quest.id.ToString());
                    }
                }
            }

            var toRemove = instanceSet.ids
                .Where(id => !activeInstanceIDs.Contains(id))
                .ToList();

            foreach (var instanceID in toRemove)
            {
                instanceSet.Remove(instanceID);
            }

            PruneEmptyInstanceSet(settings, colonyId);
        }

        // Gets the current colony identifier for per-colony instance filtering.  
        private static string GetCurrentColonyId()
        {
            if (Current.Game == null)
                return null;

            var worldInfo = Find.World?.info;
            if (worldInfo == null)
                return null;

            // Use persistentRandomValue as primary identifier
            int persistentRandom = worldInfo.persistentRandomValue;

            // Combine with seed for extra insurance
            string seed = worldInfo.seedString ?? "";

            return $"{seed}_{persistentRandom}";
        }

        // Helper to remove empty per-colony instance sets
        private static void PruneEmptyInstanceSet(EventFilterSettings settings, string colonyId)
        {
            if (settings?.disabledEventInstances == null || string.IsNullOrEmpty(colonyId))
                return;

            if (settings.disabledEventInstances.TryGetValue(colonyId, out var set))
            {
                if (set == null || set.Count == 0)
                {
                    settings.disabledEventInstances.Remove(colonyId);
                }
            }
        }

        private static string GetArchivedLabelSafe(IArchivable archivable)
        {
            if (archivable == null) return null;
            try { return archivable.ArchivedLabel; }
            catch { return null; }
        }

        // Helper struct for managing Text.Font state
        private struct TextBlock : IDisposable
        {
            private readonly GameFont _previousFont;
            private readonly TextAnchor _previousAnchor;
            private readonly bool _restoreAnchor;

            public TextBlock(GameFont font, TextAnchor anchor = TextAnchor.UpperLeft)
            {
                _previousFont = Text.Font;
                _previousAnchor = Text.Anchor;
                _restoreAnchor = anchor != TextAnchor.UpperLeft;

                Text.Font = font;
                if (_restoreAnchor)
                    Text.Anchor = anchor;
            }

            public void Dispose()
            {
                Text.Font = _previousFont;
                if (_restoreAnchor)
                    Text.Anchor = _previousAnchor;
            }
        }

        // Helper struct for managing GUI.color state
        private struct ColorBlock : IDisposable
        {
            private readonly Color _previousColor;

            public ColorBlock(Color color)
            {
                _previousColor = GUI.color;
                GUI.color = color;
            }

            public void Dispose()
            {
                GUI.color = _previousColor;
            }
        }
    }
}