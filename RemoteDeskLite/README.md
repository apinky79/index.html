# RemoteDeskLite (iOS)

`RemoteDeskLite` is a SwiftUI iOS app scaffold inspired by Jump Desktop's core UX:

- Save/manage remote hosts (RDP/VNC)
- Launch connection sessions with saved credentials
- Session screen with fluid-mode toggle, pointer gestures, and live telemetry
- Settings for stream/quality preferences

## Important scope note

This repository includes a **mock remote connection engine** that simulates connection lifecycle and stream metrics.
It does **not** yet implement real RDP/VNC protocol transports.

## Project layout

```text
RemoteDeskLite/
  project.yml                # XcodeGen spec
  RemoteDeskLite/            # App source
  RemoteDeskLiteTests/       # Unit tests
```

## Generate and run

1. Install XcodeGen (if needed):
   ```bash
   brew install xcodegen
   ```
2. Generate the Xcode project:
   ```bash
   cd RemoteDeskLite
   xcodegen generate
   ```
3. Open `RemoteDeskLite.xcodeproj` in Xcode and run on iPhone/iPad simulator.

## Next steps for production parity

1. Replace `MockConnectionEngine` with real protocol engines:
   - `RDPConnectionEngine`
   - `VNCConnectionEngine`
2. Add secure tunnel/gateway support and certificate pinning.
3. Implement frame decoder + renderer path (Metal).
4. Add keyboard mapping, gesture presets, and multi-monitor switching.
5. Add integration tests against disposable remote endpoints.
