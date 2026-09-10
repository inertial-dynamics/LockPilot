# LockPilot

A .NET 10 console app for live camera video, target lock inside a reticle, and tracking. Lucas–Kanade follows the target frame to frame; YOLO periodically relocates it. Capture uses GstSharp.Net (`mfvideosrc` on Windows, `libcamerasrc` on Linux) at the camera native resolution. A GStreamer `tee` sends the **raw** camera frames as H.264 over RTP/UDP (same pipeline as [video-link](https://github.com/inertial-dynamics/video-link)); tracking reads a parallel BGR `appsink` and prints state to the console so it cannot stall the stream. On Windows install the official GStreamer MSVC x86_64 runtime.

## Run

You need a camera and a YOLO ONNX model in `LockPilot/Models` (file name comes from settings, default `yolov8n.onnx`). Models are not in git — put the file in the project; the build copies it to the output directory.

On Windows the official GStreamer MSVC x86_64 runtime must be installed. RTP destination comes from settings (`Rtp.Host` / `Rtp.Port`).

## How it works

1. The camera opens. RTP starts immediately (unprocessed frames). The **reticle** is a fixed-size region in the center of the frame — it is not drawn on the stream; aim using the RTP receiver.
2. **Space** locks onto whatever is inside the reticle:
   - YOLO looks for detections whose center is inside the reticle and remembers the class of the one closest to the reticle center;
   - Shi-Tomasi feature points are collected inside the reticle for Lucas–Kanade.
3. Every frame, Lucas–Kanade moves those points with optical flow. The detection box is the bounding box of the remaining points (with a small padding).
4. Every `RelocalizeIntervalSeconds` (and immediately if LK loses its points), YOLO searches again for an object of **the same class**, closest to the last box. On success, LK points are re-initialized in the new box.
5. If both LK and YOLO fail — state **Lost**. Space again starts a new lock.

The current state (and the detection box while tracking) is printed to the console.

## Controls

| Key       | Action
|-----------|--------
| `Space`   | Capture the target in the reticle
| `R`       | Reset to Idle
| `Esc`/`Q` | Quit

Keys are read from the console.

## Settings

[`LockPilot/appsettings.json`](LockPilot/appsettings.json) is copied next to the exe and loaded at startup.

| Setting                      | Default Value    | Meaning
|------------------------------|------------------|--------
| `CameraIndex`                | `0`              | Camera index for Windows `mfvideosrc` (`0` is usually the built-in camera). Unused on Linux.
| `AimWidth`                   | `160`            | Reticle width in pixels.
| `AimHeight`                  | `120`            | Reticle height in pixels.
| `RelocalizeIntervalSeconds`  | `2.0`            | How often to run YOLO while LK still holds the target. On LK failure, relocalization runs immediately.
| `MinLkPoints`                | `8`              | Minimum number of good LK points. Fewer than this means LK lost the frame.
| `MaxLkError`                 | `20.0`           | Optical-flow matching error threshold. Points with a larger error are dropped.
| `Yolo.ModelName`             | `yolov8n.onnx`   | ONNX file name in the `Models` folder next to the exe.
| `Yolo.Confidence`            | `0.25`           | Minimum detection confidence.
| `Yolo.IoU`                   | `0.45`           | NMS threshold (overlap of boxes of the same class).
| `Rtp.Host`                   | `127.0.0.1`      | RTP destination host.
| `Rtp.Port`                   | `5000`           | RTP destination UDP port.

## RTP streaming

Raw camera frames go out as H.264 RTP/UDP (`rtph264pay` payload type 96). Tracking does not overlay the stream.

On the receiver, use the sibling [video-link](https://github.com/inertial-dynamics/video-link) RX scripts (`windows/rtp-rx.bat` or `linux/rtp-rx.sh`), listening on the same UDP port as `Rtp.Port`.
