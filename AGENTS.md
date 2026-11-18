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

- **Layout.** The Unity project is the repo root, as in `../sbr-electrical`; there is no
  `Main.csproj`, and Unity compiles the DLL that ships. Two assembly definitions:
  - `Assets/CableLabels/` → `CableLabels`, the shipped DLL. Prefabs find their components by
    assembly, namespace and class name, so never rename any of the three for a class a prefab
    uses (`CableLabel`).
  - `Assets/Editor/` → `CableLabels.Editor`: editor-only, not shipped.
- **Git line endings.** The machine's global `core.safecrlf=true` rejects LF files on `add`,
  `commit` and `stash`. Pass `-c core.safecrlf=false` on those commands; don't change the
  config.
- **Versioning.** `Assets/About/About.xml` holds the mod's version. Keep
  `Assets/CableLabels/ThisModInfo.cs` in step with it.
- **Edit `About.xml`, not Player Settings.** The `stationeers.modding.exporter` package syncs
  About.xml's `Name`, `Author` and `Version` to Player Settings, and back again while the
  editor is open. Its write-back drops the `<?xml?>` declaration and adds a UTF-8 BOM; that is
  expected.
- **Private game members** go through Harmony's `AccessTools`, since Unity compiles against the
  plain `Assembly-CSharp`, not a publicized one (`CableLabel` shares `Label.HasDrawn` this
  way). **Never call `AccessTools` from a static initializer** of a class a prefab uses:
  AccessTools' own type initializer throws in the editor, so the class's does too, and
  `AddComponent` then builds the component without running field initializers. The prefab
  silently saves every default as 0 (here `GridSize`, which overflowed
  `Structure.GetLocalGridBounds` in game). Look it up lazily, and check the export log for
  `TypeInitializationException`.
- **Reference DLLs.** `Assets/Assemblies/` holds local copies of the game's, BepInEx's and
  LaunchPad's DLLs for Unity to compile against. Only their `.meta` files are committed. On a
  fresh clone, copy each DLL named by a `.meta` there from the game's
  `rocketstation_Data/Managed/`, `BepInEx/core/` or `BepInEx/plugins/StationeersLaunchPad/`.
  To use another DLL, copy it in, commit the `.meta` Unity makes, and add it to the asmdef's
  `precompiledReferences`.
- **The prefab is built from code.** `Assets/Editor/BuildPrefabs.cs` writes
  `Assets/Prefabs/` (the prefab and its mesh), and `Export.Run` runs it before every export, so
  edit the builder, never the prefab, and commit what it writes. `CableLabelPrefabs` finishes
  it at load with the pipe label's material and a cable fuse's placement settings.
- **Art is generated.** `python tools/make_art.py` writes `Assets/Art/` (the icons and the
  kit's texture); edit the script, not the PNGs. Every thing needs **its own icon**: the
  creative spawn menu keys its buttons on the icon's texture, so a borrowed vanilla icon makes
  that vanilla thing unpickable there.
- **Reading vanilla assets.** UnityPy reads prefabs from the game's
  `rocketstation_Data/resources.assets` (the pipe label is `StructurePipeLabel`, its mesh
  `PipeLabel`); that is where the label's dimensions came from.
- **Building and installing.** `tools\install.ps1` exports the mod with Unity, headless
  (`CableLabels.Editor.Export.Run`, log in `Logs/install-export.log`), into
  `Build/CableLabels/`, then replaces `CableLabels\` in each mods folder: `-Target Server` is
  `E:\Stationeers\mods`, the dedicated server owned by coding agents; `-Target Local` is
  Erica's own `Documents\My Games\Stationeers\mods`; the default is both. Agents testing a
  change install to `Server` only, unless Erica asks otherwise. Batch mode can't open the
  project while the editor has it open; close the editor, or build from it to
  `Build/CableLabels.exe` and pass `-SkipBuild`. Installing doesn't enable the mod:
  LaunchPad's `modconfig.xml` in each game folder does that.
- **Check Unity's compile before committing:** run the editor with
  `-batchmode -quit -nographics -projectPath . -logFile <file>` and look for `error CS`.
- **Generated files are not committed.** Unity regenerates `*.csproj` and `*.sln` at the root,
  and `Library/`, `Temp/`, `Logs/`, `UserSettings/` and `Build/`.
- **Every asset is committed with its `.meta` file**, and they are renamed, moved and deleted
  together. The `.meta` holds the GUID that prefabs reference; a lost or regenerated one breaks
  those references silently.
- **Smart Merge.** `.gitattributes` sends Unity's YAML through the `unityyamlmerge` driver,
  which each clone configures once; see `../sbr-electrical/AGENTS.md`, "Smart Merge", for the
  three `git config` lines.
- **LaunchPad needs `thumb.png` and `Preview.png`** in `Assets/About/`. They are generated by
  `python tools/make_images.py` (Pillow); edit the script, not the PNGs.
- **No CI.** A release needs the asset bundle, which needs Unity, so releases are packaged
  from a local export.
- **The decompiled source.** `Label` is `Assets\Scripts\Objects\Label.cs`,
  `DeviceCableMounted` is `Assets\Scripts\Objects\Electrical\DeviceCableMounted.cs`, under
  `C:\Users\erica\Source\Decompilation\Stationeers\Assembly-CSharp`.
