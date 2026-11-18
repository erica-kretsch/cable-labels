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
  ([Assets/Editor/BuildPrefabs.cs](Assets/Editor/BuildPrefabs.cs)) as a placeholder:
  root → `Sleeve` (scaled thin across the cable) → `Visual`. The Unity project has none of the
  game's assets.
- **At load**, [CableLabelPrefabs](Assets/CableLabels/CableLabelPrefabs.cs) gives the
  placeholder the pipe label's mesh, materials, text settings, thumbnail, blueprint and
  deconstruct tool, fits the collider to the thinned mesh, takes placement settings from a
  vanilla cable fuse, and makes its kit, `ItemCableLabel`, as a copy of the pipe label's
  (`ItemPipeLabel`), printed at the Electronics Printer
  ([Assets/GameData/Recipes/cable_label.xml](Assets/GameData/Recipes/cable_label.xml)).

## Status

Scaffolded, not yet tried in game. Things to check first:

- The kit is a straight copy of the pipe label kit, so it still reads "PIPE LABEL" and uses
  that kit's icon. It wants its own texture (and probably a thinner model).
- The thinness (`Thinness`, 0.5, in both `CableLabelPrefabs` and `BuildPrefabs`) against a
  normal cable, and whether heavy cable wants a different size (pipes do this with
  `Pipe.LabelSizeOffset`; cables have no such field).
- That the pipe label's mesh is centred on its axis, which the sleeve's scaling assumes.
- The placement preview is still the pipe label's blueprint, so it shows pipe-thick.

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
`python tools/make_images.py` (Pillow).
