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
    var gameDir = "";
    var modsDir = "";
    var registeredMods = [];
    var runtimeReadyCallbacks = [];
    var tickCallbacks = [];
    var hotkeys = Object.create(null);
    var listeners = Object.create(null);
    var patchTargets = Object.create(null);
    var watchers = Object.create(null);
    var commandHandlers = Object.create(null);
    var processedCommandIds = Object.create(null);
    var mappingStore = Object.create(null);
    var mappingAliases = Object.create(null);
    var runtimeWasReady = false;
    var lastMessageAt = 0;
    var logRecords = [];
    var api = null;

    try {
        fs = require("fs");
        path = require("path");
        gameDir = path.dirname(process.execPath);
        modsDir = path.join(gameDir, "mods");
    } catch (err) {
        console.error("[SolCesto ModKit] Node APIs are unavailable.", err);
    }

    function nowIso() {
        return new Date().toISOString();
    }

    function fileTimestamp() {
        return nowIso().replace(/[:.]/g, "-");
    }

    function ensureDir(dir) {
        if (fs && dir && !fs.existsSync(dir)) {
            fs.mkdirSync(dir, { recursive: true });
        }
    }

    function safeResolve(baseDir, relativeFile) {
        var fullPath;
        var root;

        if (!fs || !path || !baseDir || !relativeFile) {
            return null;
        }

        fullPath = path.resolve(baseDir, relativeFile);
        root = path.resolve(baseDir) + path.sep;
        if (fullPath.toLowerCase().indexOf(root.toLowerCase()) !== 0 && fullPath.toLowerCase() !== path.resolve(baseDir).toLowerCase()) {
            warn("Blocked path outside mods folder: " + relativeFile);
            return null;
        }

        return fullPath;
    }

    function resolveModPath(relativeFile) {
        return safeResolve(modsDir, relativeFile);
    }

    function readText(relativeFile, fallback) {
        var fullPath = resolveModPath(relativeFile);
        if (!fullPath || !fs.existsSync(fullPath)) {
            return typeof fallback === "undefined" ? null : fallback;
        }

        return fs.readFileSync(fullPath, "utf8");
    }

    function writeText(relativeFile, text) {
        var fullPath = resolveModPath(relativeFile);
        if (!fullPath) {
            return false;
        }

        ensureDir(path.dirname(fullPath));
        fs.writeFileSync(fullPath, String(text), "utf8");
        return true;
    }

    function readJson(relativeFile, fallback) {
        var text = readText(relativeFile, null);
        if (text === null) {
            return typeof fallback === "undefined" ? null : fallback;
        }

        try {
            return JSON.parse(text);
        } catch (err) {
            error("Failed to parse JSON: " + relativeFile, err);
            return typeof fallback === "undefined" ? null : fallback;
        }
    }

    function writeJson(relativeFile, data) {
        return writeText(relativeFile, JSON.stringify(data, null, 2) + "\n");
    }

    function appendText(relativeFile, text) {
        var fullPath = resolveModPath(relativeFile);
        if (!fullPath) {
            return false;
        }

        ensureDir(path.dirname(fullPath));
        fs.appendFileSync(fullPath, String(text), "utf8");
        return true;
    }

    function writeDump(name, data) {
        var safeName = String(name || "dump").replace(/[^a-z0-9_.-]+/gi, "-");
        var relativeFile = "dumps/" + safeName + "-" + fileTimestamp() + ".json";
        writeJson(relativeFile, data);
        return relativeFile;
    }

    function writeResponse(id, response) {
        var safeId = String(id || ("response-" + fileTimestamp())).replace(/[^a-z0-9_.-]+/gi, "-");
        writeJson("responses/" + safeId + ".json", response);
        return "responses/" + safeId + ".json";
    }

    function recordLog(level, message, err) {
        var text = String(message);
        var row = {
            at: nowIso(),
            level: level,
            message: text
        };

        if (err) {
            row.error = err && err.stack ? err.stack : String(err);
        }

        logRecords.push(row);
        if (logRecords.length > 500) {
            logRecords.shift();
        }

        if (fs && modsDir) {
            try {
                appendText("logs/modkit.log", "[" + row.at + "] [" + level.toUpperCase() + "] " + text + (row.error ? "\n" + row.error : "") + "\n");
            } catch (writeErr) {
            }
        }
    }

    function log(message) {
        recordLog("info", message);
        console.log("[SolCesto ModKit] " + message);
    }

    function warn(message) {
        recordLog("warn", message);
        console.warn("[SolCesto ModKit] " + message);
    }

    function error(message, err) {
        recordLog("error", message, err);
        console.error("[SolCesto ModKit] " + message, err || "");
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
        }, 1800);
    }

    function emit(eventName, payload) {
        var handlers = listeners[eventName];
        var i;

        if (!handlers) {
            return;
        }

        for (i = 0; i < handlers.length; i += 1) {
            try {
                handlers[i](payload, api);
            } catch (err) {
                error("Event listener failed: " + eventName, err);
            }
        }
    }

    function on(eventName, callback) {
        if (typeof callback !== "function") {
            return function () {};
        }

        if (!listeners[eventName]) {
            listeners[eventName] = [];
        }

        listeners[eventName].push(callback);
        return function () {
            off(eventName, callback);
        };
    }

    function off(eventName, callback) {
        var handlers = listeners[eventName];
        var index;

        if (!handlers) {
            return false;
        }

        index = handlers.indexOf(callback);
        if (index < 0) {
            return false;
        }

        handlers.splice(index, 1);
        return true;
    }

    function getRuntime() {
        var iface = window.c3_runtimeInterface;
        if (!iface || !iface._localRuntime || !iface._localRuntime.GetIRuntime) {
            return null;
        }

        return iface._localRuntime.GetIRuntime();
    }

    function listObjects() {
        var runtime = getRuntime();
        if (!runtime || !runtime.objects) {
            return [];
        }

        return Object.keys(runtime.objects).sort();
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

    function cloneInstVars(inst) {
        var result = {};
        var key;

        if (!inst || !inst.instVars) {
            return result;
        }

        for (key in inst.instVars) {
            if (Object.prototype.hasOwnProperty.call(inst.instVars, key)) {
                result[key] = inst.instVars[key];
            }
        }

        return result;
    }

    function getInstanceKey(inst, index) {
        if (!inst) {
            return "index:" + index;
        }

        if (typeof inst.uid !== "undefined") {
            return "uid:" + inst.uid;
        }

        if (typeof inst._uid !== "undefined") {
            return "uid:" + inst._uid;
        }

        if (typeof inst.iid !== "undefined") {
            return "iid:" + inst.iid;
        }

        return "index:" + index;
    }

    function describeObject(objectName, options) {
        var instances = getObjectInstances(objectName);
        var sampleLimit = options && options.sampleLimit ? options.sampleLimit : 3;
        var varNames = Object.create(null);
        var samples = [];
        var i;
        var inst;
        var vars;
        var key;

        for (i = 0; i < instances.length; i += 1) {
            inst = instances[i];
            vars = cloneInstVars(inst);
            for (key in vars) {
                if (Object.prototype.hasOwnProperty.call(vars, key)) {
                    varNames[key] = true;
                }
            }

            if (samples.length < sampleLimit) {
                samples.push({
                    key: getInstanceKey(inst, i),
                    instVars: vars,
                    text: typeof inst.text !== "undefined" ? inst.text : undefined,
                    typeName: inst && inst.constructor && inst.constructor.name ? inst.constructor.name : undefined
                });
            }
        }

        return {
            object: objectName,
            count: instances.length,
            instanceVars: Object.keys(varNames).sort(),
            samples: samples
        };
    }

    function dumpRuntimeMap(options) {
        var objectNames = listObjects();
        var runtime = getRuntime();
        var globalVars = {};
        var objects = {};
        var i;
        var key;

        if (runtime && runtime.globalVars) {
            for (key in runtime.globalVars) {
                if (Object.prototype.hasOwnProperty.call(runtime.globalVars, key)) {
                    globalVars[key] = runtime.globalVars[key];
                }
            }
        }

        for (i = 0; i < objectNames.length; i += 1) {
            objects[objectNames[i]] = describeObject(objectNames[i], options);
        }

        return {
            game: "Sol Cesto",
            modkitVersion: api.__loaderVersion,
            createdAt: nowIso(),
            objectCount: objectNames.length,
            objects: objects,
            globalVars: globalVars,
            registeredMods: registeredMods.slice(0),
            mappings: Object.keys(mappingStore).sort()
        };
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

    function setTextObject(objectName, value) {
        var instances = getObjectInstances(objectName);
        var changed = 0;
        var i;
        var inst;

        for (i = 0; i < instances.length; i += 1) {
            inst = instances[i];
            try {
                if (typeof inst.text !== "undefined") {
                    inst.text = String(value);
                    changed += 1;
                }
                if (inst.setText) {
                    inst.setText(String(value));
                    changed += 1;
                }
                if (inst.SetText) {
                    inst.SetText(String(value));
                    changed += 1;
                }
            } catch (err) {
            }
        }

        return changed;
    }

    function loadMappings() {
        var mappingsDir;
        var files;
        var i;

        if (!fs || !path || !modsDir) {
            return;
        }

        mappingsDir = path.join(modsDir, "mappings");
        if (!fs.existsSync(mappingsDir)) {
            return;
        }

        files = fs.readdirSync(mappingsDir).filter(function (file) {
            return /\.json$/i.test(file);
        }).sort();

        for (i = 0; i < files.length; i += 1) {
            try {
                registerMappingPack(JSON.parse(fs.readFileSync(path.join(mappingsDir, files[i]), "utf8")), files[i]);
            } catch (err) {
                error("Failed to load mapping file: " + files[i], err);
            }
        }
    }

    function registerMappingPack(pack, sourceFile) {
        var mappings;
        var id;
        var mapping;
        var aliases;
        var i;

        if (!pack || !pack.mappings) {
            return;
        }

        mappings = pack.mappings;
        for (id in mappings) {
            if (!Object.prototype.hasOwnProperty.call(mappings, id)) {
                continue;
            }

            mapping = mappings[id] || {};
            mapping.id = mapping.id || id;
            mapping.sourceFile = sourceFile || mapping.sourceFile || "runtime";
            mappingStore[id] = mapping;

            aliases = mapping.aliases || [];
            for (i = 0; i < aliases.length; i += 1) {
                mappingAliases[aliases[i]] = id;
            }
        }
    }

    function getMapping(id) {
        var resolved = mappingAliases[id] || id;
        return mappingStore[resolved] || null;
    }

    function listMappings() {
        return Object.keys(mappingStore).sort();
    }

    function getMappedValue(id) {
        var mapping = getMapping(id);
        var target;
        var vars;
        var instances;
        var i;
        var j;
        var inst;

        if (!mapping || !mapping.targets) {
            return undefined;
        }

        for (i = 0; i < mapping.targets.length; i += 1) {
            target = mapping.targets[i];
            vars = target.vars || (target.var ? [target.var] : []);
            instances = getObjectInstances(target.object);
            for (j = 0; j < instances.length; j += 1) {
                inst = instances[j];
                if (inst && inst.instVars && vars.length && typeof inst.instVars[vars[0]] !== "undefined") {
                    return inst.instVars[vars[0]];
                }
            }
        }

        return undefined;
    }

    function setMappedValue(id, value) {
        var mapping = getMapping(id);
        var changed = 0;
        var displayChanged = 0;
        var i;
        var j;
        var target;
        var vars;
        var display;

        if (!mapping) {
            warn("Mapping not found: " + id);
            return { changed: 0, displayChanged: 0 };
        }

        for (i = 0; i < (mapping.targets || []).length; i += 1) {
            target = mapping.targets[i];
            vars = target.vars || (target.var ? [target.var] : []);
            for (j = 0; j < vars.length; j += 1) {
                changed += setInstanceVar(target.object, vars[j], value);
            }
        }

        for (i = 0; i < (mapping.displays || []).length; i += 1) {
            display = mapping.displays[i];
            displayChanged += setTextObject(display.object, value);
        }

        return { changed: changed, displayChanged: displayChanged };
    }

    function dumpMappingValues() {
        var ids = listMappings();
        var dump = {
            createdAt: nowIso(),
            mappings: {},
            patches: listPatches(),
            registeredMods: registeredMods.slice(0)
        };
        var i;

        for (i = 0; i < ids.length; i += 1) {
            dump.mappings[ids[i]] = {
                definition: getMapping(ids[i]),
                value: getMappedValue(ids[i])
            };
        }

        return dump;
    }

    function readConfig(modId, defaults) {
        var config = readJson("config/" + modId + ".json", null);
        var merged = {};
        var key;

        defaults = defaults || {};
        for (key in defaults) {
            if (Object.prototype.hasOwnProperty.call(defaults, key)) {
                merged[key] = defaults[key];
            }
        }

        if (config) {
            for (key in config) {
                if (Object.prototype.hasOwnProperty.call(config, key)) {
                    merged[key] = config[key];
                }
            }
        } else {
            writeJson("config/" + modId + ".json", merged);
        }

        return merged;
    }

    function writeConfig(modId, config) {
        return writeJson("config/" + modId + ".json", config || {});
    }

    function resolveFunctionPath(target) {
        var parts;
        var owner;
        var i;
        var prop;

        if (typeof target !== "string") {
            return null;
        }

        parts = target.split(".");
        if (parts[0] === "window") {
            parts.shift();
            owner = window;
        } else if (parts[0] === "document") {
            parts.shift();
            owner = document;
        } else if (parts[0] === "api") {
            parts.shift();
            owner = api;
        } else {
            owner = window;
        }

        for (i = 0; i < parts.length - 1; i += 1) {
            owner = owner ? owner[parts[i]] : null;
        }

        prop = parts[parts.length - 1];
        if (!owner || !prop) {
            return null;
        }

        return { owner: owner, prop: prop, target: target };
    }

    function installPatchTarget(target, resolved) {
        var original = resolved.owner[resolved.prop];
        var record;

        if (typeof original !== "function") {
            warn("Patch target is not a function: " + target);
            return null;
        }

        record = {
            target: target,
            owner: resolved.owner,
            prop: resolved.prop,
            original: original,
            handlers: []
        };

        resolved.owner[resolved.prop] = function () {
            var context = this;
            var state = {
                target: target,
                args: Array.prototype.slice.call(arguments),
                result: undefined,
                skipOriginal: false
            };
            var before = record.handlers.filter(function (handler) { return handler.kind === "before"; });
            var after = record.handlers.filter(function (handler) { return handler.kind === "after"; });
            var replace = record.handlers.filter(function (handler) { return handler.kind === "replace"; });
            var i;
            var ret;
            var replacement;

            for (i = 0; i < before.length; i += 1) {
                ret = before[i].callback.call(context, state, api);
                if (Array.isArray(ret)) {
                    state.args = ret;
                } else if (ret === false) {
                    state.skipOriginal = true;
                } else if (ret && typeof ret === "object") {
                    if (Array.isArray(ret.args)) {
                        state.args = ret.args;
                    }
                    if (typeof ret.skipOriginal !== "undefined") {
                        state.skipOriginal = !!ret.skipOriginal;
                    }
                    if (typeof ret.result !== "undefined") {
                        state.result = ret.result;
                    }
                }
            }

            if (!state.skipOriginal) {
                if (replace.length) {
                    replacement = replace[replace.length - 1];
                    state.result = replacement.callback.call(context, function () {
                        var originalArgs = arguments.length ? Array.prototype.slice.call(arguments) : state.args;
                        return record.original.apply(context, originalArgs);
                    }, state, api);
                } else {
                    state.result = record.original.apply(context, state.args);
                }
            }

            for (i = 0; i < after.length; i += 1) {
                ret = after[i].callback.call(context, state, api);
                if (typeof ret !== "undefined") {
                    state.result = ret;
                }
            }

            return state.result;
        };

        patchTargets[target] = record;
        return record;
    }

    function addFunctionPatch(kind, target, id, callback) {
        var resolved;
        var record;
        var handler;

        if (typeof id === "function") {
            callback = id;
            id = kind + ":" + target + ":" + Date.now();
        }

        if (typeof callback !== "function") {
            throw new Error("Patch callback must be a function.");
        }

        resolved = resolveFunctionPath(target);
        if (!resolved) {
            warn("Patch target could not be resolved: " + target);
            return null;
        }

        record = patchTargets[target] || installPatchTarget(target, resolved);
        if (!record) {
            return null;
        }

        handler = { id: id, kind: kind, target: target, callback: callback };
        record.handlers.push(handler);
        log("Installed " + kind + " patch " + id + " on " + target);
        return {
            id: id,
            target: target,
            unpatch: function () {
                return unpatch(id);
            }
        };
    }

    function wrapFunction(owner, prop, id, handlers) {
        var target = "object:" + id + ":" + prop;
        patchTargets[target] = {
            target: target,
            owner: owner,
            prop: prop,
            original: owner[prop],
            handlers: []
        };
        installPatchTarget(target, patchTargets[target]);
        if (handlers.before) {
            patchTargets[target].handlers.push({ id: id + ":before", kind: "before", target: target, callback: handlers.before });
        }
        if (handlers.after) {
            patchTargets[target].handlers.push({ id: id + ":after", kind: "after", target: target, callback: handlers.after });
        }
        if (handlers.replace) {
            patchTargets[target].handlers.push({ id: id + ":replace", kind: "replace", target: target, callback: handlers.replace });
        }
        return patchTargets[target];
    }

    function unpatch(id) {
        var target;
        var record;
        var i;

        for (target in patchTargets) {
            if (!Object.prototype.hasOwnProperty.call(patchTargets, target)) {
                continue;
            }

            record = patchTargets[target];
            for (i = record.handlers.length - 1; i >= 0; i -= 1) {
                if (record.handlers[i].id === id || record.target === id) {
                    record.handlers.splice(i, 1);
                }
            }

            if (!record.handlers.length || record.target === id) {
                record.owner[record.prop] = record.original;
                delete patchTargets[target];
                log("Unpatched " + target);
            }
        }

        return true;
    }

    function listPatches() {
        var result = [];
        var target;

        for (target in patchTargets) {
            if (Object.prototype.hasOwnProperty.call(patchTargets, target)) {
                result.push({
                    target: target,
                    handlers: patchTargets[target].handlers.map(function (handler) {
                        return { id: handler.id, kind: handler.kind };
                    })
                });
            }
        }

        return result;
    }

    function stableStringify(value) {
        try {
            return JSON.stringify(value);
        } catch (err) {
            return String(value);
        }
    }

    function addWatcher(id, reader, callback, options) {
        if (typeof id === "function") {
            options = callback;
            callback = id;
            id = "watch:" + Date.now();
        }

        if (typeof reader !== "function" || typeof callback !== "function") {
            throw new Error("Watcher requires reader and callback functions.");
        }

        watchers[id] = {
            id: id,
            reader: reader,
            callback: callback,
            options: options || {},
            hasLast: false,
            lastValue: undefined,
            lastKey: ""
        };

        return {
            id: id,
            unwatch: function () {
                delete watchers[id];
            }
        };
    }

    function processWatchers() {
        var id;
        var watcher;
        var value;
        var key;
        var first;

        for (id in watchers) {
            if (!Object.prototype.hasOwnProperty.call(watchers, id)) {
                continue;
            }

            watcher = watchers[id];
            try {
                value = watcher.reader(api);
                key = stableStringify(value);
                first = !watcher.hasLast;
                if (first || key !== watcher.lastKey) {
                    if (!first || watcher.options.immediate) {
                        watcher.callback({
                            id: id,
                            first: first,
                            value: value,
                            previous: watcher.lastValue
                        }, api);
                    }
                    watcher.hasLast = true;
                    watcher.lastValue = value;
                    watcher.lastKey = key;
                }
            } catch (err) {
                error("Watcher failed: " + id, err);
            }
        }
    }

    function watchInstanceVar(id, objectName, varName, callback, options) {
        return addWatcher(id, function () {
            return getObjectInstances(objectName).map(function (inst, index) {
                return {
                    key: getInstanceKey(inst, index),
                    value: inst && inst.instVars ? inst.instVars[varName] : undefined
                };
            });
        }, callback, options);
    }

    function watchGlobalVar(id, name, callback, options) {
        return addWatcher(id, function () {
            return getGlobalVar(name);
        }, callback, options);
    }

    function watchMappedValue(id, mappingId, callback, options) {
        return addWatcher(id, function () {
            return getMappedValue(mappingId);
        }, callback, options);
    }

    function registerCommand(name, handler) {
        if (!name || typeof handler !== "function") {
            throw new Error("Command handlers require a name and function.");
        }

        commandHandlers[name] = handler;
        return name;
    }

    function listCommands() {
        return Object.keys(commandHandlers).sort();
    }

    function executeCommand(command) {
        var name = command.command || command.name || command.type;
        var handler = commandHandlers[name];

        if (!handler) {
            throw new Error("Unknown ModKit command: " + name);
        }

        return handler(command, api);
    }

    function processCommandFile(fullPath) {
        var command;
        var id;
        var response;
        var result;

        command = JSON.parse(fs.readFileSync(fullPath, "utf8"));
        id = command.id || path.basename(fullPath, ".json");

        if (processedCommandIds[id]) {
            return;
        }

        processedCommandIds[id] = true;
        response = {
            id: id,
            command: command.command || command.name || command.type,
            ok: false,
            createdAt: nowIso()
        };

        try {
            result = executeCommand(command);
            response.ok = true;
            response.result = result;
        } catch (err) {
            response.error = err && err.stack ? err.stack : String(err);
            error("Command failed: " + response.command, err);
        }

        response.responseFile = writeResponse(id, response);

        try {
            fs.unlinkSync(fullPath);
        } catch (deleteErr) {
            warn("Could not delete processed command file: " + fullPath);
        }
    }

    function processCommands() {
        var commandsDir;
        var files;
        var i;

        if (!fs || !path || !modsDir) {
            return;
        }

        commandsDir = path.join(modsDir, "commands");
        if (!fs.existsSync(commandsDir)) {
            return;
        }

        files = fs.readdirSync(commandsDir).filter(function (file) {
            return /\.json$/i.test(file);
        }).sort();

        for (i = 0; i < files.length; i += 1) {
            try {
                processCommandFile(path.join(commandsDir, files[i]));
            } catch (err) {
                error("Failed to process command file: " + files[i], err);
            }
        }
    }

    function onRuntimeReady(callback) {
        if (typeof callback !== "function") {
            return;
        }

        if (getRuntime()) {
            try {
                callback(api);
            } catch (err) {
                error("Runtime-ready callback failed.", err);
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
        emit("mod:registered", metadata);
    }

    api = {
        __loaderVersion: "0.2.0",
        game: "Sol Cesto",
        gameVersion: "v100.2",
        gameDir: gameDir,
        modsDir: modsDir,
        paths: {
            gameDir: gameDir,
            modsDir: modsDir,
            configDir: fs && path ? path.join(modsDir, "config") : "",
            docsDir: fs && path ? path.join(modsDir, "docs") : "",
            dumpsDir: fs && path ? path.join(modsDir, "dumps") : "",
            logsDir: fs && path ? path.join(modsDir, "logs") : "",
            mappingsDir: fs && path ? path.join(modsDir, "mappings") : "",
            commandsDir: fs && path ? path.join(modsDir, "commands") : "",
            responsesDir: fs && path ? path.join(modsDir, "responses") : ""
        },
        events: {
            on: on,
            off: off,
            emit: emit
        },
        files: {
            ensureDir: ensureDir,
            resolveModPath: resolveModPath,
            readText: readText,
            writeText: writeText,
            appendText: appendText,
            readJson: readJson,
            writeJson: writeJson,
            writeDump: writeDump
        },
        config: {
            read: readConfig,
            write: writeConfig
        },
        patch: {
            before: function (target, id, callback) { return addFunctionPatch("before", target, id, callback); },
            after: function (target, id, callback) { return addFunctionPatch("after", target, id, callback); },
            replace: function (target, id, callback) { return addFunctionPatch("replace", target, id, callback); },
            wrapFunction: wrapFunction,
            unpatch: unpatch,
            list: listPatches
        },
        watch: {
            value: addWatcher,
            instanceVar: watchInstanceVar,
            globalVar: watchGlobalVar,
            mappedValue: watchMappedValue,
            unwatch: function (id) { delete watchers[id]; }
        },
        commands: {
            register: registerCommand,
            list: listCommands,
            execute: executeCommand,
            process: processCommands
        },
        mappings: {
            registerPack: registerMappingPack,
            get: getMapping,
            list: listMappings,
            getValue: getMappedValue,
            setValue: setMappedValue
        },
        getRuntime: getRuntime,
        listObjects: listObjects,
        getObjectInstances: getObjectInstances,
        describeObject: describeObject,
        dumpRuntimeMap: dumpRuntimeMap,
        setInstanceVar: setInstanceVar,
        getGlobalVar: getGlobalVar,
        setGlobalVar: setGlobalVar,
        setTextObject: setTextObject,
        onRuntimeReady: onRuntimeReady,
        onTick: onTick,
        addHotkey: addHotkey,
        showMessage: showMessage,
        log: log,
        warn: warn,
        error: error,
        registerMod: registerMod,
        getRegisteredMods: function () {
            return registeredMods.slice(0);
        },
        getLogRecords: function () {
            return logRecords.slice(0);
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
                error("Hotkey failed: " + event.code, err);
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
        var loaded;

        if (!enabled || !relativeFile) {
            return;
        }

        fullPath = resolveModPath(relativeFile);
        if (!fullPath) {
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
            loadMappings();
            manifest = readManifest();
            for (i = 0; i < manifest.length; i += 1) {
                try {
                    loadMod(manifest[i]);
                } catch (err) {
                    error("Failed to load mod: " + JSON.stringify(manifest[i]), err);
                }
            }
        } catch (err2) {
            error("Failed to read mods manifest.", err2);
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
            emit("runtime:ready", runtime);
            for (i = 0; i < callbacks.length; i += 1) {
                try {
                    callbacks[i](api);
                } catch (err) {
                    error("Runtime-ready callback failed.", err);
                }
            }
        }

        if (runtime) {
            for (i = 0; i < tickCallbacks.length; i += 1) {
                try {
                    tickCallbacks[i](api);
                } catch (err2) {
                    error("Tick callback failed.", err2);
                }
            }

            processWatchers();
            processCommands();
        }
    }, 250);

    registerCommand("listCommands", function () {
        return listCommands();
    });

    registerCommand("listObjects", function () {
        return listObjects();
    });

    registerCommand("describeObject", function (command) {
        return describeObject(command.object || command.objectName, { sampleLimit: command.sampleLimit || 10 });
    });

    registerCommand("dumpRuntimeMap", function (command) {
        var dump = dumpRuntimeMap({ sampleLimit: command.sampleLimit || 5 });
        dump.patches = listPatches();
        dump.file = writeDump(command.name || "runtime-map", dump);
        return { file: dump.file, objectCount: dump.objectCount, mappings: dump.mappings };
    });

    registerCommand("dumpMappings", function (command) {
        var dump = dumpMappingValues();
        dump.file = writeDump(command.name || "mapping-values", dump);
        return { file: dump.file, mappings: Object.keys(dump.mappings).sort() };
    });

    registerCommand("getMappingValue", function (command) {
        var value = getMappedValue(command.mapping || command.id);
        return {
            mapping: command.mapping || command.id,
            value: typeof value === "undefined" ? null : value
        };
    });

    registerCommand("setMappingValue", function (command) {
        return setMappedValue(command.mapping || command.id, command.value);
    });

    loadMods();
    log("Loader installed. Mods folder: " + modsDir);
}());
__END_MARKER__
""".Replace("__START_MARKER__", LoaderMarkerStart, StringComparison.Ordinal)
           .Replace("__END_MARKER__", LoaderMarkerEnd, StringComparison.Ordinal);
    }

    public static string Manifest(bool includeInspectorTools, bool includeMoneyMod)
    {
        var entries = new List<string>();
        if (includeInspectorTools)
        {
            entries.Add("""
    {
      "file": "_system/inspector.js",
      "enabled": true
    }
""");
        }

        if (includeMoneyMod)
        {
            entries.Add("""
    {
      "file": "money-lock.js",
      "enabled": true
    }
""");
        }

        return "{\n  \"mods\": [\n" + string.Join(",\n", entries) + "\n  ]\n}\n";
    }

    public static string SolCestoMappings()
    {
        return """
{
  "schema": "sol-cesto-modkit-mappings-v1",
  "game": "Sol Cesto",
  "gameVersion": "v100.2",
  "createdBy": "Sol Cesto ModKit",
  "mappings": {
    "money": {
      "id": "money",
      "aliases": ["gold", "or", "currency"],
      "type": "instanceVarSet",
      "description": "Player/menu gold. Update state objects first, then mirror the display text.",
      "targets": [
        {
          "object": "heros",
          "vars": ["or"]
        },
        {
          "object": "metaProgression",
          "vars": ["or", "or_ancien", "orEver"]
        }
      ],
      "displays": [
        {
          "object": "hero_or",
          "setter": "text"
        }
      ],
      "notes": [
        "Changing hero_or alone only changes the visible number.",
        "Purchases read the Construct runtime state on heros/metaProgression."
      ]
    }
  }
}
""";
    }

    public static string InspectorMod()
    {
        return """
module.exports = function (api) {
    "use strict";

    api.registerMod({
        id: "system.inspector",
        name: "Runtime Inspector",
        version: "1.0.0",
        description: "Dumps Construct runtime objects, instance variables, mappings, and active patches."
    });

    function dumpRuntime() {
        var dump = api.dumpRuntimeMap({ sampleLimit: 5 });
        dump.patches = api.patch.list();
        dump.file = api.files.writeDump("runtime-map", dump);
        api.showMessage("Runtime map dumped: " + dump.file);
        api.log("Runtime map dumped to " + dump.file);
        return dump;
    }

    function dumpMappings() {
        var ids = api.mappings.list();
        var dump = {
            createdAt: new Date().toISOString(),
            mappings: {},
            patches: api.patch.list(),
            registeredMods: api.getRegisteredMods()
        };
        var i;

        for (i = 0; i < ids.length; i += 1) {
            dump.mappings[ids[i]] = {
                definition: api.mappings.get(ids[i]),
                value: api.mappings.getValue(ids[i])
            };
        }

        dump.file = api.files.writeDump("mapping-values", dump);
        api.showMessage("Mappings dumped: " + dump.file);
        api.log("Mappings dumped to " + dump.file);
        return dump;
    }

    window.solCestoDumpRuntimeMap = dumpRuntime;
    window.solCestoDumpMappings = dumpMappings;
    window.solCestoDescribeObject = function (objectName) {
        return api.describeObject(objectName, { sampleLimit: 10 });
    };
    window.solCestoListObjects = api.listObjects;
    window.solCestoListMappings = api.mappings.list;

    api.commands.register("dumpRuntime", function (command) {
        return api.commands.execute({
            command: "dumpRuntimeMap",
            name: command.name || "runtime-map",
            sampleLimit: command.sampleLimit || 5
        });
    });

    api.commands.register("dumpMappingValues", function (command) {
        return api.commands.execute({
            command: "dumpMappings",
            name: command.name || "mapping-values"
        });
    });

    function bindHotkeys(codes, callback) {
        var i;
        for (i = 0; i < codes.length; i += 1) {
            api.addHotkey(codes[i], callback);
        }
    }

    bindHotkeys(["F2", "F6"], function (event) {
        event.preventDefault();
        dumpRuntime();
    });

    bindHotkeys(["F3", "F7"], function (event) {
        event.preventDefault();
        dumpMappings();
    });

    bindHotkeys(["F4", "F10"], function (event) {
        event.preventDefault();
        api.showMessage("Objects: " + api.listObjects().length + " | mappings: " + api.mappings.list().join(", "));
    });

    api.onRuntimeReady(function () {
        api.log("Inspector ready. F2 dumps runtime map. F3 dumps mapping values. F4 shows summary. F6/F7/F10 are aliases.");
    });
};
""";
    }

    public static string MoneyMod(int moneyValue, bool lockStartsOn)
    {
        var value = moneyValue.ToString(CultureInfo.InvariantCulture);
        var startsOn = lockStartsOn ? "true" : "false";
        return """
module.exports = function (api) {
    "use strict";

    var config = api.config.read("money-lock", {
        moneyValue: __MONEY_VALUE__,
        lockStartsOn: __LOCK_STARTS_ON__,
        hotkeys: {
            setOnce: "F8",
            toggleLock: "F9"
        }
    });
    var moneyValue = Number(config.moneyValue);
    var lockMoney = !!config.lockStartsOn;

    if (!isFinite(moneyValue)) {
        moneyValue = __MONEY_VALUE__;
    }

    api.registerMod({
        id: "money-lock",
        name: "Money Lock",
        version: "1.1.0",
        description: "Sets and optionally locks Sol Cesto run/menu money through the mapping database."
    });

    function setMoney(value) {
        var result = api.mappings.setValue("money", value);
        return result.changed > 0 || result.displayChanged > 0;
    }

    window.solCestoSetMoney = function (value) {
        var numericValue = Number(value);
        if (!isFinite(numericValue)) {
            numericValue = moneyValue;
        }

        if (setMoney(numericValue)) {
            api.showMessage("Money set to " + numericValue);
            return true;
        }

        api.showMessage("Money target is not available yet");
        return false;
    };

    api.addHotkey(config.hotkeys.setOnce || "F8", function (event) {
        event.preventDefault();
        window.solCestoSetMoney(moneyValue);
    });

    api.addHotkey(config.hotkeys.toggleLock || "F9", function (event) {
        event.preventDefault();
        lockMoney = !lockMoney;
        if (lockMoney) {
            setMoney(moneyValue);
        }
        api.showMessage(lockMoney ? "Money lock on: " + moneyValue : "Money lock off");
    });

    api.watch.mappedValue("money-lock.observe-money", "money", function (change) {
        api.events.emit("money:changed", change);
    });

    api.onRuntimeReady(function () {
        if (lockMoney) {
            setMoney(moneyValue);
            api.showMessage("Money lock on: " + moneyValue);
        }
    });

    api.onTick(function () {
        if (lockMoney) {
            setMoney(moneyValue);
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

    public static string ExampleHookMod()
    {
        return """
module.exports = function (api) {
    "use strict";

    api.registerMod({
        id: "example-hook",
        name: "Example Hook",
        version: "1.0.0"
    });

    api.watch.mappedValue("example-hook.money-watch", "money", function (change) {
        api.log("Money mapping changed from " + change.previous + " to " + change.value);
    });

    api.events.on("money:changed", function (change) {
        api.log("money:changed event observed by example-hook: " + JSON.stringify(change.value));
    });
};
""";
    }

    public static string ModsReadme()
    {
        return """
# Sol Cesto ModKit Mods

This folder is loaded by the Sol Cesto ModKit bootstrap in `package.nw\scripts\main.js`.

## Layout

```text
mods\
  mods.json
  _system\inspector.js
  commands\
  config\
  docs\
  dumps\
  examples\
  logs\
  mappings\sol-cesto-v100.2.json
  responses\
```

## Enable or disable mods

Edit `mods.json`:

```json
{
  "mods": [
    { "file": "_system/inspector.js", "enabled": true },
    { "file": "money-lock.js", "enabled": true }
  ]
}
```

Set `enabled` to `false` to disable a mod without deleting it.

## Built-in inspector

- `F2`: dump runtime objects and instance variables to `dumps`.
- `F3`: dump known mapping values to `dumps`.
- `F4`: show object/mapping summary.

Aliases: `F6`, `F7`, and `F10` are also bound, but `F2/F3/F4` are more reliable in NW.js.

Console helpers:

- `window.solCestoListObjects()`
- `window.solCestoDescribeObject("heros")`
- `window.solCestoDumpRuntimeMap()`
- `window.solCestoDumpMappings()`

Read `docs\MOD_API.md` and `docs\MAPPINGS.md` for the framework API.

## Agent command queue

Create a JSON command file in `commands`. The loader processes it while the game is running and writes a response JSON file to `responses`.

```json
{ "id": "dump-now", "command": "dumpRuntimeMap", "sampleLimit": 5 }
```

Useful commands:

- `listCommands`
- `listObjects`
- `describeObject`
- `dumpRuntimeMap`
- `dumpMappings`
- `getMappingValue`
- `setMappingValue`
""";
    }

    public static string ModApiDocs()
    {
        return """
# Sol Cesto ModKit API

Each mod is a CommonJS module:

```js
module.exports = function (api) {
  api.registerMod({ id: "my-mod", name: "My Mod", version: "1.0.0" });
};
```

## Runtime

- `api.getRuntime()`
- `api.listObjects()`
- `api.getObjectInstances(objectName)`
- `api.describeObject(objectName, { sampleLimit: 5 })`
- `api.dumpRuntimeMap({ sampleLimit: 5 })`
- `api.setInstanceVar(objectName, varName, value)`
- `api.getGlobalVar(name)`
- `api.setGlobalVar(name, value)`
- `api.setTextObject(objectName, value)`

## Lifecycle and UI

- `api.onRuntimeReady(callback)`
- `api.onTick(callback)`
- `api.addHotkey("F10", callback)`
- `api.showMessage(text)`
- `api.log(text)`
- `api.warn(text)`
- `api.error(text, error)`

## Files, config, and dumps

- `api.files.readJson(relativePath, fallback)`
- `api.files.writeJson(relativePath, data)`
- `api.files.readText(relativePath, fallback)`
- `api.files.writeText(relativePath, text)`
- `api.files.writeDump(name, data)`
- `api.config.read(modId, defaults)`
- `api.config.write(modId, config)`

All paths are relative to the `mods` folder and are blocked from escaping it.

## Events

- `api.events.on(name, callback)`
- `api.events.off(name, callback)`
- `api.events.emit(name, payload)`

## Command queue

The loader watches `mods\commands\*.json` and writes responses to `mods\responses`.

```json
{ "id": "dump-now", "command": "dumpRuntimeMap", "sampleLimit": 5 }
```

Built-in commands:

- `listCommands`
- `listObjects`
- `describeObject`
- `dumpRuntimeMap`
- `dumpMappings`
- `getMappingValue`
- `setMappingValue`

Mods can register their own:

```js
api.commands.register("myCommand", function (command) {
  return { ok: true, payload: command };
});
```

## Harmony-like JS patching

Patch a function path:

```js
api.patch.before("window.someFunction", "my-mod.before", function (state) {
  // state.args can be changed.
});

api.patch.after("window.someFunction", "my-mod.after", function (state) {
  // return a value to replace state.result.
});

api.patch.replace("window.someFunction", "my-mod.replace", function (original, state) {
  return original.apply(this, state.args);
});
```

Patch management:

- `api.patch.unpatch(id)`
- `api.patch.list()`

## Watchers

```js
api.watch.instanceVar("watch-money", "metaProgression", "or", function (change) {
  api.log(JSON.stringify(change.value));
});

api.watch.mappedValue("watch-mapped-money", "money", function (change) {
  api.log("money changed");
});
```

Watcher helpers:

- `api.watch.value(id, reader, callback, options)`
- `api.watch.instanceVar(id, objectName, varName, callback, options)`
- `api.watch.globalVar(id, name, callback, options)`
- `api.watch.mappedValue(id, mappingId, callback, options)`
- `api.watch.unwatch(id)`
""";
    }

    public static string MappingsDocs()
    {
        return """
# Sol Cesto ModKit Mappings

Mappings are stored in `mappings\*.json`. They are the Sol Cesto equivalent of known classes/fields in a Unity/BepInEx workflow.

Known v100.2 mapping:

```json
{
  "money": {
    "aliases": ["gold", "or", "currency"],
    "targets": [
      { "object": "heros", "vars": ["or"] },
      { "object": "metaProgression", "vars": ["or", "or_ancien", "orEver"] }
    ],
    "displays": [
      { "object": "hero_or", "setter": "text" }
    ]
  }
}
```

Use mappings in mods:

```js
api.mappings.setValue("money", 999);
var current = api.mappings.getValue("money");
```

Discovery workflow:

1. Press `F6` in game to create a runtime dump in `dumps`.
2. Search the dump for likely object names and instance variables.
3. Test changes through `api.setInstanceVar` or a temporary mod.
4. Add proven targets to a mapping JSON file.
5. Build future mods against `api.mappings.*` instead of raw object names.
""";
    }

    public static void WriteUtf8NoBom(string path, string text)
    {
        File.WriteAllText(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
