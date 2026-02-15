using HarmonyLib;
using System.Reflection;
using Verse;

namespace RimTalkEventPlus
{
    [StaticConstructorOnStartup]
    public static class EnhancedPromptDetector
    {
        private const string PACKAGE_ID = "ruaji.rimtalkpromptenhance";

        public static readonly bool IsLoaded;

        // Cached reflection metadata only
        private static readonly FieldInfo _settingsField;
        private static readonly FieldInfo _enableAutoEventCaptureField;
        private static readonly PropertyInfo _enableAutoEventCaptureProperty;

        public static bool IsAutoEventCaptureEnabled
        {
            get
            {
                // Fast path: mod not loaded or reflection setup failed
                if (!IsLoaded || _settingsField == null
                    || (_enableAutoEventCaptureField == null && _enableAutoEventCaptureProperty == null))
                    return false;

                try
                {
                    // Fetch settings instance fresh
                    var settingsInstance = _settingsField.GetValue(null);
                    if (settingsInstance == null)
                        return false;

                    // Read current value fresh (prefer property over field)
                    if (_enableAutoEventCaptureProperty != null)
                        return (bool)_enableAutoEventCaptureProperty.GetValue(settingsInstance);

                    return (bool)_enableAutoEventCaptureField.GetValue(settingsInstance);
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

            if (!IsLoaded)
                return;

            Log.Message("[RimTalk Event+] Detected RimTalk Enhanced Prompt mod.");

            // Cache reflection metadata at startup
            try
            {
                var modType = AccessTools.TypeByName("RimTalkHealthEnhance.RimTalkHealthEnhanceMod");
                if (modType == null)
                {
                    Log.Warning("[RimTalk Event+] Could not find RimTalkHealthEnhanceMod type for caching.");
                    return;
                }

                _settingsField = AccessTools.Field(modType, "Settings");
                if (_settingsField == null)
                {
                    Log.Warning("[RimTalk Event+] Could not find Settings field for caching.");
                    return;
                }

                var settingsInstance = _settingsField.GetValue(null);
                if (settingsInstance == null)
                {
                    Log.Warning("[RimTalk Event+] Settings instance is null at startup; will retry on access.");
                    return;
                }

                _enableAutoEventCaptureProperty = AccessTools.Property(
                    settingsInstance.GetType(),
                    "EnableAutoEventCapture"
                );

                if (_enableAutoEventCaptureProperty == null)
                {
                    _enableAutoEventCaptureField = AccessTools.Field(
                        settingsInstance.GetType(),
                        "EnableAutoEventCapture"
                    );
                }

                if (_enableAutoEventCaptureProperty != null || _enableAutoEventCaptureField != null)
                {
                    Log.Message("[RimTalk Event+] Successfully cached Enhanced Prompt settings accessor.");
                }
                else
                {
                    Log.Warning("[RimTalk Event+] Could not find EnableAutoEventCapture field; feature detection disabled.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Warning($"[RimTalk Event+] Failed to cache Enhanced Prompt settings: {ex.Message}");
            }
        }
    }
}