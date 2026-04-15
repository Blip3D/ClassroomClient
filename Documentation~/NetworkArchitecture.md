# Network Architecture

This document explains how ClassroomController works under the hood. It is intended for two audiences:

**Server Administrator** — responsible for deploying the server, configuring firewalls, and ensuring the network is set up correctly for the system to function.

**VR Developer** — integrating ClassroomClient into a Unity application and wanting to understand what the package does on the network so they can make informed decisions about configuration and troubleshooting.

---

## Overview

```
VR Device (Quest)         Node.js Server           Supervisor (Browser)
      │                        │                           │
      │──── WebSocket ─────────│──── WebSocket ────────────│
      │   registration,        │   session management,     │
      │   session events,      │   device list,            │
      │   messages, mute,      │   messages, mute,         │
      │   ping/pong            │   signaling relay         │
      │                        │                           │
      │◄═══════════════════════╪══ WebRTC video (P2P) ════►│
                               │   (server is not in
                               │    the video path)
```

Both the VR device and the supervisor's browser maintain a persistent WebSocket connection to the server. All control and coordination traffic — device registration, session management, messaging, muting — travels through the server on these WebSocket connections.

Video never goes through the server. Once the WebRTC connection is established between the VR device and the supervisor's browser, video travels directly peer-to-peer. The server only facilitates the initial handshake (offer, answer, and ICE candidates) — it does not see or relay the video stream.

---

## What Goes Through the Server

Every message that is not video passes through the server:

**Device → Server**
- `register_device` — sent on every connection. Contains device ID, device model, app name, bundle ID, server token, and the device's scene library (list of scenes available for remote loading). The server validates the token and either registers or rejects the device.
- `DEVICE_STATUS` — sent every 10 seconds while connected. Contains battery level (0–100), WiFi signal strength (0–4), and charging state. Shown in the supervisor dashboard.
- `session_status` — optional. Sent by the VR application via `ClassroomClientAPI.SetStatus()` to report the current activity state.
- `CURRENT_SCENE` — sent after registration to tell the server which scene is currently active. Also sent after every scene load.
- `SCENE_LOADED` — confirms a scene finished loading successfully (triggered automatically by ClassroomClient, or manually via `ClassroomClientAPI.ReportSceneLoaded()` when using custom loading).
- `SCENE_LOAD_FAILED` — reports that a scene load failed, with a reason string (triggered automatically by ClassroomClient, or manually via `ClassroomClientAPI.ReportSceneLoadFailed()`).
- `ping` — sent every 15 seconds to keep the connection alive. The server responds with `pong`. If the server receives no message from a device for 45 seconds, the device is marked offline.
- WebRTC signaling: `OFFER` and `CANDIDATE` messages (pipe-delimited format) forwarded to the supervisor.

**Server → Device**
- `registered` — server token accepted, device is now in the lobby.
- `rejected` — either wrong secret, or device is pending approval by the Server Administrator.
- `session_assigned` — device has been added to an active session. Triggers streaming start.
- `session_ended` — session has ended. Triggers streaming stop.
- `stream_request` — supervisor reconnected mid-session. Device creates a fresh WebRTC offer.
- `message` — supervisor sent a text message to this device. Contains text, colour, and category.
- `mute` — supervisor toggled mute. Contains `value: true/false`.
- `LOAD_SCENE` — supervisor requested a scene change on this device. Contains the scene key. ClassroomClient loads the scene automatically unless `customLoading = true`, in which case `OnLoadSceneRequested` fires and the VR application handles loading.
- `REQUEST_CONTENT_LIBRARY` — server requests a fresh copy of the device's scene library. Device responds by re-sending its full library.
- `pong` — keep-alive response.
- WebRTC signaling: `ANSWER` and `CANDIDATE` messages forwarded from the supervisor.

**Supervisor → Server**
- `register_controller` — sent on WebSocket connect. Contains the supervisor's JWT token for authentication.
- `session_ready` — sent after the supervisor mounts the session view. The server holds `session_assigned` for devices until this is received, ensuring the supervisor's WebRTC listeners are ready before streaming begins.
- WebRTC signaling: `ANSWER` and `CANDIDATE` forwarded to the target device.

**Server → Supervisor**
- `controller_registered` — full device list sent on connect.
- `device_connected`, `device_disconnected`, `device_updated` — real-time lobby updates.
- `session_created` — session confirmed with the list of assigned devices.
- WebRTC signaling: `OFFER` and `CANDIDATE` forwarded from the target device.

---

## What Is Peer-to-Peer

After the WebRTC handshake completes, all video travels directly from the VR device to the supervisor's browser. The path is:

