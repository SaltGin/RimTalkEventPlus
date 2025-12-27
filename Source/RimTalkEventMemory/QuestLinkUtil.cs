using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimTalkEventPlus
{
    public static class QuestLinkUtil
    {
        private static bool TryGetQuestId(Quest quest, out int questId)
        {
            questId = -1;
            if (quest == null) return false;

            var trav = Traverse.Create(quest);

            try
            {
                questId = trav.Field("id").GetValue<int>();
                if (questId >= 0) return true;
            }
            catch
            {
                // ignore
            }

            try
            {
                questId = trav.Property("Id").GetValue<int>();
                if (questId >= 0) return true;
            }
            catch
            {
                // ignore
            }

            return false;
        }

        /// Try to read description from a Quest.
        public static string TryGetQuestDescription(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            var trav = Traverse.Create(quest);
            object descObj = null;

            try
            {
                descObj = trav.Field("description").GetValue<object>();
            }
            catch
            {
                // ignore
            }

            if (descObj == null)
            {
                try
                {
                    descObj = trav.Property("Description").GetValue<object>();
                }
                catch
                {
                    // ignore
                }
            }

            return descObj?.ToString() ?? string.Empty;
        }

        /// Try to get a short label/title for a Quest.
        public static string TryGetQuestLabel(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            var trav = Traverse.Create(quest);

            try
            {
                string nameField = trav.Field("name").GetValue<string>();
                if (!nameField.NullOrEmpty())
                    return nameField;
            }
            catch
            {
                // ignore
            }

            try
            {
                string nameProp = trav.Property("Name").GetValue<string>();
                if (!nameProp.NullOrEmpty())
                    return nameProp;
            }
            catch
            {
                // ignore
            }

            // Fallback: first line of description
            string desc = TryGetQuestDescription(quest);
            if (!desc.NullOrEmpty())
            {
                int nl = desc.IndexOf('\n');
                return nl > 0 ? desc.Substring(0, nl) : desc;
            }

            return "Quest";
        }

        /// True if this quest is flagged as hidden (e.g. <hidden>True</hidden> in the save).
        public static bool IsQuestHidden(Quest quest)
        {
            if (quest == null)
                return false;

            var trav = Traverse.Create(quest);

            // Try backing field
            try
            {
                if (trav.Field("hidden").GetValue<bool>())
                    return true;
            }
            catch
            {
                // ignore
            }

            // Try property, if RimWorld exposes it that way
            try
            {
                if (trav.Property("Hidden").GetValue<bool>())
                    return true;
            }
            catch
            {
                // ignore
            }

            return false;
        }


        /// True if this quest is in the "Ongoing" state and not ended.
        public static bool IsQuestOngoing(Quest quest)
        {
            // Direct enum comparison
            return quest?.State == QuestState.Ongoing;
        }

        /// Returns a short marker like "accepted ~1.3 days ago" or "accepted ~5 hours ago"
        /// based on acceptanceTick vs current game ticks. Empty string if not accepted.
        public static string GetQuestAcceptedAgeMarker(Quest quest)
        {
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

        /// Extract short names of pawns directly referenced by this quest's parts (pawn / pawns fields).
        /// Returns a comma-separated list, or empty string if none.
        public static string GetQuestKeyPawnNames(Quest quest)
        {
            if (quest == null)
                return string.Empty;

            var parts = quest.PartsListForReading;

            if (parts == null || parts.Count == 0)
                return string.Empty;

            var pawns = new List<Pawn>();

            foreach (var partObj in parts)
            {
                if (partObj == null)
                    continue;

                var partTrav = Traverse.Create(partObj);

                // 1) Single pawn field: "pawn"
                try
                {
                    Pawn pawn = partTrav.Field("pawn").GetValue<Pawn>();
                    if (pawn != null && !pawns.Contains(pawn))
                    {
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
                    var pawnListObj = partTrav.Field("pawns").GetValue<IList>();
                    if (pawnListObj != null)
                    {
                        foreach (object o in pawnListObj)
                        {
                            if (o is Pawn p && !pawns.Contains(p))
                            {
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
                {
                    names.Add(name);
                }
            }

            if (names.Count == 0)
                return string.Empty;

            return string.Join(", ", names);
        }

        /// True if this quest should be considered as affecting the given map.
        ///
        ///  1) LookTargets first:
        ///     - If any target has HasMap && Map == this map, or references this map's parent/site/tile -> true.
        ///  2) Quest parts:
        ///     - If any part holds a MapParent/Map pointing to this map's parent or this map -> true.
        ///
        ///  - We deliberately ignore some "auxiliary" references that don't mean the quest really
        ///    "happens" on that map, e.g. threat-scaling fields like useMapParentThreatPoints.
        ///  - We also skip parts like QuestPart_DropPods that just use a map as a drop location.
        public static bool QuestAffectsMap(Quest quest, Map map)
        {
            if (quest == null || map == null)
                return false;

            // Cache only works when a game is running and ticks are available.
            var game = Current.Game;
            var tickManager = Find.TickManager;

            if (game != null && tickManager != null)
            {
                int questId;
                if (TryGetQuestId(quest, out questId) && questId >= 0)
                {
                    int mapUid = map.uniqueID;

                    var comp = game.GetComponent<QuestAffectsMapCacheComponent>();
                    if (comp != null)
                    {
                        if (comp.TryGet(questId, mapUid, out bool cached))
                        {
                            //Log.Message($"[RimTalkEventPlus] QuestAffectsMap cache HIT: questId={questId}, mapUid={mapUid}, affects={cached}");
                            return cached;
                        }

                        //Log.Message($"[RimTalkEventPlus] QuestAffectsMap cache MISS: questId={questId}, mapUid={mapUid} -> recompute");
                        bool computed = QuestAffectsMap_Uncached(quest, map);
                        comp.Store(questId, mapUid, computed);
                        //Log.Message($"[RimTalkEventPlus] QuestAffectsMap cache STORED: questId={questId}, mapUid={mapUid}, affects={computed}");
                        return computed;
                    }
                }
            }

            // Fallback: original logic without caching.
            return QuestAffectsMap_Uncached(quest, map);
        }

        private static bool QuestAffectsMap_Uncached(Quest quest, Map map)
        {
            if (quest == null || map == null)
                return false;

            MapParent mapParent = map.info?.parent;
            int mapTile = map.Tile;

            // 1. Check quest-level LookTargets
            var questLookTargets = quest.QuestLookTargets;
            if (questLookTargets != null)
            {
                foreach (var target in questLookTargets)
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
                    var targetTile = target.Tile;
                    if (targetTile.Valid && targetTile == mapTile)
                        return true;
                }
            }

            // 2. Check individual quest parts
            if (mapParent == null)
                return false;

            var parts = quest.PartsListForReading;

            if (parts != null && parts.Count > 0)
            {
                foreach (var part in parts)
                {
                    if (part == null)
                        continue;

                    // Skip parts that don't indicate actual quest location
                    string partTypeName = part.GetType().Name;
                    if (partTypeName == "QuestPart_DropPods" ||
                        partTypeName == "QuestPart_RequirementsToAcceptPlanetLayer")
                    {
                        continue;
                    }

                    // Check part's QuestLookTargets
                    var partLookTargets = part.QuestLookTargets;
                    if (partLookTargets != null)
                    {
                        foreach (var target in partLookTargets)
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

                    // Check part's QuestSelectTargets
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

                    // Still need reflection for private "mapParent" field/property
                    var partTrav = Traverse.Create(part);
                    MapParent partParent = null;

                    // Try field "mapParent"
                    try
                    {
                        partParent = partTrav.Field("mapParent").GetValue<MapParent>();
                    }
                    catch { }

                    // Try property "MapParent"
                    if (partParent == null)
                    {
                        try
                        {
                            partParent = partTrav.Property("MapParent").GetValue<MapParent>();
                        }
                        catch { }
                    }

                    if (partParent != null)
                    {
                        if (partParent == mapParent)
                            return true;

                        // Check if MapParent has a map property pointing to our map
                        if (partParent.HasMap && partParent.Map == map)
                            return true;
                    }

                    // More generic scan: look for any MapParent / Map fields or properties on this part.
                    Type partType = part.GetType();

                    // Fields
                    FieldInfo[] fields = null;
                    try
                    {
                        fields = partType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    catch { }

                    if (fields != null)
                    {
                        foreach (var field in fields)
                        {
                            // Skip threat-scaling-only reference
                            if (string.Equals(field.Name, "useMapParentThreatPoints", StringComparison.OrdinalIgnoreCase))
                                continue;

                            Type fType = field.FieldType;
                            object value = null;
                            try
                            {
                                value = field.GetValue(part);
                            }
                            catch { continue; }

                            if (value == null)
                                continue;

                            // Any MapParent-like field
                            if (typeof(MapParent).IsAssignableFrom(fType))
                            {
                                var mp = value as MapParent;
                                if (mp != null)
                                {
                                    if (mp == mapParent)
                                        return true;

                                    if (mp.HasMap && mp.Map == map)
                                        return true;
                                }
                            }
                            else if (typeof(Map).IsAssignableFrom(fType))
                            {
                                var m = value as Map;
                                if (m == map)
                                    return true;
                            }
                        }
                    }

                    // Properties
                    PropertyInfo[] props = null;
                    try
                    {
                        props = partType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    catch { }

                    if (props != null)
                    {
                        foreach (var prop in props)
                        {
                            // Skip indexers
                            if (prop.GetIndexParameters().Length != 0)
                                continue;

                            // Skip difficulty reference
                            if (string.Equals(prop.Name, "useMapParentThreatPoints", StringComparison.OrdinalIgnoreCase))
                                continue;

                            Type pType = prop.PropertyType;
                            object value = null;
                            try
                            {
                                if (!prop.CanRead)
                                    continue;
                                value = prop.GetValue(part, null);
                            }
                            catch { continue; }

                            if (value == null)
                                continue;

                            if (typeof(MapParent).IsAssignableFrom(pType))
                            {
                                var mp = value as MapParent;
                                if (mp != null)
                                {
                                    if (mp == mapParent)
                                        return true;

                                    if (mp.HasMap && mp.Map == map)
                                        return true;
                                }
                            }
                            else if (typeof(Map).IsAssignableFrom(pType))
                            {
                                var m = value as Map;
                                if (m == map)
                                    return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
