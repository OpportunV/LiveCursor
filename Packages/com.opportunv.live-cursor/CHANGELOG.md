# Changelog

All notable changes to this package are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the package uses
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Cursor set asset with looping states, authored transitions and per-size frames.
- Cursor player core with reverse playback, mid-transition reversal and queued state changes.
- Cursor Animator component driving the hardware cursor.
- `.cursorset` importer: frames from folders, file lists or grid sheets, baked per size with a
  linear-light premultiplied downscale, plus art-rule checks.
- Cursor Set Builder window: scans a folder, detects states and transitions, picks the hotspot
  and writes the `.cursorset`.
- Generated state constants (`CursorStateId` fields and an `All` list) kept in sync on import, with
  sharing between sets and a warning for states a sharing set lacks.
- Cursor sets show their first frame as their icon in the Project window.
- Cursor Set Builder reads sprite sheets, detected from names like `Grab_4x2.png` or set per row.
- Cursor Set Preview window that plays states and transitions in the editor.
- Double-clicking a cursor set opens it in the Cursor Set Builder, with the preview docked beside it.
- Prioritised state requests (`Request`, released by disposing the handle) on top of the base state.
- `CursorHoverManipulator` for UI Toolkit and the `CursorHover` component for uGUI and scene objects.
- `[CursorStateName]` attribute that shows a dropdown of known state names in the Inspector.
- Per-state hotspots; transitions move the click point between their states' hotspots.
- `CursorHover.Configure` for setting up the component from code.
- Demo sample with two skins, a UI Toolkit scene and a uGUI and 3D scene.
