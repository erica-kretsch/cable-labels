# Cable Labels

A [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad) mod that
adds a label for cables: the vanilla pipe label, but thinner, mounted on a straight stretch of
cable. Idea by Kastuk, from the SBR Electrical announcement thread.

## How it works

The vanilla pipe label is `Assets.Scripts.Objects.Label`, a `DevicePipeMounted`. Its text is
drawn by the game's one shared `CursorManager.TextCamera` into a render texture on one of the
label's materials, and redrawn on `OnRenamed`, so the Labeller works on it with no extra code.

- **`CableLabel`** ([Assets/CableLabels/CableLabel.cs](Assets/CableLabels/CableLabel.cs))
  is a `DeviceCableMounted`, the base the cable fuse and cable analyser use, so placement (a
  straight, unbroken cable in the same orientation) comes for free. `Label` can't be reused
  because it derives from the pipe class, so its drawing code is copied. Labels take turns on
  the text camera through `Label.HasDrawn`, which `CableLabel` shares (through Harmony's
  `AccessTools`) rather than copying, so a pipe label and a cable label never draw in the same
  frame.
- **The prefab** is built in code in the Unity project
  ([Assets/Editor/BuildPrefabs.cs](Assets/Editor/BuildPrefabs.cs)), with our own mesh: two
  curved panels at 0.035 m round a 0.05 m cable, each 140° and 0.32 m long, modelled on the
  pipe label's (two 120° panels at 0.102 m, 0.5 m long, read from the game's assets). Like the
  pipe label it has two trigger capsules standing out past the cable's own, so the Labeller
  hits the label, not the cable under it. Where a pipe label is scaled by its pipe's
  `LabelSizeOffset`, a cable label measures the cable it's on and widens to clear it
  (super-heavy is 0.038 m against normal and heavy's 0.025 m). The icons are camera renders of the mesh, as the
  game's are ([Assets/Editor/RenderIcons.cs](Assets/Editor/RenderIcons.cs)), running the other
  diagonal from the pipe label's so the two are easy to tell apart; the kit's texture comes
  from [tools/make_art.py](tools/make_art.py).
- **At load**, [CableLabelPrefabs](Assets/CableLabels/CableLabelPrefabs.cs) gives the label the
  pipe label's material, text settings, deconstruct tool and placement preview (with our mesh,
  and its outline's edges regenerated from it),
  takes placement settings from a vanilla cable fuse, and makes its kit, `ItemCableLabel`, as a
  copy of the pipe label's (`ItemPipeLabel`) dressed in our mesh, texture and icon, printed at
  the Electronics Printer
  ([Assets/GameData/Recipes/cable_label.xml](Assets/GameData/Recipes/cable_label.xml)).
- **Icons must be our own.** The creative spawn menu's ImGui buttons take their ID from the
  icon's texture, so a thing sharing a vanilla icon shares that thing's button, and only one of
  the two can be picked.

## Status

Placeable in game. Still to check:

- How the text reads at this size, and the panels' proportions (`Radius`, `Length`,
  `PanelDegrees` in `BuildPrefabs`).
- How the label looks once widened to clear super-heavy cable.

## Building

The repo root is a Unity 2022.3.62f3 project, laid out as SBR Electrical's; Unity compiles the
mod. On a fresh clone, first copy the game's, BepInEx's and LaunchPad's DLLs into
`Assets/Assemblies/` (the `.meta` files there name them; see [AGENTS.md](AGENTS.md)).

```
.\tools\install.ps1 -Target Local
```

exports the mod with Unity, headless, into `Build/CableLabels/`, then installs the DLL, the
asset bundle, `About/` and `GameData/` into LaunchPad's local mods folder. Close the editor
first, or build from it and pass `-SkipBuild`. See [AGENTS.md](AGENTS.md) for the repo's
quirks.

`Assets/About/Preview.png` and `Assets/About/thumb.png` come from
`python tools/make_images.py` and the kit texture from `python tools/make_art.py` (both
Pillow). The icons come from Unity: **Cable Labels → Render icons**, or headless without
`-nographics` (see [AGENTS.md](AGENTS.md)).
