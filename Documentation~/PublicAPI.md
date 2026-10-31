# ClassroomClient Public API Reference

**Who this page is for:** developers who want their VR app's code to talk to ClassroomClient — for example to react when a session starts, report the app's progress, choose which camera is streamed, load scenes themselves, or let the teacher switch the app's language. You do **not** need any of this for a basic setup: ClassroomClient works with zero code changes (see [GettingStarted.md](GettingStarted.md)). Every method here is optional and safe to call at any time.

`ClassroomClientAPI`, `ClassroomEvents` and `SessionStatus` are in the `ClassroomClient.API` namespace; `ConnectionState`, `SceneInfo`, `SceneLibrary` and `ClassroomClientManager` are in `ClassroomClient.Core`.

---

## ClassroomClientAPI — Static Methods

`ClassroomClientAPI` can be called from any script at any time. If no `ClassroomClient` is present, the calls do nothing (no exception); getters return `null` / `false` / `ConnectionState.Disconnected`.

| Method | Description |
|---|---|
| `SetStatus(SessionStatus status)` | Sends the app's activity status to the dashboard. |
| `SetAvatarUrl(string url)` | Sets an avatar image URL sent when the headset registers. |
| `ReportCurrentScene(string sceneKey)` | Tells the dashboard which scene is showing. |
| `ReportSceneLoaded(string sceneKey)` | Reports that a Custom Loading scene finished loading. |
| `ReportSceneLoadFailed(string sceneKey, string reason = "")` | Reports that a Custom Loading scene failed to load. |
| `ReportLanguage(string currentCode, string[] availableCodes)` | Tells the dashboard the app's language and the languages it can switch to. |
| `ReportLanguageFailed(string requestedCode, string reason = "")` | Reports that switching to a requested language failed. |
| `SetStreamCamera(Camera camera)` | Chooses the viewpoint that is streamed. |
| `GetStreamCamera()` | Returns ClassroomClient's own capture camera. |
| `GetStreamSourceCamera()` | Returns the viewpoint camera the stream follows. |
| `IsConnected()` | `true` only when the headset is registered (lobby or in a session). |
| `IsInSession()` | `true` while in a session (being streamed). |
| `GetConnectionState()` | Returns the current `ConnectionState`. |

---

### `SetStatus(SessionStatus status)`

```csharp
public static void SetStatus(SessionStatus status)
```

Sends the current activity status (`NotStarted`, `InProgress`, `Completed`, `Error`) to the server; teachers see it on the headset's tile. Only sent while connected (ignored otherwise). Does not affect streaming.

```csharp
using ClassroomClient.API;

ClassroomClientAPI.SetStatus(SessionStatus.InProgress);
// ...
ClassroomClientAPI.SetStatus(SessionStatus.Completed);
```

---

### `SetAvatarUrl(string url)`

```csharp
public static void SetAvatarUrl(string url)
```

Stores an avatar image URL that is sent in the headset's registration message. Call it early (e.g. in `Awake`), before the headset registers; a later change is sent the next time the headset registers (after a reconnect). Should be an `https://` URL (the dashboard loads it as an image); this is not checked. Pass `""` for none.

---

### `ReportCurrentScene(string sceneKey)`

```csharp
public static void ReportCurrentScene(string sceneKey)
```

Sends `CURRENT_SCENE` so the dashboard shows which scene the headset is in, and remembers the value for later registrations / scene-library messages. ClassroomClient already does this after its own loads and when the app changes the active scene; call it yourself after a Custom Loading load (additive loads do not change the active scene).

---

### `ReportSceneLoaded(string sceneKey)`

```csharp
public static void ReportSceneLoaded(string sceneKey)
```

Sends `SCENE_LOADED` for the scene. Call it when your Custom Loading handler has finished loading. If `sceneKey` is a Scene Library entry marked **Custom Loading**, ClassroomClient also unloads the scene **it** loaded previously (so old and new content are not visible together).

---

### `ReportSceneLoadFailed(string sceneKey, string reason = "")`

```csharp
public static void ReportSceneLoadFailed(string sceneKey, string reason = "")
```

Sends `SCENE_LOAD_FAILED` with an optional reason; the teacher sees the failure in the dashboard. Call it from your Custom Loading handler when loading fails. The reason is plain text — do not use the `|` character in it.