```
VR Device → [internet or local network] → Supervisor's Browser
```

The server is completely out of the video path. This means:
- Server bandwidth requirements are minimal (signaling only)
- Video latency is as low as the network between the VR device and supervisor allows
- Up to 10 simultaneous streams per session at 720p/2 Mbps each are supported

If a direct peer-to-peer path cannot be found (see TURN section below), a TURN server relays the video instead. In that case the video path becomes:

```
VR Device → TURN Server → Supervisor's Browser
```

---

## Ports

| Port | Protocol | Used for | Required |
|---|---|---|---|
| 443 | TCP | HTTPS and WSS — nginx proxies to the Node.js server | Yes (production with SSL) |
| 8080 | TCP | Direct HTTP and WS — Node.js server without nginx | Yes (development / no nginx) |
| 3478 | UDP + TCP | TURN relay — only if a TURN server is deployed | Only on strict NAT networks |

WebRTC media uses ephemeral UDP ports negotiated dynamically. These are allocated by the operating system and do not need to be manually opened.

---

## Why TURN Is Sometimes Needed

WebRTC establishes peer-to-peer connections using a process called ICE (Interactive Connectivity Establishment). ICE tries multiple paths between the two peers to find one that works. On most home and office networks this succeeds automatically — the two devices find each other through their routers using a technique called STUN.

Some networks are more restrictive. Specifically:

- University WiFi (including Eduroam)
- Corporate networks with symmetric NAT or strict firewall rules
- Mobile hotspots

On these networks, STUN alone is not enough. The router does not allow the inbound connection that WebRTC needs to establish direct peer-to-peer communication. Without a fallback, the video stream never starts.

A TURN server provides that fallback. When ICE cannot find a direct path, it routes video through the TURN server instead. The VR device sends video to the TURN server, and the TURN server forwards it to the supervisor's browser. The video path is no longer peer-to-peer, but it works on any network.

**Practical implication:** If the VR device is on a university WiFi network and streams are not connecting, a TURN server is almost certainly the solution. Configure `TURN_SERVER`, `TURN_USERNAME`, and `TURN_PASSWORD` in `server/.env` — the server passes these credentials to the supervisor browser via `GET /api/ice-config`, which the browser's WebRTC stack uses automatically.

---

## Authentication

**Supervisor login — JWT**

When a supervisor logs in with their email and password, the server verifies the password hash and returns a JWT (JSON Web Token). This token is stored in the browser and sent with every subsequent API request and WebSocket connection. The token is valid for 8 hours. When it expires, the supervisor is automatically logged out and must log in again. The JWT is signed with a secret known only to the server — it cannot be forged.

**VR device authentication — Server Token**

VR devices have no user accounts and no login flow. Instead, every device sends a shared secret string (`SERVER_TOKEN`) in its `register_device` message. The server checks whether this matches the configured value. If it does not match, the connection is rejected immediately.

The server token is a fleet-wide credential — every Quest headset in a deployment uses the same value. It is the VR Developer's responsibility to set the correct value in the Unity Inspector, and the Server Administrator's responsibility to keep it private.

**Why both mechanisms exist:** Supervisors are individual humans with personal accounts and sessions. JWT lets the server distinguish between multiple supervisors and scope their access accordingly — Supervisor A cannot see Supervisor B's sessions. VR devices are interchangeable hardware units with no individual identity beyond their hardware ID. A shared secret is simpler and appropriate for this use case.

**Device approval — second layer for devices**

Knowing the server token is not sufficient on its own. Each new device that connects for the first time is placed in a `PendingApproval` state and rejected until the Server Administrator explicitly approves it in the Admin panel → Devices tab. This prevents unknown hardware from connecting even if the server token is somehow leaked. Approval is tied to the device's hardware ID (`SystemInfo.deviceUniqueIdentifier` on Android), which is stable across app reinstalls and only changes on factory reset.

---

## Data Stored on the Server

The server uses a SQLite database stored in a Docker volume. The following data is persisted:

| Data | What is stored |
|---|---|
| Devices | Hardware ID, device model, app name, first seen, last seen, approval status |
| Supervisor accounts | Email, bcrypt password hash (plain password never stored), role, display name |
| Sessions | Session ID, supervisor, session name, start time, end time, status |
| Session membership | Which devices were in which session, join and leave times |
| Message presets | Per-supervisor saved message templates |

**Video is never stored.** Streams are live only — the server does not record, buffer, or log any video data. The server is not in the video path at all once the WebRTC connection is established.

The database file survives container restarts and image updates because it is stored in a named Docker volume (`classroom-db`), not inside the container itself. Running `docker compose down` preserves the database. Running `docker compose down -v` permanently deletes it.
