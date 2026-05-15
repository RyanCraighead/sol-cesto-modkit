# Sol Cesto ModKit Installer

A BepInEx-style mod framework for the Windows NW.js/Construct build of Sol Cesto `v100.2`.

BepInEx itself targets Unity/.NET games, so this project uses the same idea rather than the same runtime: install one bootstrap loader into `package.nw\scripts\main.js`, then load separate JavaScript mods from a normal `mods` folder beside `SolCesto.exe`.

The framework layer includes a Harmony-like JavaScript patch API, Construct runtime object inspection, variable watchers, per-mod config, log/dump folders, and a mappings database for known Sol Cesto runtime fields.

The installer creates a separate modded copy of the game. It does not modify the original Sol Cesto folder.

## Download

Download `SolCestoModKitInstaller.exe` from the [Releases page](https://github.com/RyanCraighead/sol-cesto-modkit/releases).

## What It Installs

```text
Sol Cesto modded\
  SolCesto.exe
  package.nw\
    scripts\main.js        bootstrap loader injected here
  mods\
    mods.json              enabled mod list
    _system\
      inspector.js         runtime inspector and dump tools
    config\                per-mod generated config
    commands\              file-based agent/user command queue
    docs\                  generated ModKit API docs
    dumps\                 inspector output
    logs\                  ModKit logs
    mappings\
      sol-cesto-v100.2.json
    responses\             command queue responses
    money-lock.js          example/proven gold mod
    README.md              local modding notes
    examples\
      example-overlay.js
      example-hook.js
```

The bootstrap exposes `window.SolCestoModding` and loads enabled CommonJS mods from `mods\mods.json`.

## Using The Installer

1. Run `SolCestoModKitInstaller.exe`.
2. For `Game folder`, select the folder containing `SolCesto.exe` and `package.nw`.
3. Leave `Output folder` as the auto-filled `Sol Cesto modded` folder, or choose another separate folder.
4. Leave `Include inspector tools` checked if you want runtime dump and mapping tools installed.
5. Leave `Include money mod` checked if you want the known working gold mod installed.
6. Click `Create modded build`.
7. Run `SolCesto.exe` from the modded output folder.

Inspector hotkeys:

- `F2`: dump Construct runtime objects, instance variables, active patches, registered mods, and mappings to `mods\dumps`.
- `F3`: dump known mapping values to `mods\dumps`.
- `F4`: show a small runtime/mapping summary.

Aliases `F6`, `F7`, and `F10` are also registered, but `F2/F3/F4` are more reliable in NW.js.

Money mod hotkeys:

- `F8`: set money once.
- `F9`: toggle money lock.

By default the money mod is loaded but passive until you press `F8` or turn the lock on with `F9`.

The money mod uses the mapping database and updates real Construct runtime state, not only the visible text:

- `heros.instVars.or`
- `metaProgression.instVars.or`
- `metaProgression.instVars.or_ancien`
- `metaProgression.instVars.orEver`
- `hero_or` display text

## Creating A Mod

Create a `.js` file in the output `mods` folder:

```js
module.exports = function (api) {
  api.registerMod({ id: "my-mod", name: "My Mod", version: "1.0.0" });

  api.onRuntimeReady(function () {
    api.showMessage("My mod loaded");
  });

  api.addHotkey("F10", function (event) {
    event.preventDefault();
    api.showMessage("F10 pressed");
  });
};
```

Add it to `mods\mods.json`:

```json
{
  "mods": [
    { "file": "money-lock.js", "enabled": true },
    { "file": "my-mod.js", "enabled": true }
  ]
}
```

## Mod API

Available through the `api` object passed to each mod:

- `api.getRuntime()`
- `api.listObjects()`
- `api.getObjectInstances(objectName)`
- `api.describeObject(objectName)`
- `api.dumpRuntimeMap()`
- `api.setInstanceVar(objectName, varName, value)`
- `api.getGlobalVar(name)`
- `api.setGlobalVar(name, value)`
- `api.setTextObject(objectName, value)`
- `api.onRuntimeReady(callback)`
- `api.onTick(callback)`
- `api.addHotkey("F10", callback)`
- `api.showMessage(text)`
- `api.log(text)`
- `api.warn(text)`
- `api.registerMod(metadata)`
- `api.getRegisteredMods()`

Additional framework namespaces:

- `api.mappings.get/list/getValue/setValue`
- `api.patch.before/after/replace/unpatch/list`
- `api.watch.value/instanceVar/globalVar/mappedValue`
- `api.config.read/write`
- `api.files.readJson/writeJson/writeDump`
- `api.events.on/off/emit`
- `api.commands.register/list/execute/process`

## BepInEx/Harmony Equivalents

Sol Cesto is not Unity, so there are no `.NET` assemblies to Harmony-patch. The ModKit equivalents are:

| BepInEx / Unity | Sol Cesto ModKit |
|---|---|
| Plugin DLLs | `mods/*.js` CommonJS mods |
| `Assembly-CSharp.dll` | `package.nw`, Construct runtime, `scripts/main.js` |
| Classes | `runtime.objects.<objectName>` |
| Fields | `instance.instVars.<varName>` |
| Static/global fields | `runtime.globalVars.<name>` |
| Harmony prefix/postfix/replace | `api.patch.before/after/replace` |
| Known field mappings | `mods/mappings/*.json` |
| Debug console/dump tooling | `_system/inspector.js` and `mods/dumps` |

## Mappings

Known targets live in `mods\mappings\sol-cesto-v100.2.json`. Mods should prefer mapping APIs over hardcoded object/field names when possible:

```js
api.mappings.setValue("money", 999);
var currentMoney = api.mappings.getValue("money");
```

Current known mapping:

```text
money / gold / or
  heros.instVars.or
  metaProgression.instVars.or
  metaProgression.instVars.or_ancien
  metaProgression.instVars.orEver
  hero_or display text
```

For new features, press `F2` in game and inspect the generated runtime dump in `mods\dumps`, then add proven targets to a mapping file.

## Agent Command Queue

The loader also watches `mods\commands\*.json` while the game is running. This avoids relying on keyboard focus when an agent or script needs to inspect the game.

Example command:

```json
{ "id": "dump-now", "command": "dumpRuntimeMap", "sampleLimit": 5 }
```

The loader deletes the command file after processing it and writes:

```text
mods\responses\dump-now.json
```

Useful built-in commands:

- `listCommands`
- `listObjects`
- `describeObject`
- `dumpRuntimeMap`
- `dumpMappings`
- `getMappingValue`
- `setMappingValue`

## Build

From the repository root:

```powershell
dotnet publish .\SolCestoModKitInstaller\SolCestoModKitInstaller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o .\output\SolCestoModKitInstaller
```

The EXE will be written to:

```text
output\SolCestoModKitInstaller\SolCestoModKitInstaller.exe
```

## Modding Skill

The reusable Sol Cesto inspection/modding workflow is captured in [skills/sol-cesto-modding/SKILL.md](skills/sol-cesto-modding/SKILL.md). It documents how to unpack `package.nw`, inspect the NW.js/Construct runtime, find real game-state targets, inject marked helper code, and test modded builds.

## Notes

- Use this only with a copy of Sol Cesto you control.
- Keep `package.nw` as a folder in modded builds; repacking it caused black-screen launches during testing.
- If a value changes visually but gameplay logic ignores it, patch the underlying Construct runtime object state first and mirror the UI second.
- To disable a mod without deleting it, set `"enabled": false` in `mods.json`.
