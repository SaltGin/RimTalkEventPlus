using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimTalkEventPlus
{
    // Utility class for accessing Quest data and determining quest-map relationships.
    public static class QuestLinkUtil
    {
        private const BindingFlags FallbackBindingFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        #region Cache Access

        private static QuestCacheComponent GetCache()
        {
            return Current.Game?.GetComponent<QuestCacheComponent>();
        }

        #endregion

        #region Quest Field Access (Direct - Public Fields in RimWorld 1.6)

        public static string TryGetQuestDescription(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            string result = quest.description;
            return result ?? string.Empty;
        }

        public static string TryGetQuestLabel(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            if (!quest.name.NullOrEmpty())
                return quest.name;

            string desc = TryGetQuestDescription(quest);
            if (!desc.NullOrEmpty())
            {
                int nl = desc.IndexOf('\n');
                return nl > 0 ? desc.Substring(0, nl) : desc;
            }

            return "Quest";
        }

        public static bool IsQuestHidden(Quest quest)
        {
            if (quest == null)
                return false;

            return quest.hidden;
        }

        public static bool IsQuestOngoing(Quest quest)
        {
            return quest?.State == QuestState.Ongoing;
        }

        #endregion

        #region Quest Accepted Age

        // Returns a short marker like "accepted ~1.3 days ago" based on acceptanceTick.
        public static string GetQuestAcceptedAgeMarker(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            int acceptanceTick = quest.acceptanceTick;

            if (acceptanceTick <= 0)
                return string.Empty;

            int currentTicks = Find.TickManager.TicksGame;
            int diff = currentTicks - acceptanceTick;
            if (diff <= 0)
                return "accepted just now";

            // RimWorld uses 60,000 ticks per in-game day. 
            double days = diff / 60000.0;

            if (days >= 1.0)
            {
                double roundedDays = Math.Round(days, 1);
                string daysStr = roundedDays.ToString("0.0");
                return "accepted ~" + daysStr + " days ago";
            }
            else
            {
                double hours = days * 24.0;
                int roundedHours = (int)Math.Round(hours);
                if (roundedHours <= 0)
                    return "accepted just now";
                if (roundedHours == 1)
                    return "accepted ~1 hour ago";
                return "accepted ~" + roundedHours + " hours ago";
            }
        }

        #endregion

        #region Quest Key Pawns

        // Extract short names of pawns directly referenced by this quest's parts (pawn/pawns fields).
        // Returns a comma-separated list, or empty string if none.
        // Uses cached FieldInfo lookups for QuestPart subclass fields.
        public static string GetQuestKeyPawnNames(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            var parts = quest.PartsListForReading;
            if (parts == null || parts.Count == 0)
                return string.Empty;

            var pawns = new List<Pawn>();
            var cache = GetCache();

            foreach (var partObj in parts)
            {
                if (partObj == null)
                    continue;

                Type partType = partObj.GetType();

                // 1) Single pawn field:  "pawn"
                try
                {
                    var pawnField = cache?.GetField(partType, "pawn")
                        ?? partType.GetField("pawn", FallbackBindingFlags);

                    if (pawnField != null)
                    {
                        Pawn pawn = pawnField.GetValue(partObj) as Pawn;
                        if (pawn != null && !pawns.Contains(pawn))
                            pawns.Add(pawn);
                    }
                }
                catch
                {
                    // ignore
                }

                // 2) List field: "pawns"
                try
                {
                    var pawnsField = cache?.GetField(partType, "pawns")
                        ?? partType.GetField("pawns", FallbackBindingFlags);

                    if (pawnsField != null)
                    {
                        var pawnListObj = pawnsField.GetValue(partObj) as System.Collections.IList;
                        if (pawnListObj != null)
                        {
                            foreach (object o in pawnListObj)
                            {
                                if (o is Pawn p && !pawns.Contains(p))
                                    pawns.Add(p);
                            }
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }

            if (pawns.Count == 0)
                return string.Empty;

            var names = new List<string>();
            foreach (var p in pawns)
            {
                if (p == null)
                    continue;

                string name = null;

                try
                {
                    name = p.LabelShortCap;
                }
                catch
                {
                    // ignore
                }

                if (name.NullOrEmpty())
                {
                    try
                    {
                        name = p.Name?.ToStringShort;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (!name.NullOrEmpty())
                    names.Add(name);
            }

            if (names.Count == 0)
                return string.Empty;

            return string.Join(", ", names);
        }

        #endregion

        #region Quest Affects Map

        // True if this quest should be considered as affecting the given map.
        // Results are cached for performance since this is called repeatedly.
        public static bool QuestAffectsMap(Quest quest, Map map)
        {
            if (quest == null || map == null)
                return false;

            // Cache only works when a game is running
            var game = Current.Game;
            var tickManager = Find.TickManager;

            if (game != null && tickManager != null)
            {
                int questId = quest.id;
                if (questId >= 0)
                {
                    int mapUid = map.uniqueID;

                    var cache = GetCache();
                    if (cache != null)
                    {
                        if (cache.TryGetQuestAffectsMap(questId, mapUid, out bool cached))
                            return cached;

                        bool computed = QuestAffectsMap_Uncached(quest, map);
                        cache.StoreQuestAffectsMap(questId, mapUid, computed);
                        return computed;
                    }
                }
            }

            // Fallback:  compute without caching
            return QuestAffectsMap_Uncached(quest, map);
        }

        private static bool QuestAffectsMap_Uncached(Quest quest, Map map)
        {
            if (quest == null || map == null)
                return false;

            MapParent mapParent = map.info?.parent;
            int mapTile = map.Tile;

            var parts = quest.PartsListForReading;
            var cache = GetCache();

            // 1. Check QuestLookTargets from filtered parts
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    if (part == null) continue;

                    // Skip auxiliary parts that don't indicate quest location
                    if (ShouldSkipQuestPart(part))
                        continue;

                    try
                    {
                        var partLookTargets = part.QuestLookTargets;
                        if (partLookTargets != null)
                        {
                            foreach (var target in partLookTargets)
                            {
                                // Check if target has a map and it matches our map
                                if (target.IsMapTarget && target.Map == map)
                                    return true;

                                // Check if target WorldObject is this map's parent
                                if (target.HasWorldObject && mapParent != null)
                                {
                                    var targetParent = target.WorldObject as MapParent;
                                    if (targetParent != null && targetParent == mapParent)
                                        return true;
                                }

                                // Check tile match
                                int targetTile = target.Tile;
                                if (targetTile >= 0 && targetTile == mapTile)
                                    return true;
                            }
                        }
                    }
                    catch
                    {
                        // Silently skip - QuestLookTargets enumeration can throw during map generation
                    }
                }
            }

            // 2. Check QuestParts for worldObject/site fields that may reference this map
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    if (part == null) continue;

                    // Skip auxiliary parts that don't indicate quest location
                    if (ShouldSkipQuestPart(part))
                        continue;

                    Type partType = part.GetType();

                    // Check "worldObject" field
                    try
                    {
                        FieldInfo worldObjectField = cache?.GetField(partType, "worldObject")
                            ?? partType.GetField("worldObject", FallbackBindingFlags);

                        if (worldObjectField != null)
                        {
                            var wo = worldObjectField.GetValue(part) as WorldObject;
                            if (wo is MapParent mp)
                            {
                                if (mp == mapParent)
                                    return true;
                                try
                                {
                                    if (mp.HasMap && mp.Map == map)
                                        return true;
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }

                    // Check "site" field (used by QuestPart_DistressCallAmbush, etc.)
                    try
                    {
                        FieldInfo siteField = cache?.GetField(partType, "site")
                            ?? partType.GetField("site", FallbackBindingFlags);

                        if (siteField != null)
                        {
                            var site = siteField.GetValue(part) as MapParent;
                            if (site != null)
                            {
                                if (site == mapParent)
                                    return true;
                                try
                                {
                                    if (site.HasMap && site.Map == map)
                                        return true;
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 3. Check individual quest parts for mapParent field/property
            if (mapParent == null)
                return false;

            if (parts != null && parts.Count > 0)
            {
                foreach (var part in parts)
                {
                    if (part == null)
                        continue;

                    if (ShouldSkipQuestPart(part))
                        continue;

                    // Check part's QuestSelectTargets
                    try
                    {
                        var partSelectTargets = part.QuestSelectTargets;
                        if (partSelectTargets != null)
                        {
                            foreach (var target in partSelectTargets)
                            {
                                if (target.IsMapTarget && target.Map == map)
                                    return true;

                                if (target.HasWorldObject && mapParent != null)
                                {
                                    var targetParent = target.WorldObject as MapParent;
                                    if (targetParent != null && targetParent == mapParent)
                                        return true;
                                }
                            }
                        }
                    }
                    catch { }

                    Type partType = part.GetType();
                    MapParent partParent = null;

                    // Try field "mapParent"
                    try
                    {
                        FieldInfo mapParentField = cache?.GetField(partType, "mapParent")
                            ?? partType.GetField("mapParent", FallbackBindingFlags);

                        if (mapParentField != null)
                            partParent = mapParentField.GetValue(part) as MapParent;
                    }
                    catch { }

                    // Try property "MapParent" if field not found
                    if (partParent == null)
                    {
                        try
                        {
                            var mapParentProp = partType.GetProperty("MapParent", FallbackBindingFlags);
                            if (mapParentProp != null)
                                partParent = mapParentProp.GetValue(part) as MapParent;
                        }
                        catch { }
                    }

                    if (partParent != null)
                    {
                        if (partParent == mapParent)
                            return true;

                        // Check if MapParent has a map property pointing to our map
                        try
                        {
                            if (partParent.HasMap && partParent.Map == map)
                                return true;
                        }
                        catch
                        {
                            // MapParent not fully initialized, skip
                        }
                    }
                }
            }

            return false;
        }

        private static bool ShouldSkipQuestPart(QuestPart part)
        {
            if (part == null) return true;

            string partTypeName = part.GetType().Name;

            // Skip parts that don't indicate actual quest location
            // These are auxiliary parts for rewards, notifications, or requirements
            return partTypeName == "QuestPart_DropPods" ||
                   partTypeName == "QuestPart_RequirementsToAcceptPlanetLayer" ||
                   partTypeName == "QuestPart_RequirementsToAccept" ||
                   partTypeName == "QuestPart_GiveRewards" ||
                   partTypeName == "QuestPart_Letter" ||
                   partTypeName == "QuestPart_Notify_PlayerRaidedSomeone" ||
                   partTypeName == "QuestPart_Choice";
        }

        #endregion
    }
}