# Live Cursor

Animated hardware (OS) cursors for Unity with zero added input lag.

- Looping cursor states you name yourself: Default, Pointer, Grab, Busy, and so on.
- Authored transitions between states that can play in reverse at their own speed.
- Changing state mid-transition turns playback around from the current frame.
- Click-driven changes show their first frame on the same frame as the input.
- Per-state click points, moved smoothly during transitions.
- Frames pre-scaled with a high-quality filter to the system cursor size, so Unity never resamples them.
- Generated state constants, so code never types state names.
- No dependencies, no input handling required, no allocations during playback.

## Requirements

- Unity 6000.0 or newer.
- Windows. Other platforms are untested.
- Any render pipeline and either input handling setting.

## Installation

In **Window > Package Manager**, choose **+ > Install package from git URL** and enter:

```
https://github.com/OpportunV/live-cursor.git?path=Packages/com.opportunv.live-cursor
```

## Quick start

1. Put your frames in a folder, one subfolder of numbered PNGs per state (see below).
2. Right-click the folder and choose **Live Cursor > Build Cursor Set From Folder**.
3. Click the frame to set the hotspot, tick **State constants > Generate**, and press **Create Cursor Set**.
4. Add a **Cursor Animator** component to a scene object and assign the new set.
5. Change states from code:

   ```csharp
   _cursor.SetState(CursorStates.Busy);
   ```

## Creating a cursor set

### Folder layout

- One subfolder of numbered PNGs per state: `Idle/Default/Frame_000.png`, `Idle/Default/Frame_001.png`, ...
- One subfolder per transition, named after its two states: `DefaultToGrab`, `Default_to_Grab`,
  `default-to-grab`, `Default-Grab`, ...
- A folder of differently named single PNGs (`Default.png`, `Grab.png`) gives one single-frame state per file.
- Sprite sheets: name them with their grid, `Grab_4x2.png`, or `Grab_4x2_7.png` when only 7 cells are used.
  Cells are read left to right, top to bottom. For any other single image, type the layout (`4x2` or `4x2:7`)
  into its **Frames** field in the builder.
- Every frame must have the same square canvas. 128 px works well.

### The Cursor Set Builder

Open it with **Live Cursor > Build Cursor Set From Folder** on a folder, **Assets > Create > Live Cursor >
Cursor Set**, or **Window > Live Cursor > Cursor Set Builder**. Double-clicking a cursor set, or **Edit in
Cursor Set Builder** on its Inspector, reopens it with your choices.

- **Hotspot**: click or drag on the frame to set the click point. Pick a state under **Hotspot for** to give it
  its own, such as the centre of a text beam or crosshair. Transitions move the click point from one state's to
  the other's.
- **States** and **Transitions**: untick what you do not want, rename states, and adjust frame times.
  **Ends** marks transitions whose first and last frames repeat the states' first frames; it is detected
  automatically. **Reverse** lets a transition play backwards for the opposite direction.
- **State constants**: generates a class with one `CursorStateId` per state and an `All` list. It is named after
  the set by default and regenerated whenever the set is imported. Interchangeable sets, such as skins picked in
  a settings menu, should share one class with **Share with**; a warning lists any state one of them lacks.

### Previewing

**Preview** on the set's Inspector, **Assets > Live Cursor > Preview Cursor Set** or **Window > Live Cursor >
Cursor Set Preview** plays the set with the runtime player. Click states to run transitions, change state
mid-transition to see it turn around, and move the pointer into the **try it** box to see the real hardware
cursor.

## Driving the cursor

Everything goes through a **Cursor Animator**, or the `CursorPlayer` it exposes as `Player`.

**Base state**: the state shown when nothing else asks for one.

```csharp
_cursor.SetState(CursorStates.Busy);
_cursor.SetState(CursorStates.Grab, immediate: true); // click-driven: the first frame shows this frame
```

**Requests** sit on top of the base state, so hover effects, drags and game code never fight. The highest
priority wins, ties go to the newest request, and releasing a request falls back to the next one or to the base
state. Handles are structs, so requests do not allocate.

```csharp
var drag = _cursor.Request(CursorStates.Grabbing, priority: 100, immediate: true);
// ...
drag.Dispose(); // or drag.Release(immediate: true)
```

**UI Toolkit**: add a manipulator. The optional pressed state is requested one priority higher while the left
button is held.

```csharp
button.AddManipulator(new CursorHoverManipulator(_cursor, CursorStates.Pointer));
card.AddManipulator(new CursorHoverManipulator(_cursor, CursorStates.Grab, CursorStates.Grabbing));
```

