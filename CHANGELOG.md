# Changelog

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
