# Getting Started — ClassroomClient

This guide shows you how to add ClassroomClient to a Unity VR app so that a teacher can watch each headset live in the ClassroomController dashboard. It is written for readers with little or no Unity experience. Follow the **Quick path** first; everything marked **Advanced** is optional.

---

## What ClassroomClient Does

ClassroomClient runs quietly in the background of your VR app. It:

- connects the headset to the ClassroomController server,
- streams what the headset user sees (and, optionally, hears) to the teacher's dashboard,
- shows short messages from the teacher inside the headset,
- lets the teacher switch the app to another scene (optional).

Your app's own code does not need to change.

---

## What You Need

Tick off this list before you start:

- [ ] **Unity 2022.3 LTS or Unity 6** installed through Unity Hub, **with the "Android Build Support" module** (Unity Hub → Installs → ⚙ next to your Unity version → Add modules → Android Build Support, including OpenJDK and Android SDK & NDK Tools).
- [ ] **A Unity project that already runs as a VR app on your headset** (ClassroomClient adds streaming to a VR app; it does not set up VR itself).
- [ ] **A Meta Quest headset** (or another Android-based standalone headset) **in developer mode**, connected to your computer with a USB cable, with **USB debugging allowed** (put the headset on after plugging it in and accept the "Allow USB debugging" prompt).
- [ ] **The server URL and the server token** — ask the administrator of your ClassroomController server. Typical URLs:
  - Raspberry Pi classroom box: `ws://192.168.50.1:8080`
  - blip3d.com cloud: `wss://blip3d.com`
- [ ] **A teacher or admin login for the ClassroomController dashboard** (to approve the headset and start a session).

---

## Unity Words Used in This Guide

| Term | Meaning |
|---|---|
| **Scene** | One "level" or room of your app, saved as a `.unity` file. |
| **GameObject** | Any object in a scene (a camera, a light, an empty helper object…). |
| **Hierarchy window** | The list of GameObjects in the open scene (usually on the left). |
| **Inspector window** | Shows the settings of the selected GameObject (usually on the right). |
| **Project window** | Your project's files (usually at the bottom). |
| **Console window** | Unity's log of messages, warnings and errors (Window → General → Console). |
| **Prefab** | A saved, reusable GameObject. |
| **Build Settings / Build Profiles** | Where you choose the target platform (Android) and which scenes go into the app. Unity 2022.3: **File → Build Settings**. Unity 6: **File → Build Profiles**. |
| **Build And Run** | Builds the app and installs + starts it on the connected headset. |
| **Scripting define** | A project-wide switch that turns optional code on. ClassroomClient uses one for microphone streaming. |
| **Addressables** | An optional Unity package for loading content (including scenes) on demand. Only relevant if your app already uses it. |

---

## Quick Path

Five steps: **install → run the wizard → build → approve the headset → start a session.**

### Step 1 — Install the package

1. Open your VR project in Unity.
2. Open **Window → Package Manager**.
3. Click the **+** button in the top-left corner of the Package Manager.
4. Choose **Add package from git URL…** (Unity 6: **Install package from git URL…**).
5. Paste the ClassroomClient repository URL (for the GitHub release: `https://github.com/Blip3D/ClassroomClient.git`) and click **Add** (Unity 6: **Install**).
6. Wait until Unity has finished importing and compiling (the spinner in the bottom-right corner stops).
7. Open the **Console** window and check that there are no red error messages.

Unity also installs the required packages `com.unity.webrtc` and `com.unity.textmeshpro` automatically.

### Step 2 — Open the scene your app starts with

1. In the **Project** window, double-click the first scene of your app (the scene listed first in Build Settings / Build Profiles).
2. Make sure this scene contains your VR camera rig.

