# LockPilot

A .NET 10 console app for live camera video, target lock inside a reticle, and tracking. Lucas–Kanade follows the target frame to frame; YOLO periodically relocates it. Capture uses GstSharp.Net (`mfvideosrc` on Windows, `libcamerasrc` on Linux) at the camera native resolution. A GStreamer `tee` sends the **raw** camera frames as H.264 over RTP/UDP; tracking reads a parallel BGR `appsink` and sends state plus the detection box as MessagePack over NetMQ UDP so it cannot stall the stream. On Windows install the official GStreamer MSVC x86_64 runtime.

LockPilot has no keyboard input. The companion Avalonia app **GroundCon** shows the RTP video with a tracker overlay, sends capture/reset/quit over TCP, and listens for overlay MessagePack on NetMQ UDP.

## Run

You need a camera and a YOLO ONNX model in `LockPilot/Models` (file name comes from settings, default `yolov8n.onnx`). Models are not in git — put the file in the project; the build copies it to the output directory.

On Windows the official GStreamer MSVC x86_64 runtime must be installed (needed for both LockPilot and GroundCon). Start LockPilot, then GroundCon. RTP and overlay go to `GroundStation.Host`; RTP uses `GroundStation.RtpPort`, overlay MessagePack uses `GroundStation.OverlayPort`. LockPilot listens for TCP commands on `CommandPort`. GroundCon binds `RtpPort` for H.264 RTP/UDP, binds `OverlayPort` for overlay NetMQ UDP, and connects to `LockPilot.Host`:`LockPilot.CommandPort`.

## How it works

1. The camera opens. RTP starts immediately (unprocessed frames). The **reticle** is a fixed-size region in the center of the frame — it is not drawn on the stream. GroundCon draws it on top of the received video.
2. **Capture** (from GroundCon) locks onto whatever is inside the reticle:
   - YOLO looks for detections whose center is inside the reticle and remembers the class of the one closest to the reticle center;
   - Shi-Tomasi feature points are collected inside the reticle for Lucas–Kanade.
3. Every frame, Lucas–Kanade moves those points with optical flow. The detection box is the bounding box of the remaining points (with a small padding).
4. Every `RelocalizeIntervalSeconds` (and immediately if LK loses its points), YOLO searches again for an object of **the same class**, closest to the last box. On success, LK points are re-initialized in the new box.
5. If both LK and YOLO fail — state **Lost**. Capture again starts a new lock.

The current state is sent each frame as a MessagePack NetMQ UDP message. The detection box is included only while tracking. GroundCon overlays the state, the reticle, and the tracking box on the RTP video.

## Controls

GroundCon buttons send NetMQ MessagePack frames (`CommandMessage` with `Command`: `Capture`, `Reset`, or `Quit`). On startup GroundCon also sends `SetupMessage` once with the reticle size.

| Button  | Command   | Action
|---------|-----------|--------
| Capture | `Capture` | Capture the target in the reticle
| Reset   | `Reset`   | Reset to Idle
| Quit    | `Quit`    | Quit LockPilot (and GroundCon)

## Settings

[`LockPilot/appsettings.json`](LockPilot/appsettings.json) is copied next to the LockPilot exe and loaded at startup.

| Setting                      | Default Value    | Meaning
|------------------------------|------------------|--------
| `CameraIndex`                | `0`              | Camera index for Windows `mfvideosrc` (`0` is usually the built-in camera). Unused on Linux.
| `RelocalizeIntervalSeconds`  | `2.0`            | How often to run YOLO while LK still holds the target. On LK failure, relocalization runs immediately.
| `MinLkPoints`                | `8`              | Minimum number of good LK points. Fewer than this means LK lost the frame.
| `MaxLkError`                 | `20.0`           | Optical-flow matching error threshold. Points with a larger error are dropped.
| `CommandPort`                | `5002`           | TCP port LockPilot listens on for GroundCon commands.
| `Yolo.ModelName`             | `yolov8n.onnx`   | ONNX file name in the `Models` folder next to the exe.
| `Yolo.Confidence`            | `0.25`           | Minimum detection confidence.
| `Yolo.IoU`                   | `0.45`           | NMS threshold (overlap of boxes of the same class).
| `GroundStation.Host`         | `127.0.0.1`      | Destination host for RTP and overlay MessagePack.
| `GroundStation.RtpPort`      | `5000`           | RTP destination UDP port.
| `GroundStation.OverlayPort`  | `5001`           | Overlay MessagePack destination NetMQ UDP port.

[`GroundCon/appsettings.json`](GroundCon/appsettings.json) is copied next to the GroundCon exe.

| Setting                | Default Value    | Meaning
|------------------------|------------------|--------
| `OverlayPort`          | `5001`           | Local NetMQ UDP port GroundCon listens on for overlay MessagePack.
| `RtpPort`              | `5000`           | Local UDP port GroundCon listens on for H.264 RTP.
| `AimWidth`             | `160`            | Reticle width in pixels, sent to LockPilot once via `SetupMessage`.
| `AimHeight`            | `120`            | Reticle height in pixels, sent to LockPilot once via `SetupMessage`.
| `LockPilot.Host`       | `127.0.0.1`      | LockPilot address for the TCP command connection.
| `LockPilot.CommandPort`| `5002`           | LockPilot TCP command port.

## RTP streaming

Raw camera frames go out as H.264 RTP/UDP (`rtph264pay` payload type 96). Tracking does not overlay the stream.

GroundCon receives that stream on `RtpPort` (`rtph264depay`).

## Overlay MessagePack

Each processed frame RADIO-s one MessagePack message over NetMQ UDP to `GroundStation.Host`:`GroundStation.OverlayPort`. GroundCon DISH-es on `OverlayPort` and joins the `overlay` group. The payload is `OverlayMessage`: `State` (`Idle`, `Tracking`, or `Lost`) and `Rect` (`X`, `Y`, `Width`, `Height` in camera pixels) while tracking, otherwise `Rect` is `null`. GroundCon draws the state, the reticle, and the tracking box on top of the video.

## TCP commands

The MessagePack command contract lives in the shared `LockPilot.Shared` library. Messages implement `IMessage` and are serialized as a MessagePack union: `CommandMessage` (`Command`: `Capture`, `Reset`, or `Quit`) and `SetupMessage` (`AimWidth`, `AimHeight`). GroundCon PUSH-es each message as a NetMQ frame; `SetupMessage` is sent once at startup. LockPilot PULL-s on `CommandPort` and uses the last received aim size on `Capture` to build the reticle.

NetMQ reconnects on its own. LockPilot keeps tracking whether GroundCon is connected or not.
