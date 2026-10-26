using System;
using System.Net.Http;
using System.Threading.Tasks;
using Unity.WebRTC;
using UnityEngine;

namespace ClassroomClient.Networking
{
    /// <summary>
    /// Reads the STUN/TURN servers the ClassroomController server advertises at GET /api/ice-config
    /// (e.g. the Raspberry Pi's coturn relay). Uses .NET HttpClient rather than UnityWebRequest:
    /// Unity blocks plain http:// on Android by default, and the Pi is served over http.
    /// </summary>
    internal static class IceConfigFetcher
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

        [Serializable]
        private class IceServerData
        {
            public string[] urls;
            public string username;
            public string credential;
        }

        [Serializable]
        private class IceConfigData
        {
            public IceServerData[] iceServers;
        }

        /// <summary>Returns the advertised ICE servers, or null if unavailable (caller keeps its default).</summary>
        public static async Task<RTCIceServer[]> FetchAsync(string serverUrl)
        {
            string endpoint = ToIceConfigUrl(serverUrl);
            if (endpoint == null) return null;

            try
            {
                using (var client = new HttpClient { Timeout = Timeout })
                {
                    string json = await client.GetStringAsync(endpoint);
                    return Parse(json);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[IceConfigFetcher] Could not read {endpoint} ({e.Message}) — using the default STUN server.");
                return null;
            }
        }

        // ws://host:port/... → http://host:port/api/ice-config, wss://host/... → https://host/api/ice-config
        internal static string ToIceConfigUrl(string serverUrl)
        {
            if (string.IsNullOrEmpty(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri uri)) return null;
            string scheme = uri.Scheme == "wss" || uri.Scheme == "https" ? "https" : "http";
            var builder = new UriBuilder(scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port, "/api/ice-config");
            return builder.Uri.ToString();
        }

        private static RTCIceServer[] Parse(string json)
        {
            var data = JsonUtility.FromJson<IceConfigData>(json);
            if (data?.iceServers == null || data.iceServers.Length == 0) return null;

            var servers = new RTCIceServer[data.iceServers.Length];
            for (int i = 0; i < servers.Length; i++)
            {
                IceServerData entry = data.iceServers[i];
                servers[i] = new RTCIceServer
                {
                    urls = entry.urls ?? new string[0],
                    username = string.IsNullOrEmpty(entry.username) ? null : entry.username,
                    credential = string.IsNullOrEmpty(entry.credential) ? null : entry.credential,
                };
            }
            return servers;
        }
    }
}
