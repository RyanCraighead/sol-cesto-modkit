---
name: sol-cesto-modding
description: Game-specific workflow for inspecting, unpacking, patching, and testing Sol Cesto v100.2 NW.js/Construct mods.
---

# Sol Cesto Modding Skill

Use this skill when the user wants to inspect, modify, patch, or build tooling for Sol Cesto, especially the Windows `v100.2` build

This is not a general Cheat Engine workflow. For this game, the reliable path is to inspect and patch the NW.js/Construct JavaScript payload inside `package.nw`, then test a separate modded copy of the game.

## Core Mental Model

Sol Cesto is packaged like an NW.js/Construct game:

- `SolCesto.exe` is mostly the NW.js/native runtime shell.
- `package.nw` contains the actual game project and JavaScript runtime payload.
- The useful modding target is usually `package.nw\scripts\main.js`.
- For many features, changing displayed text is not enough. The game logic reads Construct runtime object state.
- Prefer runtime object patches over Cheat Engine memory scans.

The key lesson from the gold investigation:

- Scanning for visible gold like `19` in Cheat Engine gave too many or no useful matches.
- Changing only the visible text to `999` made the UI lie, but shop purchases still failed.
- The correct target was the game state behind the UI, especially runtime objects and instance variables.

Known gold-related runtime targets:

- Object `heros`
  - instance variable `or`
- Object `metaProgression`
  - instance variables `or`, `or_ancien`, `orEver`
- Object `hero_or`
  - display text object for the visible gold value

`or` is French for gold.

## Safety Rules

Always preserve the original game folder.

1. Never patch the original folder in place unless the user explicitly asks.
2. Create a sibling output folder, for example `Sol Cesto modded`.
3. Stop the game before replacing or deleting an existing modded output folder.
4. Avoid editing `SolCesto.exe` unless the JS/NW.js route is proven impossible.
5. Do not blindly repack `package.nw`; a folder-form `package.nw` is safer for NW.js and avoids black-screen failures.
6. Mark injected code with unique begin/end comments so it can be replaced cleanly.

## Fast Recon

Given a candidate game folder, verify the expected files:

```powershell
Get-ChildItem -LiteralPath "C:\path\to\Sol Cesto"
Test-Path -LiteralPath "C:\path\to\Sol Cesto\SolCesto.exe"
Test-Path -LiteralPath "C:\path\to\Sol Cesto\package.nw"
```

Expected structure:

```text
Sol Cesto\
  SolCesto.exe
  package.nw
  nw.dll / other NW.js runtime files
```

`package.nw` may be either:

- a zip-like file, or
- an already extracted directory.

Check with:

```powershell
$package = "C:\path\to\Sol Cesto\package.nw"
Get-Item -LiteralPath $package | Format-List FullName,PSIsContainer,Length
```

## Unpacking / Decompilation Method

This game does not require native EXE disassembly for normal mods. The useful "decompilation" is unpacking the NW.js package and inspecting the generated JavaScript.

Recommended approach:

1. Copy the whole game folder to a modded output folder.
2. Skip copying `package.nw` as-is.
3. Create `output\package.nw` as a directory.
4. If source `package.nw` is a directory, copy it into output.
5. If source `package.nw` is a file, extract it into output using zip extraction.
6. Patch `output\package.nw\scripts\main.js`.
7. Launch `output\SolCesto.exe`.

PowerShell/.NET inspection commands:

```powershell
$game = "C:\path\to\Sol Cesto\"
$pkg = Join-Path $game "package.nw"

if (Test-Path -LiteralPath $pkg -PathType Container) {
    Get-ChildItem -LiteralPath $pkg -Recurse -File | Select-Object -First 40 FullName
} else {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::OpenRead($pkg).Entries |
        Select-Object -First 80 FullName,Length
}
```

Avoid doing destructive deletes through mixed shells. Use PowerShell end to end and verify resolved paths before recursive delete/copy operations.

