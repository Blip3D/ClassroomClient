# ClassroomClient Public API Reference

This document covers every public API method, event, enum, and usage rule available to VR Developer application code. All types are in the `ClassroomClient.API` namespace.

---

## ClassroomClientAPI — Static Methods

`ClassroomClientAPI` provides static methods that can be called from any `MonoBehaviour` at any time. These calls are no-ops if `ClassroomClientManager` is not present in the scene.

---

### `SetStatus(SessionStatus status)`

**Signature**
```csharp
public static void SetStatus(SessionStatus status)
```

**What it does**
Sends the current session status from the VR device to the server. The status is visible in the supervisor dashboard alongside the device's stream tile.

**When to call it**
Call when a meaningful activity state change occurs in your application — for example, when a training scenario begins, ends, or encounters an error. Calling this is optional; it does not affect streaming or session management.

**Example**
```csharp
void StartTrainingScenario()
{
    // ... start your scenario logic ...
    ClassroomClientAPI.SetStatus(SessionStatus.InProgress);
}

void CompleteTrainingScenario()
{
    // ... complete your scenario logic ...
    ClassroomClientAPI.SetStatus(SessionStatus.Completed);
}
```

---

### `IsConnected()`

**Signature**
```csharp
public static bool IsConnected()
```

**What it does**
Returns `true` if the device is connected to the server (in any state other than `Disconnected`). Returns `false` if the server is unreachable or the device has not yet connected.

**Example**
```csharp
if (ClassroomClientAPI.IsConnected())
{
    // safe to send status updates
}
```

---

### `IsInSession()`

**Signature**
```csharp
public static bool IsInSession()
```

**What it does**
Returns `true` if the device is currently assigned to an active supervisor session (state is `InSession`). Returns `false` in all other states.

**Example**
```csharp
if (ClassroomClientAPI.IsInSession())
{
    // streaming is active
}
```

---

### `GetConnectionState()`

**Signature**
```csharp
public static ConnectionState GetConnectionState()
```

**What it does**
Returns the current `ConnectionState` enum value. Returns `ConnectionState.Disconnected` if `ClassroomClientManager` is not found. See the `ConnectionState` enum section below for all values and their meanings.

**Example**
```csharp
ConnectionState state = ClassroomClientAPI.GetConnectionState();
Debug.Log($"Current state: {state}");
```

---

## ClassroomEvents — Static C# Events

`ClassroomEvents` provides static events that fire when the connection state or server messages change. Subscribe in `OnEnable` and always unsubscribe in `OnDisable` to avoid memory leaks and double-firing after scene reloads.

---

### `OnConnected`

**Signature**
```csharp
public static event Action OnConnected
```

**When it fires**
When the device successfully registers with the server and enters the lobby. This fires after the server sends `registered` — meaning the device secret was accepted and the device is now visible in the supervisor's lobby list.

**Example**
```csharp
ClassroomEvents.OnConnected += () =>
{
    Debug.Log("Device is in the lobby");
};
```

---

### `OnSessionStarted`

**Signature**
```csharp
public static event Action OnSessionStarted
```

**When it fires**
When the device is assigned to a supervisor session and streaming begins. This fires when `ClassroomClientManager` receives `session_assigned` from the server.

**Example**
```csharp
ClassroomEvents.OnSessionStarted += () =>
{
    // supervisor can now see this device's stream
    ShowSessionStartedUI();
};
```

---

### `OnSessionEnded`

**Signature**
```csharp
public static event Action OnSessionEnded
```

**When it fires**
When the active session ends and the device returns to the lobby state. This fires when `ClassroomClientManager` receives `session_ended` from the server. Streaming stops automatically — no application code is needed to stop it.

**Example**
```csharp
ClassroomEvents.OnSessionEnded += () =>
{
    // device is back in lobby, no longer streaming
    HideSessionUI();
};
```

---

### `OnMessageReceived`

**Signature**
```csharp
public static event Action<string, string, string> OnMessageReceived
```

**Arguments**
| Argument | Type | Description |
|---|---|---|
| text | `string` | The message text sent by the supervisor |
| color | `string` | Display colour hint: `"blue"`, `"green"`, `"yellow"`, or `"red"` |
| category | `string` | Message category: `"general"`, `"technical"`, or `"safety"` |

