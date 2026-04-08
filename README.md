# ClassroomClient

Unity package for Meta Quest VR devices. Part of the ClassroomController system.

ClassroomClient runs silently in the background of any Unity VR application. It connects to the ClassroomController server, streams the headset camera view to the supervisor dashboard, and displays overlay notifications. The VR application does not need to be modified.

---

## Requirements

- Unity 2021.3 or newer
- Meta Quest (any model) or Android XR device
- A running ClassroomController server



## Quick Start

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
| Green | Connected — streaming |


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

