using System.Globalization;
using System.Text;

namespace SolCestoModKitInstaller;

internal static class FrameworkTemplates
{
    public const string LoaderMarkerStart = "/* BEGIN SolCesto mod loader */";
    public const string LoaderMarkerEnd = "/* END SolCesto mod loader */";

    public static string LoaderScript()
    {
        return """
__START_MARKER__
(function () {
    "use strict";

    if (window.SolCestoModding && window.SolCestoModding.__loaderVersion) {
        return;
    }

    var fs = null;
    var path = null;
    var modsDir = "";
    var registeredMods = [];
    var runtimeReadyCallbacks = [];
    var tickCallbacks = [];
    var hotkeys = {};
    var runtimeWasReady = false;
    var lastMessageAt = 0;

    try {
        fs = require("fs");
        path = require("path");
        modsDir = path.join(path.dirname(process.execPath), "mods");
    } catch (err) {
        console.error("[SolCesto Modding] Node APIs are unavailable.", err);
    }

    function getRuntime() {
        var iface = window.c3_runtimeInterface;
        if (!iface || !iface._localRuntime || !iface._localRuntime.GetIRuntime) {
            return null;
        }

        return iface._localRuntime.GetIRuntime();
    }

    function getObjectInstances(objectName) {
        var runtime = getRuntime();
        var objectClass = runtime && runtime.objects ? runtime.objects[objectName] : null;
        var firstInstance = null;

        if (!objectClass) {
            return [];
        }

        if (objectClass.getAllInstances) {
            return objectClass.getAllInstances();
        }

        if (objectClass.getFirstInstance) {
            firstInstance = objectClass.getFirstInstance();
            return firstInstance ? [firstInstance] : [];
        }

        return [];
    }

    function setInstanceVar(objectName, varName, value) {
        var instances = getObjectInstances(objectName);
        var changed = 0;
        var i;
        var inst;

        for (i = 0; i < instances.length; i += 1) {
            inst = instances[i];
            if (inst && inst.instVars && typeof inst.instVars[varName] !== "undefined") {
                inst.instVars[varName] = value;
                changed += 1;
            }
        }

        return changed;
    }

    function getGlobalVar(name) {
        var runtime = getRuntime();
        if (!runtime || !runtime.globalVars || typeof runtime.globalVars[name] === "undefined") {
            return undefined;
        }

        return runtime.globalVars[name];
    }

    function setGlobalVar(name, value) {
        var runtime = getRuntime();
        if (!runtime || !runtime.globalVars || typeof runtime.globalVars[name] === "undefined") {
            return false;
        }

        runtime.globalVars[name] = value;
        return true;
    }

    function showMessage(text) {
        var now = Date.now();
        var el;

        if (now - lastMessageAt < 125) {
            return;
        }

        lastMessageAt = now;
        el = document.getElementById("solcesto-modkit-status");
        if (!el) {
            el = document.createElement("div");
            el.id = "solcesto-modkit-status";
            el.style.cssText = "position:fixed;left:16px;top:16px;z-index:2147483647;padding:8px 10px;background:rgba(0,0,0,.75);color:#fff;font:14px/1.3 sans-serif;border-radius:4px;pointer-events:none";
            document.documentElement.appendChild(el);
        }

        el.textContent = text;
        el.style.display = "block";
        clearTimeout(el._hideTimer);
        el._hideTimer = setTimeout(function () {
            el.style.display = "none";
        }, 1600);
    }

    function log(message) {
        console.log("[SolCesto Modding] " + message);
    }

    function warn(message) {
        console.warn("[SolCesto Modding] " + message);
    }

    function onRuntimeReady(callback) {
        if (typeof callback !== "function") {
            return;
        }

        if (getRuntime()) {
            try {
                callback(api);
            } catch (err) {
                console.error("[SolCesto Modding] Runtime-ready callback failed.", err);
            }
        } else {
            runtimeReadyCallbacks.push(callback);
        }
    }

    function onTick(callback) {
        if (typeof callback === "function") {
            tickCallbacks.push(callback);
        }
    }

    function addHotkey(code, callback) {
        if (!hotkeys[code]) {
            hotkeys[code] = [];
        }

        hotkeys[code].push(callback);
    }

    function registerMod(metadata) {
        metadata = metadata || {};
        registeredMods.push(metadata);
        log("Registered mod: " + (metadata.name || metadata.id || "unnamed mod"));
    }

    var api = {
        __loaderVersion: "0.1.0",
        game: "Sol Cesto",
        modsDir: modsDir,
        getRuntime: getRuntime,
        getObjectInstances: getObjectInstances,
        setInstanceVar: setInstanceVar,
        getGlobalVar: getGlobalVar,
        setGlobalVar: setGlobalVar,
        onRuntimeReady: onRuntimeReady,
        onTick: onTick,
        addHotkey: addHotkey,
        showMessage: showMessage,
        log: log,
        warn: warn,
        registerMod: registerMod,
        getRegisteredMods: function () {
            return registeredMods.slice(0);
        }
    };

    window.SolCestoModding = api;

    window.addEventListener("keydown", function (event) {
        var handlers = hotkeys[event.code];
        var i;

        if (!handlers || event.repeat) {
            return;
        }

        for (i = 0; i < handlers.length; i += 1) {
            try {
                handlers[i](event, api);
            } catch (err) {
                console.error("[SolCesto Modding] Hotkey failed: " + event.code, err);
            }
        }
    }, true);

    function readManifest() {
        var manifestPath;
        var text;
        var parsed;
        var files;

        if (!fs || !path || !modsDir) {
            return [];
        }

        manifestPath = path.join(modsDir, "mods.json");
        if (fs.existsSync(manifestPath)) {
            text = fs.readFileSync(manifestPath, "utf8");
            parsed = JSON.parse(text);
            if (Array.isArray(parsed)) {
                return parsed;
            }

            if (parsed && Array.isArray(parsed.mods)) {
                return parsed.mods;
            }
        }

        files = fs.readdirSync(modsDir).filter(function (file) {
            return /\.js$/i.test(file) && file.charAt(0) !== "_";
        });

        return files.map(function (file) {
            return { file: file, enabled: true };
        });
    }

    function loadMod(entry) {
        var relativeFile = typeof entry === "string" ? entry : entry.file;
        var enabled = typeof entry === "string" ? true : entry.enabled !== false;
        var fullPath;
        var modsRoot;
        var loaded;

        if (!enabled || !relativeFile) {
            return;
        }

        fullPath = path.resolve(modsDir, relativeFile);
        modsRoot = path.resolve(modsDir) + path.sep;
        if (fullPath.toLowerCase().indexOf(modsRoot.toLowerCase()) !== 0) {
            warn("Skipping mod outside mods folder: " + relativeFile);
            return;
        }

        loaded = require(fullPath);
        if (typeof loaded === "function") {
            loaded(api);
        } else if (loaded && typeof loaded.load === "function") {
            loaded.load(api);
        } else {
            warn(relativeFile + " did not export a function or load(api).");
        }

        log("Loaded " + relativeFile);
    }

    function loadMods() {
        var manifest;
        var i;

        if (!fs || !path || !modsDir) {
            warn("Cannot load mods because Node APIs are unavailable.");
            return;
        }

        if (!fs.existsSync(modsDir)) {
            warn("Mods folder not found: " + modsDir);
            return;
        }

        try {
            manifest = readManifest();
            for (i = 0; i < manifest.length; i += 1) {
                try {
                    loadMod(manifest[i]);
                } catch (err) {
                    console.error("[SolCesto Modding] Failed to load mod.", manifest[i], err);
                }
            }
        } catch (err2) {
            console.error("[SolCesto Modding] Failed to read mods manifest.", err2);
        }
    }

    setInterval(function () {
        var runtime = getRuntime();
        var callbacks;
        var i;

        if (runtime && !runtimeWasReady) {
            runtimeWasReady = true;
            callbacks = runtimeReadyCallbacks.slice(0);
            runtimeReadyCallbacks.length = 0;
            for (i = 0; i < callbacks.length; i += 1) {
                try {
                    callbacks[i](api);
                } catch (err) {
                    console.error("[SolCesto Modding] Runtime-ready callback failed.", err);
                }
            }
        }

        if (runtime) {
            for (i = 0; i < tickCallbacks.length; i += 1) {
                try {
                    tickCallbacks[i](api);
                } catch (err2) {
                    console.error("[SolCesto Modding] Tick callback failed.", err2);
                }
            }
        }
    }, 250);

    loadMods();
    log("Loader installed. Mods folder: " + modsDir);
}());
__END_MARKER__
""".Replace("__START_MARKER__", LoaderMarkerStart, StringComparison.Ordinal)
           .Replace("__END_MARKER__", LoaderMarkerEnd, StringComparison.Ordinal);
    }