**uGUI and scene objects**: add **Live Cursor > Cursor Hover**, or call `Configure` on it from code. It is
compiled only when the uGUI package is installed. It works on UI elements and, with a `PhysicsRaycaster` or
`Physics2DRaycaster` on the camera and an EventSystem in the scene, on 3D and 2D colliders.

**Skins**: call `SetCursorSet` to switch sets. The current state carries over when the new set has it.

**Idle**: `IdleEnabled` turns state loops on and off. `SuppressIdle(token)` and `ReleaseIdle(token)` hold them
temporarily, for example during a cutscene.

**State names in the Inspector**: mark a `string` field with `[CursorStateName]` to get a dropdown of the states
in the project's cursor sets; `[CursorStateName(true)]` adds "None".

## Performance and cursor sizes

- The first time each frame is shown, Windows takes about 4 ms to create its cursor. Call `Warm()`, or tick
  **Warm On Set Change** on the Cursor Animator, during a loading screen to pay that once: about half a second
  for a set of 120 frames.
- After that, a cursor change costs about 0.3 ms and playback allocates nothing.
- Unity keeps a limited number of cursors ready. Warm only the set in use; warming every skin at every size can
  push frames out and make each change cost about 1.5 ms again.
- Unity creates every hardware cursor at the system cursor size: 32 px, or 48 and 64 px when Windows was signed
  in at 150% and 200% display scale. Windows then enlarges the cursor on screen for the current display scale
  and pointer size. Baking that exact size lets Live Cursor's filter do the downscaling instead of Unity's, so
  the default sizes `[32, 48, 64]` cover common setups.

## Samples

Import **Demo** from the package's **Samples** tab in the Package Manager. It contains:

- Two interchangeable skins, Twinkle and Midnight, with eight states (Default, Pointer, Text, Busy, Blocked,
  Grab, Grabbing, Crosshair), transitions, per-state hotspots and shared `DemoCursorStates` constants.
- **UI Toolkit Demo**: hover cards, a held grab, a busy task set from code, a high-priority request, idle on and
  off, and skin switching.
- **uGUI and Scene Demo**: the same with **Cursor Hover** on Canvas controls, plus 3D objects with hover states
  and draggable crates. Its scripts compile only when the uGUI package is installed.

## The .cursorset format

The builder writes a `.cursorset` file (JSON) next to the frames, and Unity imports it into a `CursorSet` asset.
Frames are baked for every listed size at import time; the PNGs never need special import settings. You can
also write the file by hand.

```json
{
  "sizes": [32, 48, 64],
  "hotspot": [12, 10],
  "states": [
    { "name": "Default", "frames": { "folder": "Idle/Default" }, "frameDurationMs": 100 },
    { "name": "Busy", "frames": { "sheet": "Busy.png", "columns": 4, "rows": 3, "count": 12 }, "frameDurationMs": 83 },
    { "name": "Text", "frames": { "files": ["Text.png"] }, "hotspot": [64, 64] }
  ],
  "transitions": [
    {
      "from": "Default",
      "to": "Busy",
      "frames": { "folder": "Transitions/DefaultToBusy" },
      "frameDurationMs": 30,
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
| `hotspot` | Default click point in source pixels, measured from the top-left corner. It is scaled for every size. |
| `states[].name` | The state name. |
| `states[].frames` | A `folder` of PNGs (natural sort order), a list of `files`, or a grid `sheet` with `columns`, `rows` and an optional `count`. Paths are relative to the `.cursorset` file. |
| `states[].hotspot` | Optional click point for this state, overriding `hotspot`. |
| `frameDurationMs` | Time each frame is shown. Defaults: 100 for states, 33 for transitions. |
| `loopDelayMs` | Time the first frame is held after entering the state before the loop starts. |
| `transitions[].from`, `to` | The two states. |
| `includesEndpoints` | The first and last frames repeat frame 0 of the two states, so playback skips them. Default `true`. |
| `reversible` | The transition may play backwards for the opposite direction. Default `true`. |
| `reverseFrameDurationMs` | Frame time when playing backwards. `0` uses `frameDurationMs`. |
| `transitions[].hotspot` | Optional fixed click point. Without it, the click point moves frame by frame from the source state's to the destination's. |
| `code` | Optional `className`, `namespace` and `path` (relative to the file) of the generated state constants. |

Checked on import:

- Every frame has the same square canvas.
- Every hotspot lies inside the canvas.
- With `includesEndpoints`, a transition's first and last frames match frame 0 of its states; otherwise the
  cursor jumps when the transition starts or ends.

Editing a frame PNG reimports the set. Adding or removing files in a frame folder needs a manual **Reimport** of
the `.cursorset`.

## License

MIT. See [LICENSE.md](LICENSE.md).
