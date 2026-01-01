using HarmonyLib;
using Verse;

namespace RimTalkEventPlus
{
    [StaticConstructorOnStartup]
    public static class EnhancedPromptDetector
    {
        private const string PACKAGE_ID = "ruaji.rimtalkpromptenhance";

        public static readonly bool IsLoaded;

        public static bool IsAutoEventCaptureEnabled
        {
            get
            {
                if (!IsLoaded)
                    return false;

                try
                {
                    var modType = AccessTools.TypeByName("RimTalkHealthEnhance.RimTalkHealthEnhanceMod");
                    var settings = AccessTools.Field(modType, "Settings")?.GetValue(null);
                    var field = AccessTools.Field(settings?.GetType(), "EnableAutoEventCapture");
                    return field != null && (bool)field.GetValue(settings);
                }
                catch
                {
                    return false;
                }
            }
        }

        static EnhancedPromptDetector()
        {
            IsLoaded = ModLister.GetActiveModWithIdentifier(PACKAGE_ID, ignorePostfix: true) != null;

            if (IsLoaded)
            {
                Log.Message("[RimTalk Event+] Detected RimTalk Enhanced Prompt mod.");
            }
        }
    }
}