    public static string Manifest(bool includeMoneyMod)
    {
        return includeMoneyMod
            ? """
{
  "mods": [
    {
      "file": "money-lock.js",
      "enabled": true
    }
  ]
}
"""
            : """
{
  "mods": []
}
""";
    }

    public static string MoneyMod(int moneyValue, bool lockStartsOn)
    {
        var value = moneyValue.ToString(CultureInfo.InvariantCulture);
        var startsOn = lockStartsOn ? "true" : "false";
        return """
module.exports = function (api) {
    "use strict";

    var MONEY_VALUE = __MONEY_VALUE__;
    var lockMoney = __LOCK_STARTS_ON__;

    api.registerMod({
        id: "money-lock",
        name: "Money Lock",
        version: "1.0.0",
        description: "Sets and optionally locks Sol Cesto run/menu money."
    });

    function setTextObject(objectName, value) {
        var instances = api.getObjectInstances(objectName);
        var i;
        var inst;

        for (i = 0; i < instances.length; i += 1) {
            inst = instances[i];
            try {
                if (typeof inst.text !== "undefined") {
                    inst.text = String(value);
                }
                if (inst.setText) {
                    inst.setText(String(value));
                }
                if (inst.SetText) {
                    inst.SetText(String(value));
                }
            } catch (err) {
            }
        }
    }

    function setMoneyOnObject(objectName, value) {
        var instances = api.getObjectInstances(objectName);
        var changed = 0;
        var i;
        var inst;

        for (i = 0; i < instances.length; i += 1) {
            inst = instances[i];
            if (!inst || !inst.instVars) {
                continue;
            }

            if (typeof inst.instVars.or !== "undefined") {
                inst.instVars.or = value;
                changed += 1;
            }

            if (objectName === "metaProgression") {
                if (typeof inst.instVars.or_ancien !== "undefined") {
                    inst.instVars.or_ancien = value;
                }
                if (typeof inst.instVars.orEver !== "undefined") {
                    inst.instVars.orEver = value;
                }
            }
        }

        return changed;
    }

    function setMoney(value) {
        var changed = 0;
        changed += setMoneyOnObject("heros", value);
        changed += setMoneyOnObject("metaProgression", value);
        setTextObject("hero_or", value);
        return changed > 0;
    }

    window.solCestoSetMoney = function (value) {
        var numericValue = Number(value);
        if (!isFinite(numericValue)) {
            numericValue = MONEY_VALUE;
        }

        if (setMoney(numericValue)) {
            api.showMessage("Money set to " + numericValue);
            return true;
        }

        api.showMessage("Money target is not available yet");
        return false;
    };

    api.addHotkey("F8", function (event) {
        event.preventDefault();
        window.solCestoSetMoney(MONEY_VALUE);
    });

    api.addHotkey("F9", function (event) {
        event.preventDefault();
        lockMoney = !lockMoney;
        if (lockMoney) {
            setMoney(MONEY_VALUE);
        }
        api.showMessage(lockMoney ? "Money lock on: " + MONEY_VALUE : "Money lock off");
    });

    api.onRuntimeReady(function () {
        if (lockMoney) {
            setMoney(MONEY_VALUE);
            api.showMessage("Money lock on: " + MONEY_VALUE);
        }
    });

    api.onTick(function () {
        if (lockMoney) {
            setMoney(MONEY_VALUE);
        }
    });
};
""".Replace("__MONEY_VALUE__", value, StringComparison.Ordinal)
           .Replace("__LOCK_STARTS_ON__", startsOn, StringComparison.Ordinal);
    }