```csharp
using System.Collections;
using ClassroomClient.API;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ReportExample : MonoBehaviour
{
    public IEnumerator Load(string sceneKey)
    {
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneKey, LoadSceneMode.Additive);
        if (op == null)
        {
            ClassroomClientAPI.ReportSceneLoadFailed(sceneKey, "Scene not in Build Settings");
            yield break;
        }
        yield return op;
        ClassroomClientAPI.ReportCurrentScene(sceneKey);
        ClassroomClientAPI.ReportSceneLoaded(sceneKey);
    }
}
```

A complete Custom Loading example is in [GettingStarted.md → Custom Loading](GettingStarted.md#custom-loading).

---

### `ReportLanguage(string currentCode, string[] availableCodes)`

```csharp
public static void ReportLanguage(string currentCode, string[] availableCodes)
```

Tells the dashboard which language the app is showing and which languages it can switch to. Languages are **locale codes** such as `"en"`, `"de"` or `"pt-BR"` (letters, optionally a dash and a region; never `|` or `,`). Invalid codes and duplicates are dropped.

- Call it **once at startup** and **again after every language change** (also changes the user makes in your own menu).
- It may be called before the headset is connected — even before ClassroomClient exists. ClassroomClient remembers it and sends it as soon as the headset is registered, and again after every reconnect.
- The dashboard shows a language menu on the headset's tile **only** for headsets that report languages, and only offers the languages in `availableCodes`.
- ClassroomClient also uses the reported language for its own HUD notice (English, German or Portuguese (Brazil)).

ClassroomClient never saves the language — your app does. Full example: [GettingStarted.md → Language switch from the dashboard](GettingStarted.md#advanced--language-switch-from-the-dashboard).

---

### `ReportLanguageFailed(string requestedCode, string reason = "")`

```csharp
public static void ReportLanguageFailed(string requestedCode, string reason = "")
```

Tells the dashboard that a requested language could not be set, e.g. a language pack is missing or failed to download. The teacher sees a red message with your reason. Pass `"not_available"` to get the dashboard's translated text "this language is not available on the headset"; any other reason is shown as you wrote it (plain text, no `|`).

---

### `SetStreamCamera(Camera camera)`

```csharp
public static void SetStreamCamera(Camera camera)
```

Chooses the **viewpoint** that is streamed. The camera you pass is a source only — it is **never captured directly, never given a RenderTexture and never disabled**, so passing the headset's eye camera is safe. ClassroomClient creates (or reuses) its own capture camera named `StreamingCamera` as a child of it, copying clear flags, background colour, culling mask, field of view and clip planes, and streams that.

- Works in every state. If a stream is running, the existing video track is handed to the new camera (no interruption, no renegotiation).
- **API Controlled mode:** nothing is streamed until you call this. If a session starts first, streaming waits and starts automatically on the call.
- Passing `null` does nothing.
- You do not need to call it after scene changes in **Automatic** mode — the stream re-attaches to the new `Camera.main` by itself.

```csharp
using ClassroomClient.API;
using UnityEngine;

public class OverheadView : MonoBehaviour
{
    [SerializeField] private Camera overheadCamera;
    [SerializeField] private Camera playerCamera;

    public void ShowOverhead() => ClassroomClientAPI.SetStreamCamera(overheadCamera);
    public void ShowPlayer() => ClassroomClientAPI.SetStreamCamera(playerCamera);
}
```

---

### `GetStreamCamera()`

```csharp
public static Camera GetStreamCamera()
```

Returns ClassroomClient's own capture camera (the `StreamingCamera` that renders the stream), or `null` if none exists yet. Do not modify or disable it.

---

### `GetStreamSourceCamera()`

```csharp
public static Camera GetStreamSourceCamera()
```

Returns the viewpoint camera the stream currently follows (the camera passed to `SetStreamCamera`, or the main camera in Automatic mode), or `null` if none is set.

```csharp
using ClassroomClient.API;
using UnityEngine;

public class EnsureViewpoint : MonoBehaviour
{
    [SerializeField] private Camera replacement;

    public void Check()
    {
        Camera source = ClassroomClientAPI.GetStreamSourceCamera();
        if (source == null || !source.gameObject.activeInHierarchy)
            ClassroomClientAPI.SetStreamCamera(replacement);
    }
}
```

---

### `IsConnected()`

```csharp
public static bool IsConnected()
```

Returns `true` only when the headset is **registered with the server** — state `InLobby` or `InSession`. It is `false` while connecting, reconnecting or waiting for admin approval (`PendingApproval`), so it is safe to use for a "connected to class" indicator. For the exact state use `GetConnectionState()`.

> Changed in 2.75.0: earlier versions returned `true` in every state except `Disconnected`.

---

### `IsInSession()`

```csharp
public static bool IsInSession()
```

Returns `true` while the state is `InSession` (the headset belongs to a running session and is streamed).

---

### `GetConnectionState()`

```csharp
public static ConnectionState GetConnectionState()
```

Returns the current `ConnectionState` (see below).

```csharp
using ClassroomClient.API;
using ClassroomClient.Core;
using UnityEngine;

public class StateLogger : MonoBehaviour
{
    void Start()
    {
        ConnectionState state = ClassroomClientAPI.GetConnectionState();
        Debug.Log($"ClassroomClient state: {state}");
    }
}
```

---

## ClassroomEvents — Static C# Events

Static events in `ClassroomClient.API`. They are raised on Unity's main thread. Subscribe in `OnEnable` and unsubscribe in `OnDisable` (ClassroomClient lives across scene changes, so a forgotten subscription keeps calling a destroyed object).

| Event | Signature | Raised when |
|---|---|---|
| `OnConnected` | `Action` | The server accepted the headset (`registered`) — it is in the lobby. Raised again after every reconnect. |
| `OnSessionStarted` | `Action` | The headset was assigned to a session; streaming starts. |
| `OnSessionEnded` | `Action` | The session ended; streaming stops automatically and the headset returns to the lobby. |
| `OnMessageReceived` | `Action<string, string, string>` | A teacher sent a message: `(text, color, category)`. |
| `OnDisconnected` | `Action` | The connection to the server was lost (not raised while the headset is waiting for approval). ClassroomClient reconnects by itself (backoff 1 s doubling up to 30 s); the event may fire repeatedly while the server stays unreachable. |
| `OnStreamCameraLost` | `Action` | **API Controlled mode only:** the streamed viewpoint camera was destroyed or its GameObject deactivated (e.g. the app changed scene). |
| `OnLoadSceneRequested` | `Action<SceneInfo>` | A teacher requested a scene marked **Custom Loading**. |
| `OnSceneLoadCompleted` | `Action<string>` | ClassroomClient finished loading one of **its** scenes (scene key). Also raised when the requested scene was already loaded. |
| `OnSceneLoadFailed` | `Action<string, string>` | A teacher-requested load failed inside ClassroomClient: `(sceneKey, reason)`. |
| `OnLanguageChangeRequested` | `Action<string>` | The teacher picked a language for **this** headset in the dashboard (locale code, e.g. `"de"`). |
| `OnMuteChanged` | `Action<bool>` | **Obsolete — never raised** (see below). |

### `OnMessageReceived`

| Argument | Description |
|---|---|
| `text` | The message text. |
| `color` | Colour hint chosen in the dashboard. The built-in HUD shows `"red"` and `"green"` with a coloured background, anything else dark grey. |
| `category` | `"general"`, `"technical"` or `"safety"`. |

The built-in HUD already shows every message for about 6 seconds; subscribe only for extra behaviour (e.g. pausing the scenario on a safety message).

```csharp
using ClassroomClient.API;
using UnityEngine;

public class SafetyPause : MonoBehaviour
{
    void OnEnable() => ClassroomEvents.OnMessageReceived += HandleMessage;
    void OnDisable() => ClassroomEvents.OnMessageReceived -= HandleMessage;

    private void HandleMessage(string text, string color, string category)
    {
        if (category == "safety")
            Time.timeScale = 0f; // replace with your own pause logic
    }
}
```

### `OnStreamCameraLost`

Checked while the headset is in a session (every 0.5 s and on every scene change). Call `ClassroomClientAPI.SetStreamCamera(newCamera)` from the handler. If you do not set a camera within about 3 seconds, the stream falls back to `Camera.main` so it never stays black; a later `SetStreamCamera` call replaces the fallback. Not raised in Automatic mode (there the stream re-attaches to `Camera.main` by itself), and not raised in API Controlled mode before you have ever called `SetStreamCamera`. Example: [GettingStarted.md → Choosing the streamed camera](GettingStarted.md#advanced--choosing-the-streamed-camera).

### `OnLoadSceneRequested`

Load `info.sceneKey` yourself, then call `ReportSceneLoaded` or `ReportSceneLoadFailed`. If nothing is subscribed, or a handler throws, ClassroomClient reports the load as failed. The `OnLoadSceneRequested` instance event on the `ClassroomSceneManager` component still exists and is raised as well.

`SceneInfo` fields: `displayName`, `sceneKey`, `loadType` (`SceneLoadType.Standard` / `SceneLoadType.Addressable`), `category`, `customLoading`.

### `OnSceneLoadCompleted` / `OnSceneLoadFailed`

Only cover loads ClassroomClient performs itself (and its own checks: unknown load type, busy, no custom loader, custom loader threw). Your own `ReportSceneLoaded` / `ReportSceneLoadFailed` calls do **not** raise them. Recommended use: unload your custom-loaded scene in `OnSceneLoadCompleted` (see the Custom Loading example in GettingStarted).

### `OnLanguageChangeRequested`

Raised only on the headset the teacher chose; other headsets are not affected. Switch your app's language, **save it** (so it survives a restart), then call `ClassroomClientAPI.ReportLanguage(...)` with the new current language — that is how the dashboard learns it worked. Report **exactly the code you received** (e.g. `"pt-BR"`, not `"pt"`; letter case does not matter), otherwise the dashboard cannot match the answer to the request. If it cannot be done, call `ReportLanguageFailed(code, reason)`.

ClassroomClient answers the dashboard by itself (your handler is not called) when:

| Situation | Reason the teacher sees |
|---|---|
| Nothing is subscribed to this event | `not_supported` — "this app does not support switching the language" |
| The code is not in the list you passed to `ReportLanguage` | `not_available` |
| The code is invalid | `invalid_code` |
| Your handler throws an exception | "Language handler threw: …" |

As soon as your handler has run (without an exception), ClassroomClient confirms receipt to the dashboard by itself (2.77.0): the teacher sees "… is switching to …" and the dashboard waits up to **60 seconds** for your `ReportLanguage` / `ReportLanguageFailed` — enough time to download a language pack. You do not need to answer inside the handler; answer whenever the switch is done. Without any receipt (e.g. headset offline, or a package older than 2.77.0) the dashboard gives up after 15 seconds ("no answer from the headset"); after a receipt but no answer within 60 seconds it shows "the headset started switching but did not confirm it".

### `OnMuteChanged` (obsolete)

```csharp
[Obsolete] public static event Action<bool> OnMuteChanged
```

Removed in 2.75.0 and **never raised**. The dashboard's mute button now mutes only the stream playback in that browser tile and never affects the headset. The event is kept so existing code still compiles (with an obsolete warning). **Migration:** delete your subscription; if your app muted or unmuted itself in response, remove that logic — the headset's sound is no longer controlled by the dashboard.

---

## ConnectionState Enum

Namespace: `ClassroomClient.Core`

```csharp
public enum ConnectionState
{
    Disconnected,
    Connecting,
    InLobby,
    InSession,
    Reconnecting,
    PendingApproval
}
```

| Value | HUD cast icon | Meaning |
|---|---|---|
| `Disconnected` | Red | Not connected (no server URL, or registration rejected, e.g. wrong server token). |
| `Connecting` | Yellow | Connection / registration in progress. |
| `InLobby` | Yellow | Registered; waiting for a teacher to start a session. |
| `InSession` | Green | In a session; being streamed. |
| `Reconnecting` | Yellow | Connection lost; reconnecting automatically. |
| `PendingApproval` | Blue | Server token accepted, but an admin has not approved this headset yet (Admin → Devices). The headset joins as soon as it is approved. The one-time HUD notice appears in the app's language (see `ReportLanguage`). |

---

## SessionStatus Enum

Namespace: `ClassroomClient.API`

```csharp
public enum SessionStatus
{
    NotStarted,
    InProgress,
    Completed,
    Error
}
```

| Value | When to use |
|---|---|
| `NotStarted` | The activity has not begun yet. |
| `InProgress` | The activity is running. |
| `Completed` | The activity was finished successfully. |
| `Error` | The activity hit an error or could not be completed. |

---

## ClassroomClientManager.Instance

```csharp
public static ClassroomClientManager Instance { get; }
```

The single persistent `ClassroomClientManager` (namespace `ClassroomClient.Core`), or `null` before it has started. There is always at most one: if a scene containing a `ClassroomClient` GameObject is loaded again, the new copy deactivates and destroys itself. Integrators normally do not need it — use `ClassroomClientAPI` instead.

---

## Usage Notes

- **Subscribe in `OnEnable`, unsubscribe in `OnDisable`** for every `ClassroomEvents` event.
- **API calls never throw** when ClassroomClient is missing or offline; messages are simply not sent.
- **Built-in behaviour (no code needed):** HUD status icon updates, message display, microphone icon, stream re-attach after scene changes (Automatic mode), scene-load reporting for ClassroomClient's own loads.
- **UI in the stream:** the stream is captured by a camera, so **Screen Space – Overlay** canvases do not appear in it. Use **Screen Space – Camera** or **World Space** canvases for UI the teacher should see.
