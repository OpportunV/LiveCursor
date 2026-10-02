# Live Cursor

Animated hardware (OS) cursors for Unity with zero added input lag.

- Looping cursor states (Default, Grab, Busy, ... you name them).
- Authored transitions between states that can play in reverse at their own speed.
- Changing state mid-transition turns playback around from the current frame.
- Frames baked for several sizes; the closest to the system cursor size is used.
- No dependencies, no input handling required, no allocations during playback.

Status: early development.

## Importing a cursor set

Put the frame PNGs next to a `.cursorset` file (JSON) and Unity imports it into a
`CursorSet` asset. Frames are baked for every listed size at import time; the PNGs
themselves never need special import settings. Create one with
**Assets > Create > Live Cursor > Cursor Set**.

```json
{
  "sizes": [32, 48, 64],
  "hotspot": [48, 48],
  "states": [
    { "name": "Default", "frames": { "folder": "Idle/Default" }, "frameDurationMs": 100 },
    { "name": "Busy", "frames": { "sheet": "Busy.png", "columns": 4, "rows": 3, "count": 12 }, "frameDurationMs": 83 },
    { "name": "Grab", "frames": { "files": ["Grab.png"] }, "loopDelayMs": 0 }
  ],
  "transitions": [
    {
      "from": "Default",
      "to": "Grab",
      "frames": { "folder": "Transitions/DefaultToGrab" },
      "frameDurationMs": 20,
      "includesEndpoints": true,
      "reversible": true,
      "reverseFrameDurationMs": 0
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `sizes` | Square sizes to bake, in pixels. At runtime the smallest size that covers the system cursor size is used. Default `[32, 48, 64]`. |
| `hotspot` | Click point in source pixels, measured from the top-left corner. It is scaled for every size. |
| `states[].frames` | A `folder` of PNGs (natural sort order), a list of `files`, or a grid `sheet` with `columns`, `rows` and an optional `count`. Paths are relative to the `.cursorset` file. |
| `frameDurationMs` | Time each frame is shown. |
| `loopDelayMs` | Time the first frame is held after entering the state before the loop starts. |
| `includesEndpoints` | The first and last transition frames repeat frame 0 of the two states, so playback skips them. Default `true`. |
| `reversible` | The transition may play backwards for the opposite direction. Default `true`. |
| `reverseFrameDurationMs` | Frame time when playing backwards. `0` uses `frameDurationMs`. |

Art rules checked on import:

- Every frame has the same square canvas.
- The hotspot lies inside the canvas.
- With `includesEndpoints`, a transition's first and last frames match frame 0 of its states
  (otherwise the cursor jumps when the transition starts or ends).

Editing a frame PNG reimports the set. Adding or removing files in a frame folder needs a
manual **Reimport** of the `.cursorset`.
