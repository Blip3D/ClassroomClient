using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using ClassroomClient.API;

namespace ClassroomClient.Core
{
    /// <summary>
    /// Per-headset language switch from the dashboard (WS12). Remembers the language the app reported
    /// (also before the headset is connected, so it can be sent right after registration) and checks a
    /// SET_LANGUAGE request before handing it to the app.
    /// </summary>
    internal static class ClassroomLanguageBridge
    {
        private const int MaxLocaleLength = 35;
        private const int MaxAvailableLanguages = 32;
        // BCP-47 style ("de", "en", "pt-BR"); never a '|' or ',' because codes travel in pipe messages.
        private static readonly Regex LocaleCode = new Regex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$");

        internal static string CurrentCode { get; private set; } = "";
        internal static string[] AvailableCodes { get; private set; } = Array.Empty<string>();
        internal static bool HasReport { get; private set; }

        internal static bool IsValidCode(string code)
            => !string.IsNullOrEmpty(code) && code.Length <= MaxLocaleLength && LocaleCode.IsMatch(code);

        internal static void SetReport(string currentCode, string[] availableCodes)
        {
            CurrentCode = IsValidCode(currentCode) ? currentCode : "";
            AvailableCodes = (availableCodes ?? Array.Empty<string>())
                .Where(IsValidCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxAvailableLanguages)
                .ToArray();
            HasReport = true;
        }

        /// <summary>
        /// Hands a valid request to the app and confirms receipt (WS13: the dashboard then waits longer,
        /// e.g. while the app downloads a language pack), or reports why it cannot be done.
        /// </summary>
        internal static void HandleRequest(string localeCode, Action<string, string> reportFailed, Action<string> sendAck)
        {
            string failure = CheckRequest(localeCode);
            if (failure != null)
            {
                reportFailed(IsValidCode(localeCode) ? localeCode : "", failure);
                return;
            }

            try
            {
                ClassroomEvents.FireOnLanguageChangeRequested(localeCode);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                reportFailed(localeCode, $"Language handler threw: {e.Message}");
                return;
            }
            sendAck(localeCode);
        }

        internal static string CheckRequest(string localeCode)
        {
            if (!IsValidCode(localeCode)) return "invalid_code";
            if (!ClassroomEvents.HasLanguageChangeRequestedHandler) return "not_supported";
            if (AvailableCodes.Length > 0 &&
                !AvailableCodes.Any(code => string.Equals(code, localeCode, StringComparison.OrdinalIgnoreCase)))
                return "not_available";
            return null;
        }

        // LANGUAGE|{deviceId}|SERVER|{currentCode}|{availableCodes}
        internal static string BuildLanguageMessage(string deviceId)
            => $"LANGUAGE|{deviceId}|SERVER|{CurrentCode}|{string.Join(",", AvailableCodes)}";

        // LANGUAGE_ACK|{deviceId}|SERVER|{requestedCode}
        internal static string BuildAckMessage(string deviceId, string requestedCode)
            => $"LANGUAGE_ACK|{deviceId}|SERVER|{requestedCode}";

        // LANGUAGE_FAILED|{deviceId}|SERVER|{requestedCode}|{reason}
        internal static string BuildFailedMessage(string deviceId, string requestedCode, string reason)
        {
            string code = IsValidCode(requestedCode) ? requestedCode : "";
            return $"LANGUAGE_FAILED|{deviceId}|SERVER|{code}|{(string.IsNullOrEmpty(reason) ? "unknown" : reason)}";
        }

        // With "Enter Play Mode Options" (no domain reload) static state would survive into the next
        // play session and the old report would be sent before the app reports again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => ResetForTests();

        internal static void ResetForTests()
        {
            CurrentCode = "";
            AvailableCodes = Array.Empty<string>();
            HasReport = false;
        }
    }
}
