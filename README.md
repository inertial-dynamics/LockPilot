# LockPilot

A .NET 10 console app for live camera video, target lock inside a reticle, and tracking. Lucas–Kanade follows the target frame to frame; YOLO periodically relocates it. Capture uses GstSharp.Net (`mfvideosrc` on Windows, `libcamerasrc` on Linux) at the camera native resolution. A GStreamer `tee` sends the **raw** camera frames as H.264 over RTP/UDP (same pipeline as [video-link](https://github.com/inertial-dynamics/video-link)); tracking reads a parallel BGR `appsink` and sends state plus the detection box as JSON over UDP so it cannot stall the stream. On Windows install the official GStreamer MSVC x86_64 runtime.

LockPilot has no keyboard input. The companion console app **GroundCon** sends capture/reset/quit over TCP and prints overlay JSON received on UDP.

## Run

You need a camera and a YOLO ONNX model in `LockPilot/Models` (file name comes from settings, default `yolov8n.onnx`). Models are not in git — put the file in the project; the build copies it to the output directory.

On Windows the official GStreamer MSVC x86_64 runtime must be installed. Start LockPilot, then GroundCon. RTP and overlay go to `GroundStation.Host`; RTP uses `GroundStation.RtpPort`, overlay JSON uses `GroundStation.OverlayPort`. LockPilot listens for TCP commands on `CommandPort`. GroundCon connects to `LockPilot.Host`:`LockPilot.CommandPort` and binds `OverlayPort` for overlay UDP.

## How it works

1. The camera opens. RTP starts immediately (unprocessed frames). The **reticle** is a fixed-size region in the center of the frame — it is not drawn on the stream; aim using the RTP receiver.
2. **Space** (from GroundCon) locks onto whatever is inside the reticle:
   - YOLO looks for detections whose center is inside the reticle and remembers the class of the one closest to the reticle center;
   - Shi-Tomasi feature points are collected inside the reticle for Lucas–Kanade.
3. Every frame, Lucas–Kanade moves those points with optical flow. The detection box is the bounding box of the remaining points (with a small padding).
4. Every `RelocalizeIntervalSeconds` (and immediately if LK loses its points), YOLO searches again for an object of **the same class**, closest to the last box. On success, LK points are re-initialized in the new box.
5. If both LK and YOLO fail — state **Lost**. Space again starts a new lock.

The current state is sent each frame as a JSON UDP datagram. The detection box is included only while tracking, for example `{"State":"Tracking","Rect":{"X":10,"Y":20,"Width":160,"Height":120}}`. GroundCon prints each datagram to the console.

## Controls

Keys are read by GroundCon and sent to LockPilot as NetMQ messages, for example `{"Cmd":"Capture"}`.

| Key       | Command   | Action
|-----------|-----------|--------
| `Space`   | `Capture` | Capture the target in the reticle
| `R`       | `Reset`   | Reset to Idle
| `Esc`/`Q` | `Quit`    | Quit LockPilot (and GroundCon)

## Settings

[`LockPilot/appsettings.json`](LockPilot/appsettings.json) is copied next to the LockPilot exe and loaded at startup.

| Setting                      | Default Value    | Meaning
|------------------------------|------------------|--------
| `CameraIndex`                | `0`              | Camera index for Windows `mfvideosrc` (`0` is usually the built-in camera). Unused on Linux.
| `AimWidth`                   | `160`            | Reticle width in pixels.
| `AimHeight`                  | `120`            | Reticle height in pixels.
| `RelocalizeIntervalSeconds`  | `2.0`            | How often to run YOLO while LK still holds the target. On LK failure, relocalization runs immediately.
| `MinLkPoints`                | `8`              | Minimum number of good LK points. Fewer than this means LK lost the frame.
| `MaxLkError`                 | `20.0`           | Optical-flow matching error threshold. Points with a larger error are dropped.
| `CommandPort`                | `5002`           | TCP port LockPilot listens on for GroundCon commands.
| `Yolo.ModelName`             | `yolov8n.onnx`   | ONNX file name in the `Models` folder next to the exe.
| `Yolo.Confidence`            | `0.25`           | Minimum detection confidence.
| `Yolo.IoU`                   | `0.45`           | NMS threshold (overlap of boxes of the same class).
| `GroundStation.Host`         | `127.0.0.1`      | Destination host for RTP and overlay JSON.
| `GroundStation.RtpPort`      | `5000`           | RTP destination UDP port.
| `GroundStation.OverlayPort`  | `5001`           | Overlay JSON destination UDP port.

[`GroundCon/appsettings.json`](GroundCon/appsettings.json) is copied next to the GroundCon exe.

| Setting                | Default Value    | Meaning
|------------------------|------------------|--------
| `OverlayPort`          | `5001`           | Local UDP port GroundCon listens on for overlay JSON.
| `LockPilot.Host`       | `127.0.0.1`      | LockPilot address for the TCP command connection.
| `LockPilot.CommandPort`| `5002`           | LockPilot TCP command port.

## RTP streaming

Raw camera frames go out as H.264 RTP/UDP (`rtph264pay` payload type 96). Tracking does not overlay the stream.

On the receiver, use the sibling [video-link](https://github.com/inertial-dynamics/video-link) RX scripts (`windows/rtp-rx.bat` or `linux/rtp-rx.sh`), listening on the same UDP port as `GroundStation.RtpPort`.

## Overlay JSON

Each processed frame sends one UTF-8 JSON datagram to `GroundStation.Host`:`GroundStation.OverlayPort`:

```json
{"State":"Tracking","Rect":{"X":10,"Y":20,"Width":160,"Height":120}}
```

`State` is `Idle`, `Tracking`, or `Lost`. `Rect` is the box in camera pixels while tracking; otherwise it is `null`. GroundCon binds `OverlayPort` and writes each datagram to the console.

## TCP commands

The JSON command contract lives in the shared `LockPilot.Shared` library. GroundCon PUSH-es each command as a NetMQ frame whose payload is UTF-8 JSON; LockPilot PULL-s on `CommandPort`:

```json
{"Cmd":"Capture"}
{"Cmd":"Reset"}
{"Cmd":"Quit"}
```

NetMQ reconnects on its own. LockPilot keeps tracking whether GroundCon is connected or not.