    public static string ExampleOverlayMod()
    {
        return """
module.exports = function (api) {
    "use strict";

    api.registerMod({
        id: "example-overlay",
        name: "Example Overlay",
        version: "1.0.0"
    });

    api.addHotkey("F10", function (event) {
        event.preventDefault();
        api.showMessage("Example mod is loaded. Runtime ready: " + !!api.getRuntime());
    });
};
""";
    }

    public static string ModsReadme()
    {
        return """
# Sol Cesto ModKit Mods

This folder is loaded by the Sol Cesto ModKit bootstrap in `package.nw\scripts\main.js`.

## Enable or disable mods

Edit `mods.json`:

```json
{
  "mods": [
    { "file": "money-lock.js", "enabled": true }
  ]
}
```

Set `enabled` to `false` to disable a mod without deleting it.

## Mod format

Each mod is a CommonJS module that exports a function:

```js
module.exports = function (api) {
  api.registerMod({ id: "my-mod", name: "My Mod", version: "1.0.0" });
  api.onRuntimeReady(function () {
    var runtime = api.getRuntime();
  });
};
```

Useful API methods:

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

Examples live in `examples`. Copy one into this folder and add it to `mods.json` to enable it.
""";
    }

    public static void WriteUtf8NoBom(string path, string text)
    {
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
