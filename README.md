# LockPilot

A .NET 8 console app for live camera video, target lock inside a reticle, and tracking. Lucas–Kanade follows the target frame to frame; YOLO periodically relocates it. Runs on Windows (webcam) and Raspberry Pi (libcamera + GStreamer).

## Run

You need a camera and a YOLO ONNX model in `LockPilot/Models` (file name comes from settings, default `yolov8n.onnx`). Models are not in git — put the file in the project; the build copies it to the output directory.

The preview window uses OpenCV HighGUI. With no arguments, the picture is shown locally; with a UDP address, frames are sent over the network (see below).

## How it works

1. The camera opens. A **reticle** of fixed size is drawn in the center of the frame.
2. **Space** locks onto whatever is inside the reticle:
   - YOLO looks for detections whose center is inside the reticle and remembers the class of the one closest to the reticle center;
   - Shi-Tomasi feature points are collected inside the reticle for Lucas–Kanade.
3. Every frame, Lucas–Kanade moves those points with optical flow. The detection box is the bounding box of the remaining points (with a small padding).
4. Every `RelocalizeIntervalSeconds` (and immediately if LK loses its points), YOLO searches again for an object of **the same class**, closest to the last box. On success, LK points are re-initialized in the new box.
5. If both LK and YOLO fail — state **Lost**, the detection box is not drawn. The reticle stays. Space again starts a new lock.

The current state is drawn in the corner of the frame.

## Controls

| Key       | Action
|-----------|--------
| `Space`   | Capture / re-acquire the target in the reticle
| `R`       | Reset to Idle
| `Esc`/`Q` | Quit

In UDP mode, keys are read from the console, not from the OpenCV window.

## Settings

[`LockPilot/appsettings.json`](LockPilot/appsettings.json) is copied next to the exe and loaded at startup.

| Setting                      | Default Value    | Meaning
|------------------------------|------------------|--------
| `CameraIndex`                | `0`              | OpenCV camera index (`0` is usually the built-in camera). Ignored if `PiCamera` is set.
| `PiCamera`                   |                  | Optional. If present, capture uses GStreamer `libcamerasrc` (Raspberry Pi) instead of `CameraIndex`.
| `PiCamera.Width`             |                  | Pi camera frame width in pixels.
| `PiCamera.Height`            |                  | Pi camera frame height in pixels.
| `AimWidth`                   | `160`            | Reticle width in pixels.
| `AimHeight`                  | `120`            | Reticle height in pixels.
| `AimColorBgr`                | `[0, 255, 0]`    | Reticle color, a three-int **BGR** array (not RGB). Green by default.
| `DetectionColorBgr`          | `[255, 0, 255]`  | Detection box color. Magenta by default.
| `RelocalizeIntervalSeconds`  | `2.0`            | How often to run YOLO while LK still holds the target. On LK failure, relocalization runs immediately.
| `MinLkPoints`                | `8`              | Minimum number of good LK points. Fewer than this means LK lost the frame.
| `MaxLkError`                 | `20.0`           | Optical-flow matching error threshold. Points with a larger error are dropped.
| `Yolo.ModelName`             | `yolov8n.onnx`   | ONNX file name in the `Models` folder next to the exe.
| `Yolo.Confidence`            | `0.25`           | Minimum detection confidence.
| `Yolo.IoU`                   | `0.45`           | NMS threshold (overlap of boxes of the same class).

Optional `PiCamera`:

```json
"PiCamera": {
  "Width": 1280,
  "Height": 720
}
```

## UDP streaming

On the receiver:

```bash
ffplay -fflags nobuffer -framedrop -probesize 32 -sync ext -f mjpeg udp://0.0.0.0:5000
```

The default port is `5000` if the URI omits it.
