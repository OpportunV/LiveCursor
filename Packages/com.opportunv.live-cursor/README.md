# Live Cursor

Animated hardware (OS) cursors for Unity with zero added input lag.

- Looping cursor states (Default, Grab, Busy, ... you name them).
- Authored transitions between states that can play in reverse at their own speed.
- Changing state mid-transition turns playback around from the current frame.
- Frames baked for several sizes; the closest to the system cursor size is used.
- No dependencies, no input handling required, no allocations during playback.

Status: early development.

## Creating a cursor set

1. Put your frames in a folder: one subfolder of numbered PNGs per state
   (`Idle/Default/Frame_000.png`, ...) and one per transition, named after the two states
   (`DefaultToGrab`, `Default_to_Grab`, `default-to-grab`, `Default-Grab`, ...).
   A folder of differently named single PNGs (`Default.png`, `Grab.png`) becomes one
   single-frame state per file. Sprite sheets work too: name them with their grid,
   `Grab_4x2.png`, or `Grab_4x2_7.png` when only 7 cells are used (cells are read left to
   right, top to bottom). For any other single image, type the layout (`4x2`, `4x2:7`) into
   its **Frames** field in the builder.
2. Right-click the folder and choose **Live Cursor > Build Cursor Set From Folder**
   (or **Assets > Create > Live Cursor > Cursor Set**).
3. In the builder, click the frame to set the hotspot, untick anything you do not want,
   adjust names and timings, and press **Create Cursor Set**.
4. Optionally tick **State constants > Generate** to get a class with one `CursorStateId`
   per state, so code never types state names by hand:

   ```csharp
   _cursor.SetState(CursorStates.Grab);
   ```

   The class is named after the set by default and regenerated whenever the set is imported. It also
   has an `All` list of every state. Interchangeable sets, such as skins picked in a settings menu,
   should share one class (**Share with**), so game code never depends on which set is active; a
   warning lists any state one of them lacks.

Reopen a set later by double-clicking it or with **Edit in Cursor Set Builder** on its Inspector.

**Preview** (Inspector button, **Assets > Live Cursor > Preview Cursor Set** or
**Window > Live Cursor > Cursor Set Preview**) plays the set with the runtime player: click
states to run transitions, change state mid-transition to see it turn around, and move the
pointer into the **try it** box to see the real hardware cursor.

## Driving the cursor

Add a **Cursor Animator** component to a scene object (or register one with your DI
container) and assign the set. Everything below goes through it, or through its `Player`.

**From code**, set the base state, the one shown when nothing else asks for anything:

```csharp
_cursor.SetState(CursorStates.Busy);
_cursor.SetState(CursorStates.Grab, immediate: true); // click-driven: first frame shows this frame
```

**Requests** sit on top of the base state, so hover effects, drags and game code never fight.
The highest priority wins, ties go to the newest request, and releasing a request falls back
to the next one (or to the base state). Handles are structs, so requests do not allocate.

```csharp
var drag = _cursor.Request(CursorStates.Dragging, priority: 100, immediate: true);
// ...
drag.Dispose(); // or drag.Release(immediate: true)
```

**UI Toolkit**: add a manipulator. The optional pressed state is requested one priority
higher while the left button is held.

```csharp
button.AddManipulator(new CursorHoverManipulator(_cursor, CursorStates.Grab, CursorStates.Dragging));
```

**uGUI and scene objects**: add **Live Cursor > Cursor Hover** (compiled only when the uGUI
package is installed). It works on UI elements and, with a `PhysicsRaycaster` or
`Physics2DRaycaster` on the camera and an EventSystem in the scene, on 3D and 2D colliders.
It uses the assigned animator, or finds one in the scene.

**State names in the Inspector**: mark a `string` field with `[CursorStateName]` to get a
dropdown of the states in the project's cursor sets; `[CursorStateName(true)]` adds "None".

## The .cursorset format

The builder writes a `.cursorset` file (JSON) next to the frames, and Unity imports it into a
`CursorSet` asset. Frames are baked for every listed size at import time; the PNGs
themselves never need special import settings. You can also write the file by hand.

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
| `code` | Optional `className`, `namespace` and `path` (relative to the file) of the generated state constants. |

Art rules checked on import:

- Every frame has the same square canvas.
- The hotspot lies inside the canvas.
- With `includesEndpoints`, a transition's first and last frames match frame 0 of its states
  (otherwise the cursor jumps when the transition starts or ends).

Editing a frame PNG reimports the set. Adding or removing files in a frame folder needs a
manual **Reimport** of the `.cursorset`.
