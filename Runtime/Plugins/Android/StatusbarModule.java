package net.worldofvr.android.statusbarmodule;

import android.content.Context;
import android.net.NetworkInfo;
import android.net.wifi.WifiInfo;
import android.net.wifi.WifiManager;
import android.os.BatteryManager;
import android.content.Intent;
import android.content.IntentFilter;

import static android.content.Context.BATTERY_SERVICE;

public class StatusbarModule {

    // Called from unity to show the SSID.
    public String getSSID(Context context) {
        WifiManager manager = (WifiManager) context.getSystemService(Context.WIFI_SERVICE);
        if (manager.isWifiEnabled()) {
            WifiInfo wifiInfo = manager.getConnectionInfo();
            if (wifiInfo != null) {
                NetworkInfo.DetailedState state = WifiInfo.getDetailedStateOf(wifiInfo.getSupplicantState());
                if (state == NetworkInfo.DetailedState.CONNECTED || state == NetworkInfo.DetailedState.OBTAINING_IPADDR) {
                    return wifiInfo.getSSID();
                }
            }
        }
        return "No wifi connection!";
    }

    // Called from unity to show the signal level
    public int getSignalLevel(Context context, int numberOfLevels) {
        WifiManager manager = (WifiManager) context.getSystemService(Context.WIFI_SERVICE);
        if (manager.isWifiEnabled()) {
            WifiInfo wifiInfo = manager.getConnectionInfo();
            if (wifiInfo != null) {
                return WifiManager.calculateSignalLevel(wifiInfo.getRssi(), numberOfLevels);
            }
        }
        return 0;
    }

    // Called from unity to get battery percentage
    public int getBattery(Context context) {
        BatteryManager bm = (BatteryManager) context.getSystemService(BATTERY_SERVICE);
        return bm.getIntProperty(BatteryManager.BATTERY_PROPERTY_CAPACITY);
    }

    // Called from unity to check if device is charging
    public boolean isCharging(Context context) {
        BatteryManager bm = (BatteryManager) context.getSystemService(BATTERY_SERVICE);
        return bm.isCharging();
    }

    // Called from unity to get device model
    public String getDeviceModel() {
        return android.os.Build.MODEL;
    }

    // Called from unity to get device manufacturer
    public String getDeviceManufacturer() {
        return android.os.Build.MANUFACTURER;
    }
} 