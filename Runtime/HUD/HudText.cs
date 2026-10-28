using UnityEngine;
using ClassroomClient.Core;

namespace ClassroomClient.HUD
{
    /// <summary>
    /// ClassroomClient's own HUD text in the app's language (WS12): English, German and Portuguese (Brazil).
    /// The language comes from ClassroomClientAPI.ReportLanguage; if the app reported none, the headset's
    /// system language is used; anything else falls back to English. Teacher messages are shown as sent.
    /// </summary>
    internal static class HudText
    {
        internal const string English = "en";
        internal const string German = "de";
        internal const string PortugueseBrazil = "pt-BR";

        internal static string PendingApproval => PendingApprovalFor(PickLanguage(ClassroomLanguageBridge.CurrentCode, Application.systemLanguage));

        internal static string PendingApprovalFor(string language)
        {
            switch (language)
            {
                case German: return "Dieses Headset wartet auf die Freigabe durch einen Admin im ClassroomController-Dashboard.";
                case PortugueseBrazil: return "Aguardando um administrador aprovar este headset no painel do ClassroomController.";
                default: return "Waiting for an admin to approve this headset in the ClassroomController dashboard.";
            }
        }

        internal static string PickLanguage(string reportedCode, SystemLanguage systemLanguage)
        {
            if (!string.IsNullOrEmpty(reportedCode))
            {
                string code = reportedCode.ToLowerInvariant();
                if (code == "de" || code.StartsWith("de-")) return German;
                if (code == "pt" || code.StartsWith("pt-")) return PortugueseBrazil;
                return English;
            }

            switch (systemLanguage)
            {
                case SystemLanguage.German: return German;
                case SystemLanguage.Portuguese: return PortugueseBrazil;
                default: return English;
            }
        }
    }
}
