# Getting Started — ClassroomClient

This guide is for the **user** — integrating ClassroomClient into a VR application.

---

## What ClassroomClient Does

ClassroomClient runs silently in the background of any Unity VR application. It connects to the ClassroomController server, streams the headset's camera view to the user dashboard, and displays overlay notifications sent by the user. The VR application does not need to be modified in any way.

---

## Requirements

- Unity 2021.3 or newer
- Meta Quest or Android XR device
- A running ClassroomController server (ask the user for the server URL and server token)

---

## Step 1 — Install the Package

In Unity, open **Window → Package Manager**.

Click the **+** button in the top-left corner and choose **Add package from git URL**.

Enter the URL of the ClassroomClient repository and click **Add**.

Unity will download and import the package. Wait for compilation to finish before continuing.

---

## Step 2 — Run the Setup Wizard

Open **Tools → Classroom Client → Quick Setup**.

The wizard has two steps.

---

### Step 2a — Server Configuration

The first page has three fields:

**Server URL**
The WebSocket address of the ClassroomController server. Get this from the user. The format depends on the setup — see Step 3 for details.

**Server Token**
A shared secret string that authenticates your VR devices to the server. Get this exact value from the user. It must match the `SERVER_TOKEN` value in the server's environment configuration.

**App Name**
A human-readable name for your application. This is shown in the user dashboard alongside each connected device. Example: `Safety Training v2`.

Once all three fields are filled in, click **Setup ClassroomClient**.

