# Cable Labels: notes for agents

A small mod adding a thin label for cables, modelled on the vanilla pipe label.
[README.md](README.md) says how it works, what is still unverified, and how to build it.

## Commit trailers

Every commit an agent makes ends with one trailer:

```
Assisted-by: AGENT_NAME:MODEL_VERSION [TOOL1] [TOOL2]
```

- `AGENT_NAME` is the agent, `MODEL_VERSION` the exact model that made the change, e.g.
  `Assisted-by: Claude:claude-opus-5-5`.
- The bracketed tools are optional: the square brackets in the format line mark them as
  optional and are not written. List only specialised analysis tools used for the change (for
  example a decompiler such as dotPeek), never everyday tools like git, dotnet or an editor.
- Never add `Co-Authored-By` or `Signed-off-by` lines. Those are for people.

## Working in this repo

- **Git line endings.** The machine's global `core.safecrlf=true` rejects LF files on `add`,
  `commit` and `stash`. Pass `-c core.safecrlf=false` on those commands; don't change the
  config.
- **Versioning.** `About/About.xml` is the single source of the mod ID and version: the build
  generates `ThisModInfo` from it (`GenerateModInfo` in `Main.props`).
- **Local paths.** The game's DLL folder and the install folder go in the gitignored
  `Local.Developer.props`. Local installs go to LaunchPad's mods folder
  (`%USERPROFILE%\Documents\My Games\Stationeers\mods\CableLabels\`), never `BepInEx/plugins`.
- **One source tree, two compiles.** The sources live in the Unity project, in
  `src/Assets/CableLabels/`, under an assembly definition named `CableLabels`, the same name as
  the DLL that ships. Prefabs find their components by assembly, namespace and class name, so
  never rename any of the three for a class a prefab uses. `Main.csproj` defines `CL_GAME`;
  Unity's editor compile doesn't, and can't see private game members. Serialized fields stay
  outside `#if` so both compiles agree on the class's layout.
- **The Unity project** (`src/`, editor 2022.3.62f3) was copied from Electrical DC's
  (`../electrical_dc/src`), which came from the `stationeersunitymoddingtemplate` Unity
  template. Its reference DLLs in `src/Assets/Assemblies/` are never committed, only their
  `.meta` files; `dotnet build Main.csproj` copies them in, so build once before opening Unity
  after a clone. The same build copies `About/About.xml` to `src/Assets/About/` (gitignored)
  for the exporter; edit the root one only. Close Unity before moving files under `src/`.
- **The prefab.** `src/Assets/Editor/BuildPrefabs.cs` builds it; edit the script, not the
  prefab. It's a placeholder that `CableLabelPrefabs` finishes at load from the vanilla pipe
  label and a cable fuse.
- **Asset bundles.** `dotnet build Main.csproj -t:Install` runs Unity headless first
  (`BuildPrefabs.BuildAndExport`, log in `src/Logs/install-export.log`) and exports to
  `src/Build/CableLabels/` (gitignored). Install copies only the `*.assets` bundles, never the
  exporter's own `CableLabels.dll`, which would clash with the shipped one. While the editor
  has `src/` open, batch mode can't run; Install warns and uses the last export.
  `-p:SkipUnity=true` skips it.
- **CI** builds the DLL only. A release needs the asset bundle, which needs Unity, so releases
  are packaged locally from an Install.
- **The decompiled source.** `Label` is `Assets\Scripts\Objects\Label.cs`,
  `DeviceCableMounted` is `Assets\Scripts\Objects\Electrical\DeviceCableMounted.cs`.
