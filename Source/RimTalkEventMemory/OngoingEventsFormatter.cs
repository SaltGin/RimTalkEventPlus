using System.Text;
using Verse;

namespace RimTalkEventPlus
{
    public static class OngoingEventsFormatter
    {
        public static string FormatOngoingEventsBlock(
            System.Collections.Generic.List<OngoingEventSnapshot> events,
            int maxChars = 2000)
        {
            if (events == null || events.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            sb.AppendLine("[Ongoing events]");

            int index = 1;
            foreach (var e in events)
            {
                if (sb.Length > maxChars)
                    break;

                // Base body selection
                string body = !e.QuestDescription.NullOrEmpty()
                    ? e.QuestDescription
                    : e.Body;

                // Optional compression: only if the setting is on and we have a SourceDefName
                if (RimTalkEventPlus.Settings != null &&
                    RimTalkEventPlus.Settings.enableEventTextCompression &&
                    !e.SourceDefName.NullOrEmpty())
                {
                    var compressed = EventTextCompressionUtil.TryGetCompressedBody(e);
                    if (!compressed.NullOrEmpty())
                    {
                        body = compressed;
                    }
                }

                body = StripSimpleTags(body);
                string label = StripSimpleTags(e.Label);

                sb.AppendLine();
                sb.Append(index).Append(") ")
                  .Append(label.NullOrEmpty() ? "(no title)" : label)
                  .AppendLine();

                if (!body.NullOrEmpty())
                {
                    if (body.Length > 600)
                        body = body.Substring(0, 600) + "...";

                    sb.AppendLine("   " + body.Replace("\n", "\n   "));
                }

                index++;
            }

            sb.AppendLine();
            sb.AppendLine("[Event list end]");

            return sb.ToString();
        }

        private static string StripSimpleTags(string input)
        {
            if (input.NullOrEmpty())
                return string.Empty;

            string s = input.Replace("</color>", string.Empty);
            while (true)
            {
                int start = s.IndexOf("<color", System.StringComparison.OrdinalIgnoreCase);
                if (start < 0) break;
                int end = s.IndexOf(">", start);
                if (end < 0) break;
                s = s.Remove(start, end - start + 1);
            }

            return s;
        }
    }
}
