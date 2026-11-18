# Cable Labels: notes for agents

A small mod adding a thin label for cables, modelled on the vanilla pipe label.
[README.md](README.md) is for players and other mods' authors; how the mod works inside is
below.

## How it works

The vanilla pipe label is `Assets.Scripts.Objects.Label`, a `DevicePipeMounted`. Its text is
drawn by the game's one shared `CursorManager.TextCamera` into a render texture on one of the
label's materials, and redrawn on `OnRenamed`, so the Labeller works on it with no extra code.

- **`CableLabel`** (`Assets/CableLabels/CableLabel.cs`) is a `DeviceCableMounted`, the base
  the cable fuse and cable analyser use, so placement (a straight, unbroken cable on the same
  run axis) comes for free. `Label` can't be reused because it derives from the pipe class, so
  its drawing code is copied. Labels take turns on the text camera through `Label.HasDrawn`,
  which `CableLabel` shares (through `AccessTools`) rather than copying, so a pipe label and a
  cable label never draw in the same frame.
- **The prefab** is built in code (`Assets/Editor/BuildPrefabs.cs`) with our own mesh: two
  curved panels at 0.035 m round a 0.05 m cable, each 140° and 0.32 m long, modelled on the
  pipe label's (two 120° panels at 0.102 m, 0.5 m long, read from the game's assets). Like the
  pipe label it has two trigger capsules standing out past the cable's own (r 0.1), so the
  Labeller hits the label, not the cable under it.
- **Fitting** (`CableLabel.FitToCable`, on every cell change): a pipe label is scaled by its
  pipe's `LabelSizeOffset`; cables have no such field, so a cable label measures the cable's
  meshes and widens to clear it (super-heavy is 0.038 m against normal and heavy's 0.025 m).
  Another mod's `ICableLabelMount` can give a radius or a custom mesh instead (the README's
  "For mod authors"). The hover outline (`GetSelection`) and the capsules follow the fitted
  mesh; `Bounds` itself stays, since the grid registers cells from it.
- **At load**, `CableLabelPrefabs` gives the label the pipe label's material, text settings,
  deconstruct tool and placement preview (with our mesh, and its outline's edges regenerated
  from it), takes placement settings from a vanilla cable fuse, and makes its kit,
  `ItemCableLabel`, as a copy of the pipe label's (`ItemPipeLabel`) dressed in our mesh,
  texture and icon, printed at the Electronics Printer
  (`Assets/GameData/Recipes/cable_label.xml`). This runs in LaunchPadBooster's prefix on
  `Prefab.LoadAll`: look prefabs up in `WorldManager.SourcePrefabs`, never
  `Prefab.AllPrefabs`, which is still empty.
- **Other mods' contracts.** `CableLabelMounts` finds a copy of `ICableLabelMount` on a cable by
  full name, checks each member's signature, and compiles one forwarding delegate per member
  per type (`System.Linq.Expressions`). `CableLabelMeshes` caches custom meshes per cable
  prefab and refits that prefab's labels when a mount's change token is cancelled, queued on
  the game's `UnityMainThreadDispatcher`. Both were tested outside the game (.NET Framework and
  the Unity editor's Mono) against a separately compiled copy, and in game with another mod's
  cable implementing it: a pair of bare conductors with a custom plate mesh.

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

- **Layout.** The Unity project is the repo root; there is no `Main.csproj`, and Unity compiles
  the DLL that ships. Two assembly definitions:
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
- **Art is generated; edit the generators, not the PNGs.** `python tools/make_art.py` draws
  the kit's texture. The icons are camera renders of the mesh, as vanilla's are, by
  `Assets/Editor/RenderIcons.cs`: run it after changing the mesh, from the editor's menu or
  `Unity.exe -batchmode -quit -projectPath . -executeMethod CableLabels.Editor.RenderIcons.Run`
  (**without** `-nographics`, which can't render), and commit the PNGs. Every thing needs
  **its own icon**: the creative spawn menu keys its buttons on the icon's texture, so a
  borrowed vanilla icon makes that vanilla thing unpickable there.
- **A copied blueprint needs its outline regenerated.** A blueprint's `Wireframe` draws edges
  stored with the prefab, generated from the original mesh; swapping the mesh alone leaves the
  original's outline drawn round the new fill. `CableLabelPrefabs` regenerates them with the
  game's `WireframeGenerator`.
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
- **`Assets/CableLabels/Contracts/` is a published contract.** Other mods copy
  `ICableLabelMount.cs` verbatim and Cable Labels matches their copy by full name and member
  signatures (`CableLabelMounts`). Never edit it, not even its namespace: that silently breaks
  every copy. A change is a new interface beside it, with its own binding.
- **Check Unity's compile before committing:** run the editor with
  `-batchmode -quit -nographics -projectPath . -logFile <file>` and look for `error CS`.
- **Generated files are not committed.** Unity regenerates `*.csproj` and `*.sln` at the root,
  and `Library/`, `Temp/`, `Logs/`, `UserSettings/` and `Build/`.
- **Every asset is committed with its `.meta` file**, and they are renamed, moved and deleted
  together. The `.meta` holds the GUID that prefabs reference; a lost or regenerated one breaks
  those references silently.
- **Smart Merge.** `.gitattributes` sends Unity's YAML (prefabs, `.asset`, `.mat`, `.meta`,
  ...) through the `unityyamlmerge` driver. Git config doesn't travel with a clone, so each
  clone sets the driver once, pointing at the editor version in
  `ProjectSettings/ProjectVersion.txt`:

  ```
  git config merge.unityyamlmerge.name "Unity SmartMerge (UnityYAMLMerge)"
  git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
  git config merge.unityyamlmerge.recursive binary
  ```

  Without it, git falls back to a text merge, which can leave YAML that loads but is wrong.
- **LaunchPad needs `thumb.png` and `Preview.png`** in `Assets/About/`. They are generated by
  `python tools/make_images.py` (Pillow); edit the script, not the PNGs.
- **No CI.** A release needs the asset bundle, which needs Unity, so releases are packaged
  from a local export.
- **The decompiled source.** `Label` is `Assets\Scripts\Objects\Label.cs`,
  `DeviceCableMounted` is `Assets\Scripts\Objects\Electrical\DeviceCableMounted.cs`, under
  `C:\Users\erica\Source\Decompilation\Stationeers\Assembly-CSharp`.
