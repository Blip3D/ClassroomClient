# Changelog

## [2.75.0] — 2026-10-26

Field-test fixes (World of VR on blip3d.com) + streamed audio. Requires ClassroomController server/PWA 2.75.0 for audio and instant approval.

### Added
- **Streamed audio:** the app's sound is sent to the dashboard together with the video (`streamAppAudio`, default on). The headset's own sound is never changed.
- **Microphone streaming** (`streamMicrophone`, default off): the Setup Wizard's "Stream microphone" option adds the `CLASSROOMCLIENT_MICROPHONE` scripting define, so the Android microphone permission is only added to apps that use it. A microphone icon appears in the HUD while the microphone is streamed. Skipped (with a warning) if the app is already recording.
- `ClassroomEvents.OnStreamCameraLost` (API Controlled mode): raised when the streamed camera is destroyed or deactivated, e.g. after an app scene change. If no new camera is set within ~3 s, the stream falls back to the main camera.
- Static scene events `ClassroomEvents.OnLoadSceneRequested`, `OnSceneLoadCompleted`, `OnSceneLoadFailed`.
- HUD `Distance` / `Height` settings (wizard sliders); default 0.6 m in front, 0.10 m below eye level.
- HUD status shown as a cast icon: green = in session, yellow = lobby/connecting, **blue = waiting for admin approval** (plus a one-time notice), red = disconnected.
- Headsets use the STUN/TURN servers advertised by the server (`/api/ice-config`, e.g. the Raspberry Pi's TURN relay), falling back to Google STUN.

### Fixed
- **Stream went black after the app changed scene** until the session was restarted. A camera change now hands the existing stream texture to the new camera (no new track, no renegotiation), app-driven scene changes are detected, and Automatic mode re-attaches to the new main camera.
- **Addressable scenes never loaded** ("Addressables package not installed" even when it was): the optional Addressables support is now actually compiled in when the package is present.
- Scene loading only unloads scenes ClassroomClient itself loaded — the app's startup/rig scene is never unloaded. Loading an already-loaded scene no longer creates a duplicate. A second load while one is running is rejected instead of leaving scenes behind.
- Every failed scene load is now reported to the dashboard (unknown scene, missing component, no custom loader registered, custom loader threw, Addressables error).
- HUD messages appeared far above eye level (the panel was anchored to the top of a 0.6 m canvas).
- A headset waiting for approval no longer reconnects every few seconds (which exhausted the server's rate limit); it registers immediately when an admin approves it.
- Package compiles again on Unity 2022 LTS (TextMeshPro word-wrapping API).
- **Only one ClassroomClient instance:** when the app reloaded the scene containing ClassroomClient (e.g. returning to its menu), a second copy stayed alive — two connections with the same headset id caused a black stream, two status icons and lost scene results. Duplicates now remove themselves immediately (`ClassroomClientManager.Instance`).
- Custom Loading: when the app reports its custom scene as loaded, ClassroomClient unloads the scene it had loaded before (previously both stayed visible).
- Device status (battery, Wi-Fi) is sent only after registration — the first report was previously ignored by the server.
- Setup Wizard: ticking "Stream microphone" no longer interrupts the wizard (the scripting define is applied when you click Finish).

### Changed
- `ClassroomClientAPI.IsConnected()` now returns `true` only when the headset is registered (lobby or in a session). Before, it was also `true` while connecting, reconnecting or waiting for admin approval, so "connected" indicators could be wrong. Use `GetConnectionState()` for the exact state.
- Setup Wizard: the **Device Name** field was removed — a name baked into the build was shared by every headset. Headsets now appear with a short id; name them in the dashboard (Admin → Devices → Rename).
- Status icons moved to the upper-right of the view (`hudIconHeight` / `hudIconSideOffset`); messages stay centred.
- Minimum Unity version is now 2022.3 (tested on 2022.3 LTS and Unity 6).

### Removed
- The dashboard mute button no longer mutes the headset (it now mutes only the stream in the browser). `ClassroomEvents.OnMuteChanged` is kept as an obsolete stub that is never raised.
- Unused Meta NativeWebSocket assembly reference.

## [2.74.0] — 2026-06-19

### Added
- Quick Setup wizard **Server** preset dropdown: pick Raspberry Pi classroom (`ws://192.168.50.1:8080`) or blip3d.com cloud (`wss://blip3d.com`), or Custom to type any URL. Editing the URL by hand switches the dropdown to Custom. Presets are hardcoded; the URL is still saved to the manager exactly as before.

## [2.73.0] — 2026-06-19

### Changed
- Version alignment (lockstep) with the ClassroomController server/PWA 2.73.0 release — per-session device cap removed server-side. No functional ClassroomClient changes.

## [2.72.0] — 2026-06-19

### Changed
- Version alignment (lockstep) with the ClassroomController server/PWA 2.72.0 release — device approval lifecycle + security hardening. No functional ClassroomClient changes.

## [2.71.0] — 2026-06-17

### Added
- Quick Setup wizard **Camera Setup** mode: **Automatic** (ClassroomClient creates a dedicated streaming camera that follows the main/XR camera — for normal projects) and **API Controlled** (call `ClassroomClientAPI.SetStreamCamera(camera)` at runtime to choose the streamed viewpoint — for dynamic / Addressable-loaded camera rigs).
- `ClassroomClientAPI.GetStreamSourceCamera()` — returns the developer-set source/viewpoint camera.

### Fixed
- Streaming no longer hijacks the HMD/eye camera (T15). `SetStreamCamera(camera)` now treats the passed camera as a source/viewpoint; ClassroomClient renders a dedicated, internally-marked capture camera that follows it. The source camera is never given a RenderTexture and never disabled — even if an eye camera was serialized into the field.
- API Controlled mode: starting a session before a camera is set no longer errors; streaming waits and begins automatically once `SetStreamCamera` is called.
- Dedicated capture RenderTexture now has a depth buffer (Unity 6 Render Graph requirement) and the dedicated camera renders only while streaming.

## [2.70.0] — 2026-06-17

### Changed
- Version alignment (lockstep) with the ClassroomController server/PWA 2.70.0 release.
- Setup Wizard Scene Library page: "+ Add Addressable Scene" is shown only when the Addressables package is installed; Custom Loading tooltip corrected (it means "the app loads the scene itself", not "Addressables").
- Setup Wizard help text lists the Raspberry Pi classroom URL `ws://192.168.50.1:8080`.

### Fixed
- Addressable scene loads no longer call `SetActiveScene` (HMD freeze with the OVR Camera Rig); the previous Addressable scene is hidden before it is unloaded.

## [2.69.0] — 2026-04-15

Includes the v2.0.0-beta.1 work (2026-04-10 / 2026-04-13).

### Added
- Remote scene loading: `SceneLibrary` asset, `SceneInfo`, `ClassroomSceneManager` (Standard via `SceneManager`, Addressable via `Addressables`, or Custom Loading via the `OnLoadSceneRequested` event). Handles `LOAD_SCENE` and `REQUEST_CONTENT_LIBRARY`; sends `CONTENT_LIBRARY` after registration.
- Setup Wizard second page: Configure Scene Library.
- `ClassroomClientAPI.SetAvatarUrl`, `ReportCurrentScene`, `ReportSceneLoaded`, `ReportSceneLoadFailed` (previously stubs), `GetStreamCamera`, `SetStreamCamera`.
- Stream quality setting on `WebRTCConnection`: 360p (500 kbps), 720p (2 Mbps, default), 1080p (4 Mbps). Replaces the `streamWidth` / `streamHeight` / `stunServer` fields.

### Changed
- `SetStatus` sends the enum name as-is (e.g. `InProgress`) instead of lower case.
- Scenes are loaded additively; the previous content scene is hidden, then unloaded. `SetActiveScene` is not called (HMD freeze with the OVR Camera Rig).

### Fixed
- An ANSWER arriving before the offer was fully set is no longer dropped (short wait for the expected signaling state).
- Up to 50 queued WebSocket messages are processed per frame (was 10).
- `SetStreamCamera` during a running stream swaps the video track.

## [1.70.0] — 2026-04-08

### Changed
- `deviceSecret` renamed to `serverToken` (manager, WebSocket client, wizard). Server URL, server token and device name are hidden in the Inspector; set them with the Setup Wizard.
- Setup Wizard: new **Device Name** field; server URL and token no longer pre-filled.
- HUD redesign: follows the camera every frame (also before rendering), status indicator at the top, notification panel below it.

### Removed
- Unused POV-capture fields from the registration message.

## [1.69.1] - 2026-04-02

### Fixed
- Removed dependency on Meta XR SDK (`Meta.Net.NativeWebSocket`)
- Embedded NativeWebSocket as `ClassroomClient.Internal.WebSocket` — package now works in any Unity VR project regardless of installed SDKs
- Fixed obsolete `FindFirstObjectByType` replaced with `FindAnyObjectByType`

## [1.69.0] — 2026-04-01

### Added
- ClassroomClientManager — singleton MonoBehaviour, WebSocket state machine, message handler
- WebSocketClient — NativeWebSocket wrapper with reconnect, exponential backoff, send queue
- WebRTCConnection — RTCPeerConnection, VideoStreamTrack, ICE, codec selection
- ClassroomHUD — world space canvas, follows camera, hosts status dot and notification panel
- StatusIndicator — coloured dot showing connection state (red/yellow/green/blue)
- NotificationPanel — timed notification overlay with fade-out (6 second display)
- ClassroomClientAPI — static public API surface
- ClassroomEvents — static C# events for VR app integration
- ConnectionState enum — Disconnected | Connecting | InLobby | InSession | Reconnecting | PendingApproval
- SessionStatus enum — NotStarted | InProgress | Completed | Error
- DeviceStatusProvider — battery, WiFi signal, charging state via Android native
- SimpleSetupWizard — Editor window for one-click setup (Tools → Classroom Client → Quick Setup)
- Auto-reconnect with exponential backoff (1s base, 30s cap)
- Pending approval retry loop
- session_assigned guard — prevents duplicate streaming when supervisor reconnects mid-session