**When it fires**
When the supervisor sends a message to this device. The built-in HUD overlay shows the message automatically — subscribing to this event is only needed if you want additional in-application handling (e.g. pausing the scenario, triggering audio, etc.).

**Example**
```csharp
ClassroomEvents.OnMessageReceived += (text, color, category) =>
{
    if (category == "safety")
    {
        PauseScenario();
    }
    Debug.Log($"[{category}] {text}");
};
```

---

### `OnMuteChanged`

**Signature**
```csharp
public static event Action<bool> OnMuteChanged
```

**Arguments**
| Argument | Type | Description |
|---|---|---|
| isMuted | `bool` | `true` = supervisor has muted this device, `false` = unmuted |

**When it fires**
When the supervisor toggles mute on this device. When muted, `ClassroomClientManager` automatically sets `AudioListener.volume = 0`. Subscribing to this event is only needed if you want additional behaviour beyond the volume change.

**Example**
```csharp
ClassroomEvents.OnMuteChanged += (isMuted) =>
{
    microphoneIndicator.SetActive(isMuted);
};
```

---

### `OnDisconnected`

**Signature**
```csharp
public static event Action OnDisconnected
```

**When it fires**
When the WebSocket connection to the server is lost. `ClassroomClientManager` will automatically attempt to reconnect with exponential backoff (starting at 1 second, capped at 30 seconds). This event fires each time the connection drops — it may fire multiple times during a reconnect cycle if the server remains unreachable.

**Example**
```csharp
ClassroomEvents.OnDisconnected += () =>
{
    ShowReconnectingIndicator();
};
```

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

| Value | HUD dot colour | Meaning |
|---|---|---|
| `Disconnected` | Red | Not connected to the server. Either the server is unreachable, the device secret was wrong, or the device was rejected. |
| `Connecting` | Yellow | WebSocket connection attempt is in progress. |
| `InLobby` | Green | Connected and registered. Waiting for the supervisor to start a session. |
| `InSession` | Blue | Assigned to an active session. Streaming is active. |
| `Reconnecting` | Yellow | Connection was lost and automatic reconnect is in progress. |
| `PendingApproval` | Red | Device secret was accepted but the Server Administrator has not yet approved this specific device. The device will retry automatically after approval. |

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
| `NotStarted` | The participant has not yet begun the activity. Default state. |
| `InProgress` | The participant has started the activity and it is running normally. |
| `Completed` | The participant has successfully finished the activity. |
| `Error` | The activity encountered an error or could not be completed. |

Pass these values to `ClassroomClientAPI.SetStatus()`. The value is sent to the server and shown in the supervisor dashboard.

---

## Usage Notes

**Subscribe in OnEnable, unsubscribe in OnDisable**

Always follow this pattern for all `ClassroomEvents` subscriptions:

```csharp
void OnEnable()
{
    ClassroomEvents.OnSessionStarted += HandleSessionStarted;
    ClassroomEvents.OnMessageReceived += HandleMessage;
}

void OnDisable()
{
    ClassroomEvents.OnSessionStarted -= HandleSessionStarted;
    ClassroomEvents.OnMessageReceived -= HandleMessage;
}
```

Because `ClassroomClientManager` persists across scene loads (`DontDestroyOnLoad`), events can fire after the subscribing `MonoBehaviour` is destroyed if you do not unsubscribe. Failing to unsubscribe in `OnDisable` will cause null reference errors and potential double-event handling after scene reloads.

**API calls are safe from any MonoBehaviour**

`ClassroomClientAPI` static methods can be called from any script at any time. If `ClassroomClientManager` is not present, the calls are silently ignored — they will not throw exceptions.

**Default HUD behaviour**

`ClassroomClientManager` handles the following automatically, with no application code required:

- Status dot colour updates on every state change
- Overlay notification display when `OnMessageReceived` fires (shows text for 6 seconds, then fades)
- `AudioListener.volume = 0` when muted, restored to 1 when unmuted

Subscribing to events only adds behaviour on top of these defaults — it does not replace them.
