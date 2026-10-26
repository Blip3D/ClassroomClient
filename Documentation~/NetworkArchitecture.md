# Network Architecture

**Who this page is for:** anyone who wants to understand what travels over the network between the headsets, the ClassroomController server and the teacher's browser — mainly the person who runs the server (firewalls, Wi-Fi, TURN) and VR developers troubleshooting a connection. If you only want to get streaming working, [GettingStarted.md](GettingStarted.md) is enough. In short: the headset keeps a connection to the server for control messages, and the video/audio goes from the headset to the teacher's browser over WebRTC — directly when possible, through a relay (TURN) when not.

It is intended for two audiences:

**Server Administrator** — deploys the server, configures firewalls and makes sure the network allows the system to work.

**VR Developer** — integrates ClassroomClient into a Unity app and wants to know what the package does on the network.

---

## Overview

```
VR Device (Quest)         Node.js Server            Teacher (Browser)
      │                        │                           │
      │──── WebSocket ─────────│──── WebSocket ────────────│
      │   registration,        │   session management,     │
      │   approval, session    │   device list,            │
      │   events, messages,    │   messages, scene         │
      │   scene loading,       │   loading, signaling      │
      │   ping/pong            │   relay                   │
      │                        │                           │
      │◄═════ WebRTC video + audio (direct, or via TURN) ═══►│
```

Both the headset and the teacher's browser keep a persistent WebSocket connection to the server. All control traffic — registration, approval, sessions, messages, scene loading — goes through the server on these connections.

Video and audio travel over WebRTC. The server only relays the handshake (offer, answer, ICE candidates). The media then flows directly between headset and browser when a direct path exists, or through a TURN relay when it does not (for example on the Raspberry Pi classroom box in hotspot mode, where the Pi's WiFi blocks direct traffic between devices, so media goes through the Pi's TURN relay; behind an external router it goes direct when possible, otherwise through the relay).

---

## What Goes Through the Server

**Device → Server**
- `register_device` — sent on every connection: device ID, device name, device model, app name, bundle ID, current scene, server token, optional avatar URL. The server checks the token and the device's approval status.
- `CONTENT_LIBRARY` — sent after registration (and when the server asks for it): app name, bundle ID, current scene and the Scene Library (scenes a teacher can load).
- `DEVICE_STATUS` — sent after registration and then every 10 seconds: battery level (0–100), Wi-Fi signal (0–4), charging state.
- `session_status` — optional, from `ClassroomClientAPI.SetStatus()`.
- `CURRENT_SCENE` — which scene is showing; sent after ClassroomClient's own loads, when the app changes the active scene, and via `ClassroomClientAPI.ReportCurrentScene()`.
- `SCENE_LOADED` — a requested scene finished loading (automatic, or `ClassroomClientAPI.ReportSceneLoaded()` for Custom Loading).
- `SCENE_LOAD_FAILED` — a requested scene could not be loaded, with a reason (automatic for every failure ClassroomClient detects, or `ClassroomClientAPI.ReportSceneLoadFailed()`).
- `ping` — every 15 seconds; the server answers `pong`. A device the server hears nothing from for 45 seconds is marked offline.
- WebRTC signaling: `OFFER` and `CANDIDATE` (pipe-delimited), forwarded to the teacher. The headset also sends `DISPOSE` when it closes a stream and a `disconnect` message when it disconnects on purpose; the current server ignores both.

**Server → Device**
- `registered` — accepted; the headset is in the lobby. The headset then reads the ICE configuration (see below).
- `rejected` — `invalid_secret` (wrong server token → the headset stops and shows red) or `pending_approval` (not approved yet → blue).
- `approved` — sent on the waiting headset's still-open connection the moment an admin approves it; the headset registers again immediately. (As a fallback the headset also retries on its own after 15, 30, then 60 seconds.)
- `session_assigned` — the headset joined a session; streaming starts.
- `session_ended` — streaming stops; back to the lobby.
- `stream_request` — the teacher's browser reconnected mid-session; the headset creates a fresh WebRTC offer.
- `message` — a teacher message: text, colour, category.
- `LOAD_SCENE` — load a scene by key. ClassroomClient loads it, or raises `ClassroomEvents.OnLoadSceneRequested` for Custom Loading scenes; the result is always reported back.
- `REQUEST_CONTENT_LIBRARY` — the server asks for the Scene Library again.
- `pong` — keep-alive answer.
- WebRTC signaling: `ANSWER` and `CANDIDATE`, forwarded from the teacher.

There is **no mute message**: the dashboard's mute button only mutes the tile's playback in the browser and never reaches the headset (removed in 2.75.0).

**Teacher → Server**
- `register_controller` — on connect, with the teacher's JWT.
- `session_ready` — sent once the session view is open; the server holds `session_assigned` for the headsets until then, so the browser is ready before streams start.
- Scene loading (`LOAD_SCENE`, `load_scene_all`, `load_scene_selected`) and session changes (e.g. adding a headset to a running session).
- WebRTC signaling: `ANSWER` and `CANDIDATE`, forwarded to the target headset.

**Server → Teacher**
- `controller_registered` — full device list on connect.
- `device_connected`, `device_disconnected`, `device_updated`, `lobby_updated` — live lobby updates.
- `device_pending` — a headset is waiting for approval.
- `session_created`, `session_ended`, `session_device_added`, `session_device_removed`.
- `content_library_updated`, `scene_load_result`.
- WebRTC signaling: `OFFER` and `CANDIDATE` from the headset.