## What To Inspect

Start with these paths inside extracted `package.nw`:

```text
package.nw\package.json
package.nw\scripts\main.js
package.nw\data.js
package.nw\scripts\
```

Useful search patterns:

```powershell
rg -n --hidden --glob "!node_modules" "or|gold|money|coin|shop|buy|cost|price|metaProgression|heros|hero_or" "C:\path\to\extracted\package.nw"
```

For minified/generated Construct JS, direct source names may be sparse. Search both human terms and observed runtime object names.

Good indicators:

- object names in `runtime.objects`
- Construct instance variable names under `.instVars`
- UI text object names
- event sheet generated code touching purchases, unlocks, costs, progression, or inventory

## Runtime Inspection In Game

When static inspection is not enough, inject or run a small runtime probe in DevTools/Lua/console context.

Get the Construct runtime:

```js
function getRuntime() {
  var iface = window.c3_runtimeInterface;
  if (!iface || !iface._localRuntime || !iface._localRuntime.GetIRuntime) {
    return null;
  }
  return iface._localRuntime.GetIRuntime();
}
```

List object names:

```js
var runtime = getRuntime();
console.log(Object.keys(runtime.objects).sort());
```

List instances and instance variables for likely objects:

```js
function getObjectInstances(objectName) {
  var runtime = getRuntime();
  var objectClass = runtime && runtime.objects ? runtime.objects[objectName] : null;
  var firstInstance = null;

  if (!objectClass) return [];
  if (objectClass.getAllInstances) return objectClass.getAllInstances();
  if (objectClass.getFirstInstance) {
    firstInstance = objectClass.getFirstInstance();
    return firstInstance ? [firstInstance] : [];
  }
  return [];
}

["heros", "metaProgression", "hero_or"].forEach(function (name) {
  console.log(name, getObjectInstances(name).map(function (inst) {
    return inst && inst.instVars ? inst.instVars : inst;
  }));
});
```

Use this pattern for any new feature:

1. Identify the visible UI object.
2. Identify the underlying gameplay/progression object.
3. Change the underlying state first.
4. Update UI text only as a mirror.
5. Test whether game logic accepts the change.

## Gold Mod Implementation Pattern

Do not only set `hero_or` text. That changes the display but not purchase logic.

Set the state objects:

```js
function setMoneyOnObject(objectName, value) {
  var instances = getObjectInstances(objectName);
  var changed = 0;

  for (var i = 0; i < instances.length; i += 1) {
    var inst = instances[i];
    if (!inst || !inst.instVars) continue;

    if (typeof inst.instVars.or !== "undefined") {
      inst.instVars.or = value;
      changed += 1;
    }

    if (objectName === "metaProgression") {
      if (typeof inst.instVars.or_ancien !== "undefined") inst.instVars.or_ancien = value;
      if (typeof inst.instVars.orEver !== "undefined") inst.instVars.orEver = value;
    }
  }

  return changed;
}
```

Mirror the UI:

```js
function updateMoneyText(value) {
  getObjectInstances("hero_or").forEach(function (inst) {
    try {
      if (typeof inst.text !== "undefined") inst.text = String(value);
      if (inst.setText) inst.setText(String(value));
      if (inst.SetText) inst.SetText(String(value));
    } catch (err) {
    }
  });
}
```

Combined operation:

```js
function setMoney(value) {
  var changed = 0;
  changed += setMoneyOnObject("heros", value);
  changed += setMoneyOnObject("metaProgression", value);
  updateMoneyText(value);
  return changed > 0;
}
```

Current hotkey convention:

- `F8`: set money once.
- `F9`: toggle money lock.
- Money lock starts off by default.

This avoids interfering with normal game flow until the user explicitly enables the lock.

## ModKit Loader Pattern

Patch `scripts\main.js` by appending one loader block with unique markers:

```js
/* BEGIN SolCesto mod loader */
(function () {
  "use strict";
  // loader code here
}());
/* END SolCesto mod loader */
```

