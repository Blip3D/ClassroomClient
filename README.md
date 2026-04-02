# ClassroomClient

Unity package for Meta Quest VR devices. Part of the ClassroomController system.

ClassroomClient runs silently in the background of any Unity VR application. It connects to the ClassroomController server, streams the headset camera view to the supervisor dashboard, and displays overlay notifications. The VR application does not need to be modified.

---

## Roles

**Supervisor** — operates the PWA dashboard, monitors streams, starts and ends sessions.

**Participant** — wears the VR headset. Their view is streamed to the supervisor.

**VR Developer** — integrates this package into a Unity project.

**Server Administrator** — deploys and maintains the ClassroomController server.

---

## Requirements

- Unity 2021.3 or newer
- Meta Quest (any model) or Android XR device
- A running ClassroomController server

---

## Package Structure

```
Assets/ClassroomClient/
  Runtime/
    Core/
      ClassroomClientManager.cs     Singleton MonoBehaviour — state machine, message handler
      ConnectionState.cs            Enum: Disconnected | Connecting | InLobby | InSession | Reconnecting | PendingApproval
    Networking/
      WebSocketClient.cs            WebSocket connection with reconnect and send queue
      WebRTCConnection.cs           RTCPeerConnection, VideoStreamTrack, ICE handling
    HUD/
      ClassroomHUD.cs               World space canvas — follows camera, hosts status dot and notifications
      StatusIndicator.cs            Coloured dot showing connection state
      NotificationPanel.cs          Timed notification overlay with fade-out
    API/
      ClassroomClientAPI.cs         Static public API — the only surface the VR app needs to touch
      ClassroomEvents.cs            Static C# events for the VR app to subscribe to
      SessionStatus.cs              Enum: NotStarted | InProgress | Completed | Error
    Utilities/
      DeviceStatusProvider.cs       Battery level, WiFi signal, charging state (Android native)
  Editor/
    SimpleSetupWizard.cs            Quick setup wizard: Tools → Classroom Client → Quick Setup
```

---

## Quick Start

See [`Documentation~/GettingStarted.md`](Documentation~/GettingStarted.md) for full setup instructions.

Short version:
1. Window → Package Manager → Add package from git URL: https://github.com/Blip3D/ClassroomClient.git
2. Tools → Classroom Client → Quick Setup
3. Fill in Server URL and Device Secret → click Setup ClassroomClient
4. Build and deploy to Quest

---

## Documentation

| Document | Contents |
|---|---|
| [`Documentation~/GettingStarted.md`](Documentation~/GettingStarted.md) | Installation, wizard, configuration, build |
| [`Documentation~/PublicAPI.md`](Documentation~/PublicAPI.md) | ClassroomClientAPI, ClassroomEvents, enums |
| [`Documentation~/NetworkArchitecture.md`](Documentation~/NetworkArchitecture.md) | WebRTC flow, ports, TURN, authentication |

---

## HUD Status Dot

| Colour | Meaning |
|---|---|
| Red | Disconnected |
| Yellow | Connecting or reconnecting |
| Green | Connected — in lobby |
| Blue | In active session — streaming |

---

## Optional VR App Integration

ClassroomClient works with zero code changes. These are optional:

```csharp
using ClassroomClient.API;

// Subscribe in OnEnable, unsubscribe in OnDisable
ClassroomEvents.OnSessionStarted += () => { };
ClassroomEvents.OnSessionEnded += () => { };
ClassroomEvents.OnMessageReceived += (text, color, category) => { };
ClassroomEvents.OnMuteChanged += (isMuted) => { };
ClassroomEvents.OnDisconnected += () => { };

// Send session status to server
ClassroomClientAPI.SetStatus(SessionStatus.InProgress);
```

---

## Server Repository

[ClassroomController](https://git.rwth-aachen.de/blib3d/blip3d_development/classroomcontroller_pwa) — Node.js server + PWA dashboard

**ClassroomClient package URL:** https://github.com/Blip3D/ClassroomClient.git
