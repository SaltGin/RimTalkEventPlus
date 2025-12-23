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
            if (quest == null)
                return false;

            var trav = Traverse.Create(quest);
            object stateObj = null;

            try
            {
                stateObj = trav.Field("state").GetValue<object>();
            }
            catch
            {
                // ignore
            }

            if (stateObj == null)
            {
                try
                {
                    stateObj = trav.Property("State").GetValue<object>();
                }
                catch
                {
                    // ignore
                }
            }

            if (stateObj == null)
                return false;

            string stateString = stateObj.ToString();

            // If there is an "ended" flag, treat it as authoritative
            try
            {
                var travEnded = Traverse.Create(quest);
                bool? ended = null;

                try
                {
                    ended = travEnded.Field("ended").GetValue<bool?>();
                }
                catch
                {
                    // ignore
                }

                if (ended == null)
                {
                    try
                    {
                        ended = travEnded.Property("Ended").GetValue<bool?>();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (ended.HasValue && ended.Value)
                    return false;
            }
            catch
            {
                // ignore
            }

            // Default: rely on state == "Ongoing"
            return string.Equals(stateString, "Ongoing", StringComparison.OrdinalIgnoreCase);
        }

        /// Returns a short marker like "accepted ~1.3 days ago" or "accepted ~5 hours ago"
        /// based on acceptanceTick vs current game ticks. Empty string if not accepted.
        public static string GetQuestAcceptedAgeMarker(Quest quest)
        {
            if (quest == null || Find.TickManager == null)
                return string.Empty;

            int acceptanceTick = -1;
            var trav = Traverse.Create(quest);

            try
            {
                acceptanceTick = trav.Field("acceptanceTick").GetValue<int>();
            }
            catch
            {
                // ignore
            }

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

            var questTrav = Traverse.Create(quest);
            IList parts = null;
            try
            {
                parts = questTrav.Field("parts").GetValue<IList>();
            }
            catch
            {
                // ignore
            }

            if (parts == null)
                return string.Empty;

            var pawns = new List<Pawn>();

            foreach (object partObj in parts)
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
                    int nowTick = tickManager.TicksGame;

                    var comp = game.GetComponent<QuestAffectsMapCacheComponent>();
                    if (comp != null)
                    {
                        if (comp.TryGet(questId, mapUid, nowTick, out bool cached))
                        {
                            //Log.Message($"[RimTalkEventPlus] QuestAffectsMap cache HIT: questId={questId}, mapUid={mapUid}, affects={cached}");
                            return cached;
                        }

                        //Log.Message($"[RimTalkEventPlus] QuestAffectsMap cache MISS: questId={questId}, mapUid={mapUid} -> recompute");
                        bool computed = QuestAffectsMap_Uncached(quest, map);
                        comp.Store(questId, mapUid, nowTick, computed);
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
            bool hasAnyTarget;
            bool hasAnyMapTarget;
            bool affectsByTargets = QuestAffectsMapByLookTargetsDetailed(
                quest,
                map,
                out hasAnyTarget,
                out hasAnyMapTarget
            );

            // LookTargets explicitly reference this map (or its parent/tile) -> definitely affects this map.
            if (affectsByTargets)
                return true;

            // Fall back to quest parts' association with this map or its parent.
            MapParent mapParent = map.info != null ? map.info.parent : null;
            if (mapParent == null)
                return false;

            var questTrav = Traverse.Create(quest);
            IList parts = null;
            try
            {
                parts = questTrav.Field("parts").GetValue<IList>();
            }
            catch
            {
                // ignore
            }

            if (parts != null)
            {
                foreach (object partObj in parts)
                {
                    if (partObj == null)
                        continue;

                    // Skip world-site related or reward-only parts that just use
                    // the colony map as a drop location / requirement,
                    // not as the actual quest location.
                    //
                    // QuestPart_DropPods typically only "drops" onto a map.
                    // QuestPart_RequirementsToAcceptPlanetLayer controls whether
                    // a quest can be accepted, often referencing the colony map in
                    // a non-location way.
                    string partTypeName = partObj.GetType().Name;
                    if (partTypeName == "QuestPart_DropPods" ||
                        partTypeName == "QuestPart_RequirementsToAcceptPlanetLayer")
                    {
                        continue;
                    }

                    var partTrav = Traverse.Create(partObj);
                    MapParent partParent = null;

                    // Quick path: try field "mapParent"
                    try
                    {
                        partParent = partTrav.Field("mapParent").GetValue<MapParent>();
                    }
                    catch
                    {
                        // ignore
                    }

                    // Try property "MapParent"
                    if (partParent == null)
                    {
                        try
                        {
                            partParent = partTrav.Property("MapParent").GetValue<MapParent>();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    if (partParent != null)
                    {
                        // Direct parent match
                        if (partParent == mapParent)
                            return true;

                        // Some MapParent types expose Map; check that too
                        try
                        {
                            Map partMap = Traverse.Create(partParent).Property("Map").GetValue<Map>();
                            if (partMap == map)
                                return true;
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    // More generic scan: look for any MapParent / Map fields or properties on this part.
                    Type partType = partObj.GetType();

                    // Fields
                    FieldInfo[] fields = null;
                    try
                    {
                        fields = partType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    catch
                    {
                        // ignore
                    }

                    if (fields != null)
                    {
                        foreach (var field in fields)
                        {
                            // Skip threat-scaling-only reference; it does not mean the quest
                            // "happens" on that map. Example: useMapParentThreatPoints on
                            // RelicHunt subquest generators, which just uses the colony map
                            // as a difficulty reference.
                            if (string.Equals(field.Name, "useMapParentThreatPoints", StringComparison.OrdinalIgnoreCase))
                                continue;

                            Type fType = field.FieldType;
                            object value = null;
                            try
                            {
                                value = field.GetValue(partObj);
                            }
                            catch
                            {
                                continue;
                            }

                            if (value == null)
                                continue;

                            // Any MapParent-like field (e.g. Site, Settlement, custom subclasses)
                            if (typeof(MapParent).IsAssignableFrom(fType))
                            {
                                var mp = value as MapParent;
                                if (mp != null)
                                {
                                    if (mp == mapParent)
                                        return true;

                                    try
                                    {
                                        Map partMap2 = Traverse.Create(mp).Property("Map").GetValue<Map>();
                                        if (partMap2 == map)
                                            return true;
                                    }
                                    catch
                                    {
                                        // ignore
                                    }
                                }
                            }
                            // Any Map field directly
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
                    catch
                    {
                        // ignore
                    }

                    if (props != null)
                    {
                        foreach (var prop in props)
                        {
                            // Skip indexers
                            if (prop.GetIndexParameters().Length != 0)
                                continue;

                            // Same reasoning as for fields: useMapParentThreatPoints is a
                            // difficulty reference, not a location binding.
                            if (string.Equals(prop.Name, "useMapParentThreatPoints", StringComparison.OrdinalIgnoreCase))
                                continue;

                            Type pType = prop.PropertyType;
                            object value = null;
                            try
                            {
                                if (!prop.CanRead)
                                    continue;
                                value = prop.GetValue(partObj, null);
                            }
                            catch
                            {
                                continue;
                            }

                            if (value == null)
                                continue;

                            if (typeof(MapParent).IsAssignableFrom(pType))
                            {
                                var mp = value as MapParent;
                                if (mp != null)
                                {
                                    if (mp == mapParent)
                                        return true;

                                    try
                                    {
                                        Map partMap2 = Traverse.Create(mp).Property("Map").GetValue<Map>();
                                        if (partMap2 == map)
                                            return true;
                                    }
                                    catch
                                    {
                                        // ignore
                                    }
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

        /// Detailed LookTargets check.
        ///  - returns true if a target is on this map or references its parent/site/tile,
        ///  - sets hasAnyTarget if there was at least one target,
        ///  - sets hasAnyMapTarget if at least one target had HasMap == true.
        private static bool QuestAffectsMapByLookTargetsDetailed(
            Quest quest,
            Map map,
            out bool hasAnyTarget,
            out bool hasAnyMapTarget)
        {
            hasAnyTarget = false;
            hasAnyMapTarget = false;

            if (quest == null || map == null)
                return false;

            // Anchor on the current map's parent and tile so we can also match
            // site/world-object targets that reference this quest map indirectly.
            MapParent mapParent = map.info != null ? map.info.parent : null;
            int mapTile = -1;
            try
            {
                mapTile = mapParent != null ? mapParent.Tile : map.Tile;
            }
            catch
            {
                // ignore
            }

            bool anyTarget = false;
            bool anyMapTargetLocal = false;
            bool found = false;

            var questTrav = Traverse.Create(quest);
            object lookTargetsObj = null;

            // Try property "LookTargets" first, then field "lookTargets"
            try
            {
                lookTargetsObj = questTrav.Property("LookTargets").GetValue<object>();
            }
            catch
            {
                // ignore
            }

            if (lookTargetsObj == null)
            {
                try
                {
                    lookTargetsObj = questTrav.Field("lookTargets").GetValue<object>();
                }
                catch
                {
                    // ignore
                }
            }

            if (lookTargetsObj == null)
                return false;

            var ltTrav = Traverse.Create(lookTargetsObj);

            // Local helper that updates local flags instead of out parameters
            bool ProcessTarget(object targetObj)
            {
                if (targetObj == null)
                    return false;

                anyTarget = true;

                var tTrav = Traverse.Create(targetObj);
                bool hasMapLocal = false;
                Map targetMap = null;

                try { hasMapLocal = tTrav.Property("HasMap").GetValue<bool>(); } catch { }
                try { targetMap = tTrav.Property("Map").GetValue<Map>(); } catch { }

                if (hasMapLocal)
                {
                    anyMapTargetLocal = true;
                    if (targetMap == map)
                        return true;
                }

                // Also check for world objects / sites tied to this map's parent or tile.
                // This lets quests that only target a site (without exposing the map)
                // still be detected as affecting the quest map that site owns.
                WorldObject worldObject = null;
                MapParent targetParent = null;
                int targetTile = -1;

                try { worldObject = tTrav.Property("WorldObject").GetValue<WorldObject>(); } catch { }

                if (worldObject != null)
                {
                    targetParent = worldObject as MapParent;
                    try { targetTile = worldObject.Tile; } catch { }
                }

                if (targetParent == null)
                {
                    try { targetParent = tTrav.Property("MapParent").GetValue<MapParent>(); } catch { }
                }

                if (targetTile < 0)
                {
                    try { targetTile = tTrav.Property("Tile").GetValue<int>(); } catch { }
                }

                if (targetParent != null && mapParent != null && targetParent == mapParent)
                    return true;

                if (targetTile >= 0 && mapTile >= 0 && targetTile == mapTile)
                    return true;

                return false;
            }

            // 1) PrimaryTarget
            try
            {
                object primaryObj = ltTrav.Property("PrimaryTarget").GetValue<object>();
                if (ProcessTarget(primaryObj))
                    found = true;
            }
            catch
            {
                // ignore
            }

            // 2) AllTargets / Targets, only if we didn't already find a match
            if (!found)
            {
                object allTargetsObj = null;

                try
                {
                    allTargetsObj = ltTrav.Property("AllTargets").GetValue<object>();
                }
                catch
                {
                    // ignore
                }

                if (allTargetsObj == null)
                {
                    try
                    {
                        allTargetsObj = ltTrav.Property("Targets").GetValue<object>();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                var enumerable = allTargetsObj as IEnumerable;
                if (enumerable != null)
                {
                    foreach (object t in enumerable)
                    {
                        if (ProcessTarget(t))
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }

            // Copy local flags into out parameters before returning
            hasAnyTarget = anyTarget;
            hasAnyMapTarget = anyMapTargetLocal;
            return found;
        }
    }
}
