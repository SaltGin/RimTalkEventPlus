using HarmonyLib;
using Verse;

namespace RimTalkEventPlus
{
    [StaticConstructorOnStartup]
    public static class EnhancedPromptDetector
    {
        private const string PACKAGE_ID = "ruaji.rimtalkpromptenhance";

        public static readonly bool IsLoaded;
        public static readonly bool IsAutoEventCaptureEnabled;

        static EnhancedPromptDetector()
        {
            IsLoaded = ModLister.GetActiveModWithIdentifier(PACKAGE_ID, ignorePostfix: true) != null;

            if (!IsLoaded)
                return;

            Log.Message("[RimTalk Event+] Detected RimTalk Enhanced Prompt mod.");

            try
            {
                var modType = AccessTools.TypeByName("RimTalkHealthEnhance.RimTalkHealthEnhanceMod");
                var settings = AccessTools.Field(modType, "Settings")?.GetValue(null);
                var field = AccessTools.Field(settings?.GetType(), "EnableAutoEventCapture");
                IsAutoEventCaptureEnabled = field != null && (bool)field.GetValue(settings);
            }
            catch { }
        }
    }
}