The exact message formats are defined in the ClassroomController planning document (Section 5, Message Protocol).

---

## The Stream (WebRTC)

- One WebRTC connection per headset to the teacher's browser (no fixed limit on headsets per session — the teacher's browser and network are the practical limit).
- **Video:** rendered by ClassroomClient's own capture camera. Quality is set on the `WebRTCConnection` component: 360p / 500 kbps, **720p / 2 Mbps (default)**, 1080p / 4 Mbps; up to 30 fps.
- **Audio:** one audio track (Opus, up to 64 kbps) carrying a copy of the app's sound and, if enabled, the headset microphone. It is in the same `MediaStream` as the video, so the dashboard shows picture and sound in one tile. The headset's own sound is never changed.

Rough bandwidth per headset: the video bitrate above plus up to 64 kbps audio.

---

## How the Media Path Is Found (ICE, STUN, TURN)

WebRTC uses ICE to find a working path between headset and browser:

- **Direct** — headset and browser can reach each other (same network, or NAT traversal via STUN succeeds).
- **Relayed (TURN)** — no direct path; a TURN server forwards the media. The path is then `VR Device → TURN Server → Teacher's Browser`.

**Where the headset gets its ICE servers:** after `registered`, the headset requests `GET /api/ice-config` from the same host and port as its server URL (`ws://` → `http://`, `wss://` → `https://`). This public endpoint returns the STUN/TURN servers configured on the server (`STUN_SERVERS`, `TURN_SERVER`, `TURN_USERNAME`, `TURN_PASSWORD` in `server/.env`). The headset uses them for its next stream. If the request fails or the list is empty, it falls back to Google's public STUN server (`stun:stun.l.google.com:19302`). The teacher's browser uses the same endpoint.

**Raspberry Pi classroom box:** headsets and the teacher's device are on the Pi's Wi-Fi, often without internet. The Pi runs its own TURN relay (coturn). In hotspot mode the Pi's WiFi blocks direct traffic between devices, so media goes through the Pi's relay; behind an external router it goes direct when possible, otherwise through the relay.

**Cloud (e.g. blip3d.com):** typically STUN only; media goes directly between headset and browser when the networks allow it.

**When TURN is needed:** restrictive networks — university Wi-Fi (including eduroam), corporate networks with symmetric NAT or strict firewalls, some mobile hotspots — often block direct paths. If streams never start there, configure a TURN server in `server/.env`.

Note: `/api/ice-config` is public, so configured TURN credentials are readable by anyone who can reach the server. This is accepted for the Pi (LAN only); see the server documentation.

---

## Ports

| Port | Protocol | Used for | Required |
|---|---|---|---|
| 443 | TCP | HTTPS and WSS — nginx proxies to the Node.js server | Production with SSL |
| 8080 | TCP | HTTP and WS directly to the Node.js server (e.g. the Pi: `ws://192.168.50.1:8080`) | Without nginx |
| 3478 | UDP + TCP | TURN relay | Only where a TURN server is used (e.g. the Pi) |

WebRTC media itself uses UDP ports negotiated at runtime (and the TURN server's relay ports when relayed); these do not have to be opened by hand on the headset or the browser side.

The server limits new WebSocket connections per client IP (120 per minute). Behind nginx, set `TRUST_PROXY=1` so the real client IP is used; otherwise all clients share one limit.

---

## Authentication

**Teacher login — JWT**

A teacher logs in with email and password; the server checks the password hash and returns a JWT that the browser sends with every API request and its WebSocket registration. It is valid for 8 hours. Logging in again elsewhere ends the older login.

**VR device authentication — Server Token**

Headsets have no accounts. Every headset sends the shared `SERVER_TOKEN` in `register_device`. A wrong token is rejected immediately (`invalid_secret`) and the headset does not retry. The token is fleet-wide — every headset of a deployment uses the same value — so keep it private. VR developers enter it in the Setup Wizard.

**Device approval — second layer**

Knowing the token is not enough: a headset that connects for the first time is **pending** until an admin approves it in the dashboard (**Admin → Devices**). While pending, the headset shows a blue status icon and keeps its connection open; on approval the server sends `approved` and the headset joins within seconds. Approval is tied to the device's hardware ID (`SystemInfo.deviceUniqueIdentifier` on Android). An admin can also reject or delete a headset; a rejected headset stays in the waiting state until it is approved.

**Why both mechanisms exist:** teachers are individual people with their own sessions — JWT keeps one teacher's sessions separate from another's. Headsets are interchangeable hardware, so a shared token plus per-device approval is simpler and sufficient.

---

## Data Stored on the Server

The server uses one SQLite database file:

| Data | What is stored |
|---|---|
| Devices | Hardware ID, model, app name, first/last seen, approval status |
| Teacher accounts | Email, bcrypt password hash (never the plain password), role, display name |
| Sessions | Session ID, teacher, name, start/end time, status |
| Session membership | Which headsets were in which session, join and leave times |
| Message presets | Saved message templates per teacher |

**Video and audio are never stored.** Streams are live only; the server does not record them. When media is relayed through a TURN server, the relay only forwards it.

Where the file lives: the cloud deployment (`docker-compose.yml`) keeps it in the `./data` folder next to the compose file (back it up by copying `data/classroom.db`); the Raspberry Pi deployment (`docker-compose.pi.yml`) keeps it in the `/opt/classroom/data` folder on the Pi (back it up by copying `data/classroom.db`; older Pi installs used a Docker volume `classroom-db` instead — see the Pi setup guide for moving it). Both survive container restarts and updates.
