using RimWorld;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalkEventPlus
{
    public static class EventTextCompressionUtil
    {
        private static List<RimTalkCompressionTemplateDef> _templates;

        private static List<RimTalkCompressionTemplateDef> Templates
        {
            get
            {
                if (_templates == null)
                    _templates = DefDatabase<RimTalkCompressionTemplateDef>.AllDefsListForReading;
                return _templates;
            }
        }

        /// Main entry point: given an ongoing event snapshot, try to produce
        /// a compressed body string. Returns null if no compression applies.
        public static string TryGetCompressedBody(OngoingEventSnapshot snapshot)
        {
            if (snapshot == null)
                return null;

            // Base body selection first so we can always fall back to it.
            string baseBody = !snapshot.QuestDescription.NullOrEmpty()
                ? snapshot.QuestDescription
                : snapshot.Body;

            // If there's nothing to compress, just return as-is.
            if (baseBody.NullOrEmpty())
                return baseBody;

            // If we don't know the source def, we can't use any templates.
            if (snapshot.SourceDefName.NullOrEmpty())
                return baseBody;

            // 1) Special-case dynamic compressor(s) for specific roots.
            if (!snapshot.Kind.NullOrEmpty() &&
                snapshot.Kind.Equals("Quest", StringComparison.OrdinalIgnoreCase) &&
                snapshot.SourceDefName.Equals("Hospitality_Refugee", StringComparison.OrdinalIgnoreCase))
            {
                string refugeeCompressed = TryCompressHospitalityRefugee(snapshot.SourceDefName, baseBody, snapshot.Kind);
                if (!refugeeCompressed.NullOrEmpty())
                    return refugeeCompressed;
            }

            // 2) Fallback to simple template-based override, if any is defined.
            var template = TryGetTemplate(snapshot.SourceDefName, snapshot.Kind);
            if (template != null && !template.compressedBody.NullOrEmpty())
                return template.compressedBody;

            // 3) If nothing applied, return the original (uncompressed) text.
            return baseBody;
        }

        /// Find a compression template for the given sourceDefName/kind.
        /// Returns null if none match.
        public static RimTalkCompressionTemplateDef TryGetTemplate(string sourceDefName, string kind)
        {
            if (sourceDefName.NullOrEmpty())
                return null;

            if (Templates == null || Templates.Count == 0)
                return null;

            string kindNorm = kind ?? string.Empty;
            RimTalkCompressionTemplateDef fallback = null;

            foreach (var def in Templates)
            {
                if (def == null || def.sourceDefName.NullOrEmpty())
                    continue;

                if (!string.Equals(def.sourceDefName, sourceDefName, StringComparison.OrdinalIgnoreCase))
                    continue;

                // kind not specified on the def → generic match, keep as fallback
                if (def.kind.NullOrEmpty())
                {
                    if (fallback == null)
                        fallback = def;
                    continue;
                }

                // kind specified → require exact match (case-insensitive)
                if (string.Equals(def.kind, kindNorm, StringComparison.OrdinalIgnoreCase))
                    return def;
            }

            return fallback;
        }

        #region Template parsing helpers

        /// Minimal representation of one segment in a template RHS:
        /// either a token like [foo] or a plain literal.
        private struct TemplateSegment
        {
            public bool IsToken;
            public string Text;
        }

        /// Split a template RHS into token and literal segments.
        /// Tokens are anything inside [...] brackets.
        private static List<TemplateSegment> ParseTemplateSegments(string rhs)
        {
            if (rhs.NullOrEmpty())
                return null;

            var result = new List<TemplateSegment>();
            StringBuilder literalBuffer = null;

            int i = 0;
            int len = rhs.Length;

            void FlushLiteral()
            {
                if (literalBuffer != null && literalBuffer.Length > 0)
                {
                    result.Add(new TemplateSegment
                    {
                        IsToken = false,
                        Text = literalBuffer.ToString()
                    });
                    literalBuffer.Clear();
                }
            }

            while (i < len)
            {
                char c = rhs[i];
                if (c == '[')
                {
                    // Start of a token; flush any pending literal.
                    if (literalBuffer == null)
                        literalBuffer = new StringBuilder();
                    FlushLiteral();

                    int end = rhs.IndexOf(']', i + 1);
                    if (end < 0)
                    {
                        // Malformed: treat the rest as literal.
                        if (literalBuffer == null)
                            literalBuffer = new StringBuilder();
                        literalBuffer.Append(rhs.Substring(i));
                        break;
                    }

                    string tokenName = rhs.Substring(i + 1, end - i - 1);
                    result.Add(new TemplateSegment
                    {
                        IsToken = true,
                        Text = tokenName
                    });
                    i = end + 1;
                }
                else
                {
                    if (literalBuffer == null)
                        literalBuffer = new StringBuilder();
                    literalBuffer.Append(c);
                    i++;
                }
            }

            if (literalBuffer != null && literalBuffer.Length > 0)
            {
                result.Add(new TemplateSegment
                {
                    IsToken = false,
                    Text = literalBuffer.ToString()
                });
            }

            return result;
        }

        /// From a parsed template, find the literal that appears
        /// immediately before and after a given token occurrence.
        /// We walk backwards/forwards to find the nearest non-empty literals.
        private static bool TryGetNeighborLiteralsForToken(
            List<TemplateSegment> segments,
            string tokenName,
            out string beforeLiteral,
            out string afterLiteral)
        {
            beforeLiteral = null;
            afterLiteral = null;

            if (segments == null || segments.Count == 0 || tokenName.NullOrEmpty())
                return false;

            int index = -1;
            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].IsToken &&
                    string.Equals(segments[i].Text, tokenName, StringComparison.Ordinal))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return false;

            // find literal before
            for (int i = index - 1; i >= 0; i--)
            {
                if (!segments[i].IsToken && !segments[i].Text.NullOrEmpty())
                {
                    beforeLiteral = segments[i].Text;
                    break;
                }
            }

            // find literal after
            for (int i = index + 1; i < segments.Count; i++)
            {
                if (!segments[i].IsToken && !segments[i].Text.NullOrEmpty())
                {
                    afterLiteral = segments[i].Text;
                    break;
                }
            }

            return !beforeLiteral.NullOrEmpty() && !afterLiteral.NullOrEmpty();
        }

        #endregion

        #region Hospitality_Refugee compression

        /// Main compressor for the Hospitality_Refugee quest.
        private static string TryCompressHospitalityRefugee(string sourceDefName, string resolvedDescription, string kind)
        {
            if (resolvedDescription.NullOrEmpty())
                return null;

            // 1) Get the quest script definition
            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(sourceDefName);
            if (questDef == null)
                return null;

            // 2) Get the questDescription RHS template for the current language
            string questDescriptionRhs = GrammarTemplateUtil.GetQuestDescriptionTemplateRhs(questDef);
            if (questDescriptionRhs.NullOrEmpty())
                return null;

            // 3) Parse into template segments
            var segments = ParseTemplateSegments(questDescriptionRhs);
            if (segments == null || segments.Count == 0)
                return null;

            // 4) From the template, find the literals around [questDurationTicks_duration]
            if (!TryGetNeighborLiteralsForToken(
                    segments,
                    "questDurationTicks_duration",
                    out string beforeDurationLiteral,
                    out string afterDurationLiteral))
            {
                return null;
            }

            // 5) Using those literals, extract the duration substring
            //    from the resolved description in this language.
            string duration = TryExtractBetweenLiterals(
                resolvedDescription,
                beforeDurationLiteral,
                afterDurationLiteral);

            if (duration.NullOrEmpty())
                return null;

            duration = duration.Trim();
            if (duration.NullOrEmpty())
                return null;

            // 6) Extract the "claimInfo" sentence by building patterns
            //    from the claimInfo rule templates (tokens -> wildcards).
            string claimSentence = TryExtractClaimSentence(questDef, resolvedDescription);
            if (claimSentence != null)
                claimSentence = claimSentence.Trim();

            // 7) Resolve the editable template:
            //    compressedBody uses grammar tokens like [claimInfo],
            //    [questDurationTicks_duration], etc.
            var templateDef = TryGetTemplate(sourceDefName, kind);
            if (templateDef == null || templateDef.compressedBody.NullOrEmpty())
            {
                // No explicit template defined → do not compress this quest.
                return null;
            }

            string templateText = templateDef.compressedBody;

            // 8) Build the replacement map for tokens we can actually resolve.
            var replacements = new Dictionary<string, string>();

            if (!claimSentence.NullOrEmpty())
            {
                replacements["claimInfo"] = claimSentence;
            }

            if (!duration.NullOrEmpty())
            {
                replacements["questDurationTicks_duration"] = duration;
            }

            string result = FillTemplateWithTokenReplacements(templateText, replacements);
            return result.NullOrEmpty() ? null : result;
        }

        /// Applies token replacements to a template that uses grammar-style
        /// tokens like [claimInfo] or [questDurationTicks_duration].
        /// Only tokens present in 'replacements' are substituted; all others
        /// are left as "[tokenName]" so they remain visible to the LLM.
        private static string FillTemplateWithTokenReplacements(
            string templateText,
            Dictionary<string, string> replacements)
        {
            if (templateText.NullOrEmpty())
                return null;

            if (replacements == null || replacements.Count == 0)
                return templateText;

            var segments = ParseTemplateSegments(templateText);
            if (segments == null || segments.Count == 0)
                return templateText;

            var sb = new StringBuilder(templateText.Length);

            foreach (var seg in segments)
            {
                if (seg.IsToken)
                {
                    if (replacements.TryGetValue(seg.Text, out string value) &&
                        !value.NullOrEmpty())
                    {
                        sb.Append(value);
                    }
                    else
                    {
                        // Leave unknown tokens intact, including brackets.
                        sb.Append('[').Append(seg.Text).Append(']');
                    }
                }
                else
                {
                    if (!seg.Text.NullOrEmpty())
                        sb.Append(seg.Text);
                }
            }

            return sb.ToString();
        }

        /// Extract the substring of 'text' that lies between the first
        /// occurrence of 'beforeLiteral' and the next occurrence of
        /// 'afterLiteral'. Returns null on failure.
        private static string TryExtractBetweenLiterals(string text, string beforeLiteral, string afterLiteral)
        {
            if (text.NullOrEmpty() || beforeLiteral.NullOrEmpty() || afterLiteral.NullOrEmpty())
                return null;

            int idxBefore = text.IndexOf(beforeLiteral, StringComparison.Ordinal);
            if (idxBefore < 0)
                return null;

            int start = idxBefore + beforeLiteral.Length;
            if (start >= text.Length)
                return null;

            int idxAfter = text.IndexOf(afterLiteral, start, StringComparison.Ordinal);
            if (idxAfter < 0 || idxAfter <= start)
                return null;

            return text.Substring(start, idxAfter - start);
        }

        /// Using the quest's questDescriptionRules RulePack, build regex
        /// patterns for all claimInfo rules (tokens -> wildcards, literals
        /// -> anchors) and try to find which one matches the resolved
        /// description. Returns the longest match value, or null.
        private static string TryExtractClaimSentence(QuestScriptDef questDef, string resolvedDescription)
        {
            if (questDef == null ||
                questDef.questDescriptionRules == null ||
                resolvedDescription.NullOrEmpty())
            {
                return null;
            }

            var ruleStrings = GrammarTemplateUtil.GetRuleStrings(questDef.questDescriptionRules);
            if (ruleStrings == null || ruleStrings.Count == 0)
                return null;

            string bestMatch = null;
            int bestLength = 0;

            foreach (var rs in ruleStrings)
            {
                if (rs.Key == null)
                    continue;

                if (!rs.Key.StartsWith("claimInfo", StringComparison.Ordinal))
                    continue;

                if (rs.Output.NullOrEmpty())
                    continue;

                // Build a regex pattern from this claimInfo output:
                // - tokens [foo] become "(.+?)"
                // - literals become Regex.Escape(literal)
                string pattern = BuildWildcardPatternFromTemplate(rs.Output);
                if (pattern.NullOrEmpty())
                    continue;

                try
                {
                    var match = Regex.Match(resolvedDescription, pattern, RegexOptions.Singleline);
                    if (match.Success)
                    {
                        string value = match.Value;
                        if (!value.NullOrEmpty() && value.Length > bestLength)
                        {
                            bestMatch = value;
                            bestLength = value.Length;
                        }
                    }
                }
                catch
                {
                    // If regex construction fails for some reason, just skip this rule.
                }
            }

            return bestMatch;
        }

        /// Converts a template string with [tokens] into a regex pattern:
        ///   literal parts -> Regex.Escape(literal)
        ///   [token]       -> "(.+?)"
        /// This lets us match resolved text that filled in those tokens.
        private static string BuildWildcardPatternFromTemplate(string template)
        {
            if (template.NullOrEmpty())
                return null;

            var segments = ParseTemplateSegments(template);
            if (segments == null || segments.Count == 0)
                return null;

            var sb = new StringBuilder();
            foreach (var seg in segments)
            {
                if (seg.IsToken)
                {
                    sb.Append("(.+?)");
                }
                else if (!seg.Text.NullOrEmpty())
                {
                    sb.Append(Regex.Escape(seg.Text));
                }
            }

            string pattern = sb.ToString();
            return pattern.NullOrEmpty() ? null : pattern;
        }

        #endregion
    }
}