using UnityEngine;
using System;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ClassroomClient.Utilities
{
    /// <summary>
    /// Provides device status information (battery, WiFi) via Android native calls.
    /// Handles missing permissions silently without flooding logs.
    /// </summary>
    public class DeviceStatusProvider : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float updateInterval = 10f;
        [Tooltip("Number of WiFi signal levels (5 = levels 0-4)")]
        public int wifiSignalLevels = 5; // 0-4 = 5 levels
        
        // Cached values
        private int batteryLevel = 100;
        private int wifiSignalLevel = 0;
        private bool isCharging = false;
        private string wifiSSID = "N/A";
        
        // Events
        public Action<int, int, bool, string> OnStatusUpdated; // battery, wifi, charging, ssid
        
        // Properties
        public int BatteryLevel => batteryLevel;
        public int WifiSignalLevel => wifiSignalLevel;
        public bool IsCharging => isCharging;
        public string WifiSSID => wifiSSID;
        
        private float lastUpdateTime;
        private bool hasLocationPermission = false;
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject statusbarModule;
        private AndroidJavaObject unityActivity;
#endif

        void Start()
        {
            InitializeAndroid();
            CheckPermissions();
            UpdateStatus(); // Initial update
        }

        void Update()
        {
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateStatus();
                lastUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// Check if required permissions are granted (does not request them)
        /// </summary>
        private void CheckPermissions()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Check if ACCESS_FINE_LOCATION is granted (required for WiFi SSID on Android 8+)
            hasLocationPermission = Permission.HasUserAuthorizedPermission(Permission.FineLocation);
            
            if (hasLocationPermission)
            {
                Debug.Log("[DeviceStatusProvider] Location permission granted - WiFi info available");
            }
            // Silent if permission not granted - no log spam
#else
            hasLocationPermission = true; // Always true in Editor
#endif
        }

        private void InitializeAndroid()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
                
                statusbarModule = new AndroidJavaObject("net.worldofvr.android.statusbarmodule.StatusbarModule");
                Debug.Log("[DeviceStatusProvider] Android StatusbarModule initialized");
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeviceStatusProvider] Failed to initialize Android module: {e.Message}");
            }
#else
            Debug.Log("[DeviceStatusProvider] Running in Editor - using simulated values");
#endif
        }

        /// <summary>
        /// Update all device status values.
        /// Battery/charging work without permissions.
        /// WiFi requires ACCESS_FINE_LOCATION - returns defaults silently if missing.
        /// </summary>
        public void UpdateStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (statusbarModule != null && unityActivity != null)
            {
                // Battery and charging don't require special permissions
                try
                {
                    batteryLevel = statusbarModule.Call<int>("getBattery", unityActivity);
                    isCharging = statusbarModule.Call<bool>("isCharging", unityActivity);
                }
                catch
                {
                    // Silent failure - keep previous values
                }
                
                // WiFi requires ACCESS_FINE_LOCATION permission
                // Check permission before calling to avoid SecurityException spam
                if (hasLocationPermission)
                {
                    try
                    {
                        wifiSignalLevel = statusbarModule.Call<int>("getSignalLevel", unityActivity, wifiSignalLevels);
                        wifiSSID = statusbarModule.Call<string>("getSSID", unityActivity);
                        
                        // Handle null/empty SSID
                        if (string.IsNullOrEmpty(wifiSSID))
                        {
                            wifiSSID = "N/A";
                        }
                    }
                    catch
                    {
                        // Silent failure - permission might have been revoked
                        // Re-check permission on next cycle
                        hasLocationPermission = Permission.HasUserAuthorizedPermission(Permission.FineLocation);
                        wifiSignalLevel = 0;
                        wifiSSID = "N/A";
                    }
                }
                else
                {
                    // No permission - use defaults silently (no logging)
                    wifiSignalLevel = 0;
                    wifiSSID = "N/A";
                    
                    // Periodically re-check in case permission was granted later (~10% of updates)
                    if (UnityEngine.Random.value < 0.1f)
                    {
                        hasLocationPermission = Permission.HasUserAuthorizedPermission(Permission.FineLocation);
                    }
                }
                
                // Fire event with current values
                OnStatusUpdated?.Invoke(batteryLevel, wifiSignalLevel, isCharging, wifiSSID);
            }
#else
            // Simulated values for Editor testing
            batteryLevel = UnityEngine.Random.Range(20, 100);
            wifiSignalLevel = UnityEngine.Random.Range(1, 5);
            isCharging = UnityEngine.Random.value > 0.7f;
            wifiSSID = "TestNetwork";
            
            OnStatusUpdated?.Invoke(batteryLevel, wifiSignalLevel, isCharging, wifiSSID);
#endif
        }
        
        /// <summary>
        /// Request location permission from user (call this from UI if needed)
        /// </summary>
        public void RequestLocationPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!hasLocationPermission)
            {
                Permission.RequestUserPermission(Permission.FineLocation);
            }
#endif
        }
        
        /// <summary>
        /// Check if WiFi info is available (permission granted)
        /// </summary>
        public bool IsWifiInfoAvailable => hasLocationPermission;

        /// <summary>
        /// Get status as formatted string for WebSocket transmission
        /// Format: battery|wifiLevel|isCharging|ssid
        /// </summary>
        public string GetStatusString()
        {
            return $"{batteryLevel}|{wifiSignalLevel}|{(isCharging ? "1" : "0")}|{wifiSSID}";
        }

        void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            statusbarModule?.Dispose();
            unityActivity?.Dispose();
#endif
        }
    }
}