The wizard will:
- Create a persistent `ClassroomClient` root GameObject in the current scene (marked `DontDestroyOnLoad` — it survives scene changes)
- Add `ClassroomClientManager`, `WebRTCConnection`, `WebSocketClient`, and `ClassroomSceneManager` components to it
- Create a `StreamingCamera` as a child of the main camera (copies the main camera's field of view, culling mask, and clear flags — renders to a 1280×720 render texture used for streaming)
- Create a separate `ClassroomClient_HUD` GameObject with the `ClassroomHUD` and overlay components
- Wire all component references automatically
- Pre-fill the server URL, server token, and app name you entered

After setup completes the wizard advances to the next step automatically.

---

### Step 2b — Scene Library (Optional)

The second page lets you register which scenes users can load onto devices remotely from the PWA. This step is optional — if your application does not use remote scene loading, click **Done** to close the wizard.

The scene list is populated from your project's **Build Settings**. Each row shows:

| Column | Description |
|---|---|
| Scene Name | The scene filename without extension |
| Load Type | `Standard` (via `SceneManager`) or `Addressable` (via `Addressables.LoadSceneAsync`) |
| Custom Loading | Tick only if you want to handle loading yourself — see the Scene Loading section below |

Use **+ Add Addressable Scene** to add a scene that is not in Build Settings but is registered as an Addressable asset. Enter the Addressable address (the key you assigned in the Addressables Groups window).

Click **Done** to save the scene library and close the wizard.

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

Ask the user which format applies to the setup.

**Server Token**
Must exactly match the `SERVER_TOKEN` value configured on the server. If this is wrong, the server will reject the device's connection attempt.

---

## Step 4 — Build and Deploy

Build and deploy to Quest using the normal Unity build process. No special build steps, post-processors, or plugins are required beyond what the package already installs.

On first run after deployment, the device will connect to the server and appear in the user's device list with status **Pending Approval**. The user must approve the device once in the Admin panel (Admin → Devices tab) before it can enter the lobby. After approval, the device connects automatically on every subsequent launch.

---

## Step 5 — Verify

When the VR application is running on-device and connected to the server, a small status dot appears in the HUD overlay in the top-right of the VR view:

| Dot colour | Meaning |
|---|---|
| Red | Disconnected from server — not connected, or waiting to reconnect |
| Yellow | Connecting or reconnecting to server |
| Green | Connected and in the lobby — waiting for a session to start |
| Blue | In an active session — streaming to the user |

On the user side: after the device connects and is approved, it appears in the lobby device list. The user can select it and start a session. When a session starts, the blue dot appears in VR and the stream tile appears in the user dashboard.

---

## Optional — VR Application Integration

ClassroomClient works with zero code changes to the VR application. The following integrations are optional — use them only if you want your application code to react to ClassroomController events or send information back to the user.

Subscribe to events in `OnEnable` and unsubscribe in `OnDisable`:

```csharp
using ClassroomClient.API;

void OnEnable()
{
    // Fires when the device successfully registers and enters the lobby
    ClassroomEvents.OnConnected += HandleConnected;

    // Fires when a session starts (device assigned to the user's session)
    ClassroomEvents.OnSessionStarted += HandleSessionStarted;

    // Fires when the session ends and the device returns to the lobby
    ClassroomEvents.OnSessionEnded += HandleSessionEnded;

    // Fires when the user sends a message to this device
    // text: the message string, color: "blue"/"green"/"yellow"/"red", category: "general"/"technical"/"safety"
    ClassroomEvents.OnMessageReceived += HandleMessage;

    // Fires when the user mutes or unmutes this device
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

If your application switches the active camera at runtime (for example, switching between environments or viewpoints), tell ClassroomClient which camera to stream:

```csharp
// Switch the streamed camera without interrupting the session
ClassroomClientAPI.SetStreamCamera(myNewCamera);
```

This swaps the video track immediately if streaming is active. If called before a session starts, the new camera is used when the next session begins.

This status is visible in the user dashboard alongside the device's stream tile.

For a full description of all API methods, events, enums, and usage rules, see [PublicAPI.md](PublicAPI.md).

---

## Optional — Scene Loading

If your VR application uses scene changes, ClassroomClient can automatically re-attach its streaming camera to the new scene's camera after every load. No code is required for this — it happens automatically.

The Setup Wizard lets you register which scenes users can load onto devices remotely. Each scene has a **Custom Loading** checkbox. Understanding what this checkbox does is important.

---

### The Custom Loading checkbox

**Unticked (recommended for most projects)**

ClassroomClient handles scene loading entirely. When the user triggers a scene load:

- Standard scenes → loaded via `SceneManager.LoadSceneAsync`
- Addressable scenes → loaded via `Addressables.LoadSceneAsync`

After loading, ClassroomClient automatically finds the new main camera and re-attaches the streaming camera. You write no code.

**Ticked (advanced — only if you need full control)**

ClassroomClient does **not** load the scene. Instead it fires an event and stops. Subscribe to `OnLoadSceneRequested` on the `ClassroomSceneManager` component that lives on the persistent `ClassroomClient` GameObject:

```csharp
using ClassroomClient.Core;
using ClassroomClient.API;

void OnEnable()
{
    var sceneManager = FindAnyObjectByType<ClassroomSceneManager>();
    if (sceneManager != null)
        sceneManager.OnLoadSceneRequested += HandleLoadSceneRequested;
}

void OnDisable()
{
    var sceneManager = FindAnyObjectByType<ClassroomSceneManager>();
    if (sceneManager != null)
        sceneManager.OnLoadSceneRequested -= HandleLoadSceneRequested;
}

void HandleLoadSceneRequested(SceneInfo info)
{
    // You are responsible for loading the scene however you need.
    // Show a loading screen, preload dependencies, etc.
    StartCoroutine(MyCustomLoad(info.sceneKey));
}

private IEnumerator MyCustomLoad(string sceneKey)
{
    // ... your custom loading logic ...
    yield return SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Additive);

    // Tell the server the scene loaded successfully
    ClassroomClientAPI.ReportSceneLoaded(sceneKey);

    // If loading failed, report it instead:
    // ClassroomClientAPI.ReportSceneLoadFailed(sceneKey, "reason here");
}
```

If you tick this and write no handler code, **nothing happens** — the scene will never load when the user requests it.

**Important:** When using custom loading you are responsible for calling `ClassroomClientAPI.ReportSceneLoaded` or `ClassroomClientAPI.ReportSceneLoadFailed` when loading finishes. Without these calls the server and PWA will not know the scene changed.

Use this only when you need behaviour the built-in path cannot provide, such as:
- A custom loading screen with a progress bar
- Preloading Addressable dependencies before the scene loads
- Additive / multi-scene loading setups

---

### Do I need to tick it for Addressable scenes?

**No.** ClassroomClient has a built-in Addressable loading path that works without ticking the checkbox. If your scene is an Addressable asset, add it via **+ Add Addressable Scene** in the Setup Wizard and leave **Custom Loading** unticked. ClassroomClient will call `Addressables.LoadSceneAsync` for you.

Only tick **Custom Loading** if the built-in Addressable loading is not enough for your use case.

---

### Summary

| Custom Loading | Who loads the scene | Camera re-attached automatically | Code required |
|---|---|---|---|
| Unticked | ClassroomClient | Yes | No |
| Ticked | You (via `OnLoadSceneRequested`) | No — you must handle it | Yes |
