# Changelog

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
