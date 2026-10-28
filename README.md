# ClassroomClient

Unity package for VR headsets (Meta Quest and other Android-based standalone headsets). Part of the ClassroomController system.

ClassroomClient runs quietly in the background of a Unity VR app. It connects the headset to the ClassroomController server, streams what the headset user sees (and optionally hears) to the teacher's dashboard, and shows the teacher's messages inside the headset. Your app's own code does not need to change.

---

## Requirements

- **Unity 2022.3 LTS or Unity 6**, with the **Android Build Support** module (installed through Unity Hub)
- A Unity project that already runs as a VR app on your headset
- A Meta Quest (or other Android-based standalone headset) in developer mode, connected by USB
- A running ClassroomController server, plus its **server URL** and **server token** (ask your administrator)

Installed automatically with the package: `com.unity.webrtc` (3.0.0-pre.8 or newer) and `com.unity.textmeshpro` (3.0.6).
Optional: `com.unity.addressables` — if your project has it, ClassroomClient can also load Addressable scenes.

---

## Quick Start

1. In Unity, open **Window → Package Manager**.
2. Click **+** (top-left) → **Add package from git URL…** (Unity 6: **Install package from git URL…**).
3. Paste `https://github.com/Blip3D/ClassroomClient.git` and click **Add** (Unity 6: **Install**). Wait until Unity finishes compiling.
4. Open the first scene of your app (the one with your VR camera rig).
5. Open **Tools → Classroom Client → Quick Setup**.
6. Choose the **Server** (Raspberry Pi classroom `ws://192.168.50.1:8080`, blip3d.com cloud `wss://blip3d.com`, or Custom), paste the **Server Token**, type an **App Name**, and click **Setup ClassroomClient**.
7. On the **Configure Scene Library** page click **Finish**, then save the scene (**File → Save**).
8. Build for Android and install on the headset with **Build And Run** (Unity 2022.3: **File → Build Settings**; Unity 6: **File → Build Profiles**).
9. Start the app. The first time, an admin approves the headset in the dashboard under **Admin → Devices**. New headsets appear in the dashboard with a short device id (the last 4 characters of the hardware id); an admin gives them a name in **Admin → Devices → Rename**.
10. A teacher starts a session in the dashboard — the headset's view appears as a tile.

Full beginner-friendly guide, settings and troubleshooting: [`Documentation~/GettingStarted.md`](Documentation~/GettingStarted.md).

---

## Documentation

| Document | Contents |
|---|---|
| [`Documentation~/GettingStarted.md`](Documentation~/GettingStarted.md) | Installation, Setup Wizard, build, approval, troubleshooting, scene loading, camera modes, language switch |
| [`Documentation~/PublicAPI.md`](Documentation~/PublicAPI.md) | `ClassroomClientAPI`, `ClassroomEvents`, enums |
| [`Documentation~/NetworkArchitecture.md`](Documentation~/NetworkArchitecture.md) | Messages, WebRTC, STUN/TURN, ports, authentication |
| [`CHANGELOG.md`](CHANGELOG.md) | Version history |

---

## HUD Status Icon

A cast icon in the upper-right of the headset view shows the connection state:

| Colour | Meaning |
|---|---|
| Green | In a session — being streamed |
| Yellow | Connecting, or connected and waiting in the lobby |
| Blue | Waiting for an admin to approve this headset (or it was rejected) — approve it in **Admin → Devices** |
| Red | Disconnected |

A microphone icon appears next to it only while the headset microphone is being streamed. Teacher messages appear centred slightly below eye level.

---

## Good to Know

- **Audio:** the app's sound is streamed by default (the headset's own sound is unchanged). The microphone is optional and off by default ("Stream microphone" in the wizard). Dashboard tiles start muted — click the speaker button on a tile to listen; this never mutes the headset.
- **Scene changes:** if the app changes scene by itself, the stream follows the new camera automatically.
- **UI in the stream:** Screen Space – Overlay canvases are not captured. Use Screen Space – Camera or World Space canvases for UI the teacher should see.
- **One instance:** reloading the scene that contains ClassroomClient never creates a second copy.
- **Language switch (optional):** the teacher can change the language of a single headset from the dashboard — your app reports its languages and switches itself. See [GettingStarted → Language switch](Documentation~/GettingStarted.md#advanced--language-switch-from-the-dashboard).

---

## Optional App Integration

ClassroomClient works with zero code changes. If you want your app to react to sessions or report progress:

```csharp
using ClassroomClient.API;
using UnityEngine;

public class ClassroomHooks : MonoBehaviour
{
    void OnEnable()
    {
        ClassroomEvents.OnSessionStarted += HandleSessionStarted;
        ClassroomEvents.OnSessionEnded += HandleSessionEnded;
        ClassroomEvents.OnMessageReceived += HandleMessage;
    }

    void OnDisable()
    {
        ClassroomEvents.OnSessionStarted -= HandleSessionStarted;
        ClassroomEvents.OnSessionEnded -= HandleSessionEnded;
        ClassroomEvents.OnMessageReceived -= HandleMessage;
    }

    private void HandleSessionStarted() => ClassroomClientAPI.SetStatus(SessionStatus.InProgress);
    private void HandleSessionEnded() => Debug.Log("Session ended");
    private void HandleMessage(string text, string color, string category) => Debug.Log($"[{category}] {text}");
}
```

More (camera selection, custom scene loading, language switch, all events): [`Documentation~/PublicAPI.md`](Documentation~/PublicAPI.md).
