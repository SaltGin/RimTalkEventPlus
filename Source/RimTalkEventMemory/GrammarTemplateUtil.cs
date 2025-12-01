using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.Grammar;

namespace RimTalkEventPlus
{
    /// Helper for reading grammar templates (rulesStrings) from RulePacks
    /// via reflection. This lets us see the localized "[token] + literal"
    /// templates for any language without guessing or hardcoding text.
    public static class GrammarTemplateUtil
    {
        /// Private field on Verse.Grammar.RulePack that holds the raw
        /// "key->value" rule strings (per language).
        private static readonly FieldInfo RulePack_rulesStrings =
            AccessTools.Field(typeof(RulePack), "rulesStrings");

        /// Parsed representation of a single "key->output" rule string.
        /// Example raw: "questDescription->[approachInfo][claimInfo]..."
        public struct RuleStringData
        {
            /// The original raw line from rulesStrings.
            public string Raw;

            /// The left side before "->", e.g. "questDescription"
            /// or "claimInfo(lodgerCount>=2)".
            public string Key;

            /// The right side after "->", the template that contains
            /// literals and [tokens] in the current language.
            public string Output;
        }

        /// Returns the parsed rule strings (key + output) for a RulePack,
        /// using reflection to read the private rulesStrings list.
        /// Returns null if the field is not found or the pack is null.
        public static List<RuleStringData> GetRuleStrings(RulePack pack)
        {
            if (pack == null || RulePack_rulesStrings == null)
                return null;

            var rawList = RulePack_rulesStrings.GetValue(pack) as List<string>;
            if (rawList == null || rawList.Count == 0)
                return null;

            var result = new List<RuleStringData>(rawList.Count);

            foreach (string raw in rawList)
            {
                if (raw.NullOrEmpty())
                    continue;

                int arrowIndex = raw.IndexOf("->", StringComparison.Ordinal);
                if (arrowIndex < 0)
                    continue;

                string key = raw.Substring(0, arrowIndex).Trim();
                string output = raw.Substring(arrowIndex + 2); // keep as-is, may include tokens

                if (key.NullOrEmpty() || output.NullOrEmpty())
                    continue;

                result.Add(new RuleStringData
                {
                    Raw = raw,
                    Key = key,
                    Output = output
                });
            }

            return result;
        }

        /// Convenience: get the questDescription template RHS for a
        /// specific QuestScriptDef, in the current language.
        /// Returns the output part (after "questDescription->") or null.
        public static string GetQuestDescriptionTemplateRhs(QuestScriptDef questDef)
        {
            if (questDef == null || questDef.questDescriptionRules == null)
                return null;

            var ruleStrings = GetRuleStrings(questDef.questDescriptionRules);
            if (ruleStrings == null)
                return null;

            foreach (var rs in ruleStrings)
            {
                // We match anything that starts with "questDescription",
                // which covers "questDescription" and variants with conditions.
                if (rs.Key.StartsWith("questDescription", StringComparison.Ordinal))
                    return rs.Output;
            }

            return null;
        }
    }
}