> **Tip:** the best place for ClassroomClient is a small "bootstrap" scene that holds only your camera rig and managers and then loads the actual content. See [Scene Loading](#advanced--scene-loading) for why.

### Step 3 — Run the Setup Wizard (page 1: settings)

1. Open **Tools → Classroom Client → Quick Setup**. The "Classroom Client Setup" window opens.
2. **Server** — pick a preset:
   - **Raspberry Pi classroom** → `ws://192.168.50.1:8080`
   - **blip3d.com cloud** → `wss://blip3d.com`
   - **Custom** → type the URL you were given into **Server URL**. (Editing the URL by hand switches the dropdown to Custom automatically.)
3. **Server Token** — paste the token from your administrator. Click **Show** to check it, **Hide** to hide it again. It must match the server's `SERVER_TOKEN` exactly.
4. **App Name** — the name teachers see next to the headset, e.g. `Safety Training`.
5. **Camera Setup** — leave **Automatic (Recommended)** unless you know you need **API Controlled (Advanced)** (see [Advanced — Choosing the streamed camera](#advanced--choosing-the-streamed-camera)). Automatic needs a camera in the open scene.
6. **Headset HUD** — where the HUD (the small overlay ClassroomClient shows in the headset) appears. The defaults are fine:

   | Slider | Default | Meaning |
   |---|---|---|
   | Distance (m) | 0.6 | How far in front of the eyes teacher messages float |
   | Height (m) | −0.10 | Message panel height relative to eye level (negative = slightly below) |
   | Icon height (m) | +0.22 | Status icons above eye level |
   | Icon side (m) | +0.25 | Status icons to the right → upper-right corner of the view |

7. **Streamed Audio**:
   - **Stream app audio** (on by default) — the teacher can hear the app's sound. The headset's own sound is never changed.
   - **Stream microphone** (off by default) — also sends the headset microphone. Ticking it adds the scripting define `CLASSROOMCLIENT_MICROPHONE` when you click **Finish** on page 2; this makes Unity add the Android microphone permission to your app, and the headset user is asked for permission the first time. The microphone only runs while the headset is being streamed; a microphone icon is shown in the HUD while it does. If your app is already using the microphone itself (e.g. voice chat), ClassroomClient does not take it over and streams the app audio only. Unticking it and finishing the wizard again removes the define.
8. Click **Setup ClassroomClient**.

The wizard now:

- creates a `ClassroomClient` GameObject at the top level of the open scene (you can see it in the **Hierarchy**). At runtime it is kept alive across scene changes,
- adds the components `ClassroomClientManager`, `WebRTCConnection`, `WebSocketClient` and `ClassroomSceneManager`, and a child `ClassroomClient_HUD`,
- in Automatic mode, adds a `StreamingCamera` as a child of your main camera (a separate camera used only for the stream — the headset's own view is never touched),
- fills in the server URL, token, app name, camera mode, HUD and audio settings.

Running the wizard again replaces the existing `ClassroomClient` GameObject in that scene.

### Step 4 — Run the Setup Wizard (page 2: Scene Library)

The second page, **Configure Scene Library**, decides which scenes a teacher can switch the headset to from the dashboard.

- If you don't want remote scene switching, just click **Finish**.
- Otherwise, see [Advanced — Scene Loading](#advanced--scene-loading) first, then tick the scenes you want and click **Finish**.

Clicking **Finish** saves `Assets/ClassroomClient/SceneLibrary.asset`, links it to the `ClassroomClient` GameObject and closes the wizard ("Setup Complete").

Save the scene: **File → Save** (Ctrl+S / Cmd+S).

### Step 5 — Build and install on the headset

1. Open Build Settings (Unity 2022.3: **File → Build Settings**; Unity 6: **File → Build Profiles**).
2. Make sure the platform is **Android** (select it and click **Switch Platform** if needed).
3. Make sure the scene with the `ClassroomClient` GameObject is in the scene list and is the first one.
4. Connect the headset by USB and select it as **Run Device** (click **Refresh** if it is missing).
5. Click **Build And Run** and choose a file name for the `.apk`.

No extra build steps or plugins are needed.

### Step 6 — Approve the headset (first time only)

1. Start the app on the headset. A **blue** cast icon appears in the upper-right of the view and a one-time message says the headset is waiting for an admin to approve it.
2. An admin logs in to the dashboard and opens **Admin → Devices**. The new headset is listed as **Pending**.
3. The admin clicks **Approve**. The headset joins within a second or two (the icon turns **yellow** = connected, waiting in the lobby).

Approval is remembered — the headset connects automatically every time after that.

**Naming headsets:** the wizard has no name field. A new headset appears in the dashboard with a short device id (the last 4 characters of its hardware id). To give it a readable name such as "Station 1", an admin opens **Admin → Devices** and clicks **Rename**. The name is stored on the server, so every headset can have its own name even though all of them run the same build.

### Step 7 — Start a session

1. A teacher selects the headset in the dashboard lobby and starts a session.
2. The cast icon in the headset turns **green** — the headset is being streamed.
3. The headset's video (and sound, if enabled) appears as a tile in the dashboard. **Tiles start muted** (browsers require this) — click the speaker button on the tile to listen. This only affects that browser; the headset's sound is never changed.

You are done. Everything below is optional.

---

## What the Headset Shows (HUD)

**Cast icon** (upper-right of the view):

| Colour | Meaning |
|---|---|
| Green | In a session — this headset is being streamed |
| Yellow | Connecting / reconnecting, or connected and waiting in the lobby |
| Blue | Waiting for an admin to approve this headset, or it was rejected (with a one-time notice in the app's language — English, German or Portuguese (Brazil)) — approve it in **Admin → Devices** |
| Red | Disconnected (server unreachable or registration rejected) |

**Microphone icon** — shown next to the cast icon only while the headset microphone is actually being streamed.

**Teacher messages** appear centred slightly below eye level for about 6 seconds, then fade out.

---

## Troubleshooting

| Symptom | What to check |
|---|---|
| **Icon stays yellow and the headset never appears in the dashboard** | The headset cannot reach the server. Is it on the same Wi-Fi as the server (Pi: the classroom Wi-Fi)? Is the URL right — `ws://` for the Pi / plain HTTP servers, `wss://` for HTTPS servers like blip3d.com? Is a firewall blocking the port (e.g. 8080)? |
| **Icon is blue** | The headset is waiting for approval (or it was rejected). An admin must approve it in the dashboard under **Admin → Devices**. |
| **Icon is red** | Disconnected. A wrong server token is a common cause — check the token and look for "Registration rejected" in the log. |
| **Build fails with "Scene file not found"** (or a similar missing-scene error) | Build Settings / Build Profiles still lists a scene that was deleted or moved. Remove the missing entry from the scene list and build again. |
| **Build And Run cannot find the headset** | Check the USB cable, put the headset on and accept "Allow USB debugging", close the Meta Quest Link app on your computer, unplug and replug the cable, then click **Refresh** next to Run Device. |
| **Dashboard tile is silent** | Tiles start muted — click the speaker button on the tile. Also check that **Stream app audio** was ticked in the wizard. |
| **No microphone in the stream** | Was **Stream microphone** ticked and the wizard finished with **Finish**? The headset user must allow the microphone permission (if it was denied, allow it again in the headset's app permission settings). If the app itself is already recording the microphone, ClassroomClient leaves it alone. |
| **Headset appears in the dashboard but its stream stays black, and the dashboard runs on a Mac** | macOS blocks the browser from reaching devices on the local network until allowed: **System Settings → Privacy & Security → Local Network** → switch the browser (Chrome, Brave, Edge, Safari …) **on**, quit it with **Cmd+Q** and reopen it. Typical sign: it fails when laptop and headset share the same Wi-Fi/hotspot. |
| **Stream is black or frozen after the app changes scene** | Make sure you use 2.75.0 or newer. In API Controlled mode, call `SetStreamCamera` with the new camera (see below). |
| **No language menu on the headset's tile** | The menu only appears when the app reports its languages with `ClassroomClientAPI.ReportLanguage` (see [Language switch from the dashboard](#advanced--language-switch-from-the-dashboard)), and needs ClassroomClient and the server at 2.76.0 or newer. |
| **Menus/UI missing from the stream** | Canvases set to **Screen Space – Overlay** are not captured (the stream is recorded by a camera, and Overlay UI is drawn after all cameras). Use **Screen Space – Camera** or **World Space** for UI that should appear in the stream. |

---

## Advanced — Choosing the Streamed Camera

The camera you choose is only a **viewpoint**: ClassroomClient creates its own capture camera (`StreamingCamera`) as a child of it and streams that. Your camera is never given a render texture and never disabled, so the headset view is never affected.

**Automatic (Recommended)** — streams from the main camera (`Camera.main`). If the app changes scene by itself and the camera rig is replaced, the stream re-attaches to the new main camera automatically.

**API Controlled (Advanced)** — for apps whose camera rig is created at runtime (e.g. loaded via Addressables). Nothing is streamed until your code calls `ClassroomClientAPI.SetStreamCamera(camera)`; if a session starts earlier, streaming waits and begins as soon as you call it.

```csharp
using ClassroomClient.API;
using UnityEngine;

public class MyRigStreamSetup : MonoBehaviour
{
    [SerializeField] private Camera viewpoint; // e.g. the rig's centre-eye camera

    void OnEnable()
    {
        ClassroomEvents.OnStreamCameraLost += HandleCameraLost;
    }

    void Start()
    {
        ClassroomClientAPI.SetStreamCamera(viewpoint);
    }

    void OnDisable()
    {
        ClassroomEvents.OnStreamCameraLost -= HandleCameraLost;
    }

    // Raised when the streamed camera is destroyed or deactivated (e.g. the app changed scene).
    private void HandleCameraLost()
    {
        Camera newCamera = Camera.main; // replace with your own lookup
        if (newCamera != null)
            ClassroomClientAPI.SetStreamCamera(newCamera);
    }
}
```

If no new camera is set within about 3 seconds after `OnStreamCameraLost`, ClassroomClient falls back to `Camera.main` so the stream never stays black; a later `SetStreamCamera` call replaces the fallback.

---

## Advanced — Scene Loading

From the dashboard a teacher can switch a headset to any scene listed in the app's **Scene Library**. You set the library up on page 2 of the wizard (you can re-run the wizard later; it rebuilds `Assets/ClassroomClient/SceneLibrary.asset`).

### Setting up the Scene Library (wizard page 2)

1. Only scenes that are in Build Settings / Build Profiles (and enabled) are listed. Add your scenes there first if needed.
2. For each scene: tick the checkbox to include it, optionally change **Display name** (what teachers see) and **Category** (General, Training, Simulation, Assessment, Lab, Other).
3. Leave **Custom Loading** unticked unless your app loads that scene itself (see below).
4. If the Addressables package is installed, click **+ Add Addressable Scene** to add a scene by its Addressable key (the address shown in Window → Asset Management → Addressables → Groups), then **Add**.
5. Click **Finish**.

### Load types

| Load type | How ClassroomClient loads it |
|---|---|
| **Standard** | A Build Settings scene, loaded additively with `SceneManager.LoadSceneAsync`. |
| **Addressable** | Loaded additively with `Addressables.LoadSceneAsync`. Works when the `com.unity.addressables` package is installed in your project (optional — ClassroomClient compiles without it). |
| **Custom Loading** (checkbox) | ClassroomClient does not load the scene; your code does (see below). |

### Rules ClassroomClient follows

- **It only unloads scenes it loaded itself.** When a teacher loads scene B after scene A, ClassroomClient unloads A. It never unloads your app's startup scene or scenes your app loaded.
- **Start your app in a bootstrap/rig scene.** Because the startup scene is never unloaded, a startup scene that is itself a visible environment stays visible underneath the first teacher-loaded scene. A small startup scene that only holds the camera rig, managers and ClassroomClient avoids this.
- **Loading a scene that is already loaded counts as success** — no second copy is loaded.
- **Only one load at a time** — a load requested while another is still running is rejected, and the teacher sees the failure.
- **Every failure is reported to the dashboard** with a reason (unknown scene, scene not in Build Settings, Addressables error, no custom loader, custom loader threw, …).
- The scene that was active before stays the active scene (lighting and skybox come from it).

### Custom Loading

Tick **Custom Loading** only if your app must load a scene itself (custom loading screen, extra preparation, …). Then:

1. Subscribe to `ClassroomEvents.OnLoadSceneRequested`. (Subscribing to the `OnLoadSceneRequested` event on the `ClassroomSceneManager` component still works too.)
2. Load the scene yourself.
3. Report the result with `ClassroomClientAPI.ReportSceneLoaded(sceneKey)` or `ClassroomClientAPI.ReportSceneLoadFailed(sceneKey, reason)`.

If nobody is subscribed, or your handler throws an exception, the load is reported as failed.

When you report a Custom Loading scene as loaded, ClassroomClient unloads **its own** previously loaded scene. The other direction is your job: when ClassroomClient later loads one of its scenes, unload your custom scene — the recommended way is to listen to `ClassroomEvents.OnSceneLoadCompleted`.

Complete example (put it on a GameObject in your startup scene):

```csharp
using System.Collections;
using System.IO;
using ClassroomClient.API;
using ClassroomClient.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MyCustomSceneLoader : MonoBehaviour
{
    private static MyCustomSceneLoader _instance;
    private string _loadedSceneName;

    void Awake()
    {
        // Keep exactly one loader alive across scene changes.
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        ClassroomEvents.OnLoadSceneRequested += OnLoadSceneRequested;
        ClassroomEvents.OnSceneLoadCompleted += OnClassroomSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance != this) return;
        ClassroomEvents.OnLoadSceneRequested -= OnLoadSceneRequested;
        ClassroomEvents.OnSceneLoadCompleted -= OnClassroomSceneLoaded;
        _instance = null;
    }

    // The teacher picked a scene marked "Custom Loading".
    private void OnLoadSceneRequested(SceneInfo info)
    {
        StartCoroutine(LoadCustom(info.sceneKey));
    }

    private IEnumerator LoadCustom(string sceneKey)
    {
        string sceneName = Path.GetFileNameWithoutExtension(sceneKey);
        if (SceneManager.GetSceneByName(sceneName).isLoaded)
        {
            ClassroomClientAPI.ReportSceneLoaded(sceneKey);
            yield break;
        }

        AsyncOperation load = SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Additive);
        if (load == null)
        {
            ClassroomClientAPI.ReportSceneLoadFailed(sceneKey, "Scene not in Build Settings");
            yield break;
        }
        yield return load;

        // Unload the custom scene we loaded before, if any.
        if (!string.IsNullOrEmpty(_loadedSceneName) && SceneManager.GetSceneByName(_loadedSceneName).isLoaded)
        {
            yield return SceneManager.UnloadSceneAsync(_loadedSceneName);
        }
        _loadedSceneName = sceneName;

        ClassroomClientAPI.ReportCurrentScene(sceneKey);
        ClassroomClientAPI.ReportSceneLoaded(sceneKey); // ClassroomClient now unloads its own previous scene
    }

    // ClassroomClient loaded one of its own scenes: our custom scene is no longer the content.
    private void OnClassroomSceneLoaded(string sceneKey)
    {
        if (string.IsNullOrEmpty(_loadedSceneName)) return;
        if (SceneManager.GetSceneByName(_loadedSceneName).isLoaded)
        {
            SceneManager.UnloadSceneAsync(_loadedSceneName);
        }
        _loadedSceneName = null;
    }
}
```

### Do I need Custom Loading for Addressable scenes?

**No.** Add them with **+ Add Addressable Scene** and leave Custom Loading unticked; ClassroomClient calls `Addressables.LoadSceneAsync` for you.

### Scene changes your app makes by itself

If your app switches scenes on its own (not via the dashboard), ClassroomClient notices it, tells the dashboard which scene is now active, and re-attaches the stream to the new camera (Automatic mode) or raises `OnStreamCameraLost` (API Controlled mode). If your app reloads the scene that contains the `ClassroomClient` GameObject, the extra copy removes itself — there is always exactly one ClassroomClient.

---

## Advanced — Language switch from the dashboard

The teacher can change the language of **one** headset from the dashboard: each tile gets a small language menu. ClassroomClient does not translate anything itself — it passes the teacher's choice to your app, and your app switches its own language. This needs a few lines in your app:

1. **At startup, report your languages:** `ClassroomClientAPI.ReportLanguage(currentCode, availableCodes)`, e.g. `("en", new[] { "en", "de", "pt-BR" })`. Without this call the dashboard shows no language menu for this headset.
2. **Listen for the teacher's choice:** subscribe to `ClassroomEvents.OnLanguageChangeRequested`. It gives you a locale code such as `"de"`.
3. **Switch, save, report:** switch your app's language, save it (so it survives a restart), and call `ReportLanguage` again with the new language — the teacher then sees a green "switched" message. If it fails (e.g. a language pack is missing), call `ClassroomClientAPI.ReportLanguageFailed(code, reason)` — the teacher sees a red message with your reason.

Also call `ReportLanguage` when the user changes the language in your own menu, so the dashboard stays correct.

Example for apps that use Unity's **Localization** package (`com.unity.localization`). Put it on a GameObject in a scene that is never unloaded (e.g. your startup/bootstrap scene):

```csharp
using System.Collections;
using System.Linq;
using ClassroomClient.API;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class ClassroomLanguageSwitch : MonoBehaviour
{
    private const string SavedLanguageKey = "AppLanguage";

    private IEnumerator Start()
    {
        yield return LocalizationSettings.InitializationOperation; // wait until the languages are known

        string saved = PlayerPrefs.GetString(SavedLanguageKey, "");
        Locale savedLocale = string.IsNullOrEmpty(saved) ? null : LocalizationSettings.AvailableLocales.GetLocale(saved);
        if (savedLocale != null) LocalizationSettings.SelectedLocale = savedLocale;

        ClassroomEvents.OnLanguageChangeRequested += OnLanguageChangeRequested;
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        Report();
    }

    private void OnDestroy()
    {
        ClassroomEvents.OnLanguageChangeRequested -= OnLanguageChangeRequested;
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
    }

    // The teacher picked a language for this headset.
    private void OnLanguageChangeRequested(string code)
    {
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale == null)
        {
            ClassroomClientAPI.ReportLanguageFailed(code, "not_available");
            return;
        }
        LocalizationSettings.SelectedLocale = locale; // OnLocaleChanged saves and reports it
    }

    // Any language change — from the dashboard or from the app's own menu.
    private void OnLocaleChanged(Locale locale)
    {
        PlayerPrefs.SetString(SavedLanguageKey, locale.Identifier.Code);
        PlayerPrefs.Save();
        Report();
    }

    private void Report()
    {
        string current = LocalizationSettings.SelectedLocale != null ? LocalizationSettings.SelectedLocale.Identifier.Code : "";
        string[] available = LocalizationSettings.AvailableLocales.Locales.Select(l => l.Identifier.Code).ToArray();
        ClassroomClientAPI.ReportLanguage(current, available);
    }
}
```

If your app has its own localization system, the three steps are the same — only the "switch" and "save" lines change.

**Good to know:**
- Only the chosen headset is affected; the others keep their language.
- If your app has no handler, ClassroomClient answers "this app does not support switching the language" by itself. A code you did not list is answered with "not available".
- If no answer arrives within 15 seconds, the dashboard shows "no answer from the headset".
- ClassroomClient's own HUD notice ("waiting for approval") uses the language you report (English, German or Portuguese (Brazil); other languages → English).

Details: [PublicAPI.md → ReportLanguage](PublicAPI.md#reportlanguagestring-currentcode-string-availablecodes) and [OnLanguageChangeRequested](PublicAPI.md#onlanguagechangerequested).

---

## Advanced — Events and Status from Your App

All optional. Subscribe in `OnEnable`, unsubscribe in `OnDisable`:

```csharp
using ClassroomClient.API;
using UnityEngine;

public class ClassroomListener : MonoBehaviour
{
    void OnEnable()
    {
        ClassroomEvents.OnConnected += HandleConnected;         // registered, in the lobby
        ClassroomEvents.OnSessionStarted += HandleSessionStarted; // streaming starts
        ClassroomEvents.OnSessionEnded += HandleSessionEnded;     // back in the lobby
        ClassroomEvents.OnMessageReceived += HandleMessage;       // teacher sent a message
        ClassroomEvents.OnDisconnected += HandleDisconnected;     // connection lost
    }

    void OnDisable()
    {
        ClassroomEvents.OnConnected -= HandleConnected;
        ClassroomEvents.OnSessionStarted -= HandleSessionStarted;
        ClassroomEvents.OnSessionEnded -= HandleSessionEnded;
        ClassroomEvents.OnMessageReceived -= HandleMessage;
        ClassroomEvents.OnDisconnected -= HandleDisconnected;
    }

    private void HandleConnected() => Debug.Log("In the lobby");
    private void HandleSessionStarted() => Debug.Log("Session started");
    private void HandleSessionEnded() => Debug.Log("Session ended");
    private void HandleMessage(string text, string color, string category) => Debug.Log($"[{category}] {text}");
    private void HandleDisconnected() => Debug.Log("Disconnected");

    // Report the activity state to the dashboard, e.g. from your scenario code:
    public void ScenarioStarted() => ClassroomClientAPI.SetStatus(SessionStatus.InProgress);
    public void ScenarioFinished() => ClassroomClientAPI.SetStatus(SessionStatus.Completed);
}
```

For every method, event and enum see [PublicAPI.md](PublicAPI.md).

---

## Advanced — Settings in the Inspector

Select the `ClassroomClient` GameObject in the **Hierarchy** to see its settings in the **Inspector**:

- **ClassroomClientManager** — App Name, Camera Setup mode, Stream App Audio / Stream Microphone, Scene Library, Device Status update interval (default 10 s). Server URL and token are hidden here on purpose — change them by running the wizard again.
- **WebRTCConnection** — **Stream Quality**: 360p (500 kbps), **720p (2 Mbps, default)** or 1080p (4 Mbps).
- **ClassroomClient_HUD → ClassroomHUD** — the four HUD position values from the wizard.

Turning **Stream Microphone** on in the Inspector alone is not enough — the microphone code is only compiled in with the `CLASSROOMCLIENT_MICROPHONE` scripting define, which the wizard adds (a warning in the Console tells you if it is missing).
