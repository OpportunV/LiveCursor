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
- Generated state constants (`CursorStateId` fields) kept in sync on import.
