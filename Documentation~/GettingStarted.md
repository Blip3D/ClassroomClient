# Getting Started — ClassroomClient

This guide is for the **VR Developer** — the Unity developer integrating ClassroomClient into a VR application.

---

## What ClassroomClient Does

ClassroomClient runs silently in the background of any Unity VR application. It connects to the ClassroomController server, streams the headset's camera view to the supervisor dashboard, and displays overlay notifications sent by the supervisor. The VR application does not need to be modified in any way.

---

## Requirements

- Unity 2021.3 or newer
- Meta Quest or Android XR device
- A running ClassroomController server (ask your Server Administrator for the server URL and device secret)

---

## Step 1 — Install the Package

In Unity, open **Window → Package Manager**.

Click the **+** button in the top-left corner and choose **Add package from git URL**.

Enter the URL of the ClassroomClient repository and click **Add**.

Unity will download and import the package. Wait for compilation to finish before continuing.

---

## Step 2 — Run the Setup Wizard

Open **Tools → Classroom Client → Quick Setup**.

The wizard window has three fields and one button:

**Server URL**
The WebSocket address of the ClassroomController server. Get this from your Server Administrator. The format depends on their setup — see Step 3 for details.

**Device Secret**
A shared secret string that identifies your VR devices to the server. Get this exact value from your Server Administrator. It must match the `DEVICE_SECRET` value in the server's environment configuration.

**App Name**
A human-readable name for your application. This is shown in the supervisor dashboard alongside each connected device. Example: `Safety Training v2`.

Once all three fields are filled in, click **Setup ClassroomClient**.

The wizard will:
- Create a persistent `ClassroomClient` root GameObject in the current scene (marked `DontDestroyOnLoad` — it survives scene changes)
- Add `ClassroomClientManager`, `WebRTCConnection`, and `WebSocketClient` components to it
- Create a `StreamingCamera` as a child of the main camera (copies the main camera's field of view, culling mask, and clear flags — renders to a 1280×720 render texture used for streaming)
- Create a separate `ClassroomClient_HUD` GameObject with the `ClassroomHUD` and overlay components
- Wire all component references automatically
- Pre-fill the server URL, device secret, and app name you entered

A confirmation dialog will appear when setup is complete.

---

## Step 3 — Configure

After the wizard runs, select the `ClassroomClient` GameObject in the Hierarchy. In the Inspector you can verify or change the following fields on `ClassroomClientManager`:

**Server URL**
The WebSocket URL of the ClassroomController server.

| Server setup | URL format |
|---|---|
| Domain with SSL (production) | `wss://yourdomain.com` |
| Public IP without SSL | `ws://123.45.67.89:8080` |
| Local network (dev / testing) | `ws://192.168.x.x:8080` |

Ask your Server Administrator which format applies to their setup.

**Device Secret**
Must exactly match the `DEVICE_SECRET` value configured on the server. If this is wrong, the server will reject the device's connection attempt.

---

## Step 4 — Build and Deploy

Build and deploy to Quest using the normal Unity build process. No special build steps, post-processors, or plugins are required beyond what the package already installs.

On first run after deployment, the device will connect to the server and appear in the Server Administrator's device list with status **Pending Approval**. The Server Administrator must approve the device once in Settings → Devices before it can enter the lobby. After approval, the device connects automatically on every subsequent launch.

---

## Step 5 — Verify

When the VR application is running on-device and connected to the server, a small status dot appears in the HUD overlay in the top-right of the VR view:

| Dot colour | Meaning |
|---|---|
| Red | Disconnected from server — not connected, or waiting to reconnect |
| Yellow | Connecting or reconnecting to server |
| Green | Connected and in the lobby — waiting for a session to start |
| Blue | In an active session — streaming to the supervisor |

On the supervisor side: after the device connects and is approved, it appears in the lobby device list. The supervisor can select it and start a session. When a session starts, the blue dot appears in VR and the stream tile appears in the supervisor dashboard.

---

## Optional — VR Application Integration

ClassroomClient works with zero code changes to the VR application. The following integrations are optional — use them only if you want your application code to react to ClassroomController events or send information back to the supervisor.

Subscribe to events in `OnEnable` and unsubscribe in `OnDisable`:

```csharp
using ClassroomClient.API;

void OnEnable()
{
    // Fires when the device successfully registers and enters the lobby
    ClassroomEvents.OnConnected += HandleConnected;

    // Fires when a session starts (device assigned to supervisor's session)
    ClassroomEvents.OnSessionStarted += HandleSessionStarted;

    // Fires when the session ends and the device returns to the lobby
    ClassroomEvents.OnSessionEnded += HandleSessionEnded;

    // Fires when the supervisor sends a message to this device
    // text: the message string, color: "blue"/"green"/"yellow"/"red", category: "general"/"technical"/"safety"
    ClassroomEvents.OnMessageReceived += HandleMessage;

    // Fires when the supervisor mutes or unmutes this device
    // isMuted: true = muted, false = unmuted
    ClassroomEvents.OnMuteChanged += HandleMute;

    // Fires when the device disconnects from the server
    ClassroomEvents.OnDisconnected += HandleDisconnected;
}

void OnDisable()
{
    ClassroomEvents.OnConnected -= HandleConnected;
    ClassroomEvents.OnSessionStarted -= HandleSessionStarted;
    ClassroomEvents.OnSessionEnded -= HandleSessionEnded;
    ClassroomEvents.OnMessageReceived -= HandleMessage;
    ClassroomEvents.OnMuteChanged -= HandleMute;
    ClassroomEvents.OnDisconnected -= HandleDisconnected;
}
```

To send the current session status from your application to the server:

```csharp
// Tell the server the activity has started
ClassroomClientAPI.SetStatus(SessionStatus.InProgress);

// Tell the server the activity has completed
ClassroomClientAPI.SetStatus(SessionStatus.Completed);
```

This status is visible in the supervisor dashboard alongside the device's stream tile.

For a full description of all API methods, events, enums, and usage rules, see [PublicAPI.md](PublicAPI.md).