Before injecting, remove an existing block between the same markers. This makes the install idempotent and prevents duplicate loaders.

Also remove known old marker blocks if migrating from an earlier gold patcher:

```text
/* BEGIN SolCesto money helper */
/* SolCesto gold helper injected by Codex */
```

When generating a patcher, keep patch logic deterministic:

1. Resolve full source and output paths.
2. Validate `SolCesto.exe`.
3. Validate `package.nw`.
4. Reject output folder equal to source folder.
5. Reject output folder inside source folder.
6. Delete/replace only the verified output folder.
7. Copy game files.
8. Extract/copy `package.nw` as a directory.
9. Patch `package.nw\scripts\main.js`.
10. Write the `mods` folder and log the exact modded executable path.

## Testing Checklist

Basic launch test:

1. Run `SolCesto.exe` from the modded output folder.
2. Confirm it does not black-screen.
3. Reach main menu or map.
4. Press `F8`.
5. Confirm visible money changes.
6. Try a shop/unlock/purchase that requires money.
7. Confirm the purchase logic accepts the new value.
8. Press `F9` and confirm lock status message.
9. Spend money while lock is on and confirm it restores.
10. Press `F9` again and confirm lock turns off.

If visible money changes but purchases fail:

- You probably patched only UI text or too late in the scene lifecycle.
- Recheck `heros` and `metaProgression` instances.
- Press `F8` after reaching the menu/shop.
- Add a short delayed retry or lock interval only when needed.

If modded game black-screens:

- Verify `package.nw\scripts\main.js` exists in the output folder.
- Verify `package.nw` is a directory in the modded output.
- Verify the original `SolCesto.exe` and NW.js runtime files were copied unchanged.
- Check for syntax errors in the injected JavaScript.
- Remove the injected block and retest the extracted package layout.
- Avoid repacking until folder-form `package.nw` is proven not to work.

## Feature Discovery Method

For new mods, repeat the same state-first process:

1. Name the feature in gameplay terms, for example unlocks, shop costs, health, hero stats, stage nodes, or progression.
2. Search extracted files for obvious names and French equivalents.
3. Probe `runtime.objects` while the relevant scene is active.
4. Dump instance variables for candidate objects.
5. Change one variable at a time through the runtime console/helper.
6. Observe whether real game logic changes, not only display text.
7. Convert the proven change into a marked injected helper.
8. Put user controls behind hotkeys, checkboxes, or patcher options.
9. Keep defaults conservative so a modded build launches normally.

French terms are worth checking because the gold variable uses `or`:

```text
or, argent, achat, prix, cout, boutique, heros, progression, debloque
```

## ModKit Project Context

The local ModKit installer project is here:

```text
C:\Users\Ryan\Documents\New project 5\SolCestoModKitInstaller
```

Current behavior:

- Windows Forms GUI.
- Selects game folder and output folder.
- Builds a separate modded copy.
- Extracts/copies `package.nw`.
- Injects the Sol Cesto ModKit loader into `scripts\main.js`.
- Writes a `mods` folder beside `SolCesto.exe`.
- Includes `mods\money-lock.js` as a proven example mod when selected.
- `F8` sets money once through the example mod.
- `F9` toggles money lock through the example mod.
- Lock starts off.

Build command:

```powershell
dotnet publish .\SolCestoModKitInstaller\SolCestoModKitInstaller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o .\output\SolCestoModKitInstaller
```

Output:

```text
output\SolCestoModKitInstaller\SolCestoModKitInstaller.exe
```

## Definition Of Done For Future Sol Cesto Mods

A mod is not done when the UI changes. It is done when:

- the original game folder is untouched,
- the modded copy launches,
- the relevant runtime state changes,
- the game logic accepts the change,
- the visible UI stays consistent,
- the injected code is idempotent,
- the installer can recreate the modded build from a clean source folder,
- and the user can control the behavior without editing code manually.
