# Cable Labels

A [StationeersLaunchPad](https://github.com/StationeersLaunchPad/StationeersLaunchPad) mod that
adds a label for cables: the vanilla pipe label, but thinner, mounted on a straight stretch of
cable. Idea by Kastuk, from the SBR Electrical announcement thread.

## How it works

The vanilla pipe label is `Assets.Scripts.Objects.Label`, a `DevicePipeMounted`. Its text is
drawn by the game's one shared `CursorManager.TextCamera` into a render texture on one of the
label's materials, and redrawn on `OnRenamed`, so the Labeller works on it with no extra code.

- **`CableLabel`** ([src/Assets/CableLabels/CableLabel.cs](src/Assets/CableLabels/CableLabel.cs))
  is a `DeviceCableMounted`, the base the cable fuse and cable analyser use, so placement (a
  straight, unbroken cable in the same orientation) comes for free. `Label` can't be reused
  because it derives from the pipe class, so its drawing code is copied. Labels take turns on
  the text camera through `Label.HasDrawn`, which `CableLabel` shares (publicized) rather than
  copying, so a pipe label and a cable label never draw in the same frame.
- **The prefab** is built in code in the Unity project
  ([src/Assets/Editor/BuildPrefabs.cs](src/Assets/Editor/BuildPrefabs.cs)) as a placeholder:
  root → `Sleeve` (scaled thin across the cable) → `Visual`. The Unity project has none of the
  game's assets.
- **At load**, [CableLabelPrefabs](src/Assets/CableLabels/Game/CableLabelPrefabs.cs) gives the
  placeholder the pipe label's mesh, materials, text settings, thumbnail, blueprint and
  deconstruct tool, fits the collider to the thinned mesh, takes placement settings from a
  vanilla cable fuse, and adds the label to the `ItemPipeLabel` kit as a second option.

## Status

Scaffolded, not yet tried in game. Things to check first:

- That `ItemPipeLabel` is a multi-constructor kit (the load logs an error if not).
- The thinness (`Thinness`, 0.5, in both `CableLabelPrefabs` and `BuildPrefabs`) against a
  normal cable, and whether heavy cable wants a different size (pipes do this with
  `Pipe.LabelSizeOffset`; cables have no such field).
- That the pipe label's mesh is centred on its axis, which the sleeve's scaling assumes.
- The placement preview is still the pipe label's blueprint, so it shows pipe-thick.

## Building

Needs the .NET SDK, a Stationeers install, and Unity 2022.3.62f3 (for the asset bundle).

```
dotnet build Main.csproj
dotnet build Main.csproj -t:Install
```

The first builds the DLL and copies the game's DLLs into the Unity project; do it once before
opening `src/` in Unity. The second also rebuilds the prefab and exports the asset bundle with
Unity headless, then copies the DLL, the bundle, `About/` and `GameData/` into LaunchPad's local
mods folder. Pass `-p:SkipUnity=true` to install the last exported bundle instead. See
[AGENTS.md](AGENTS.md) for the repo's quirks.

`About/Preview.png` and `About/thumb.png` come from `python tools/make_images.py` (Pillow).
