# Sol Cesto ModKit Installer

A small BepInEx-style mod framework for the Windows NW.js/Construct build of Sol Cesto `v100.2`.

BepInEx itself targets Unity/.NET games, so this project uses the same idea rather than the same runtime: install one bootstrap loader into `package.nw\scripts\main.js`, then load separate JavaScript mods from a normal `mods` folder beside `SolCesto.exe`.

The installer creates a separate modded copy of the game. It does not modify the original Sol Cesto folder.

## What It Installs

```text
Sol Cesto modded\
  SolCesto.exe
  package.nw\
    scripts\main.js        bootstrap loader injected here
  mods\
    mods.json              enabled mod list
    money-lock.js          example/proven gold mod
    README.md              local modding notes
    examples\
      example-overlay.js
```

The bootstrap exposes `window.SolCestoModding` and loads enabled CommonJS mods from `mods\mods.json`.

## Using The Installer

1. Run `SolCestoModKitInstaller.exe`.
2. For `Game folder`, select the folder containing `SolCesto.exe` and `package.nw`.
3. Leave `Output folder` as the auto-filled `Sol Cesto modded` folder, or choose another separate folder.
4. Leave `Include money mod` checked if you want the known working gold mod installed.
5. Click `Create modded build`.
6. Run `SolCesto.exe` from the modded output folder.

Money mod hotkeys:

- `F8`: set money once.
- `F9`: toggle money lock.

By default the money mod is loaded but passive until you press `F8` or turn the lock on with `F9`.

The money mod updates the real Construct runtime state, not only the visible text:

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
- `api.getObjectInstances(objectName)`
- `api.setInstanceVar(objectName, varName, value)`
- `api.getGlobalVar(name)`
- `api.setGlobalVar(name, value)`
- `api.onRuntimeReady(callback)`
- `api.onTick(callback)`
- `api.addHotkey("F10", callback)`
- `api.showMessage(text)`
- `api.log(text)`
- `api.warn(text)`
- `api.registerMod(metadata)`
- `api.getRegisteredMods()`

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
