// Prints a UI subtree of the game's exported prefabs or scenes (.gameref/assets, from scripts/game-assets.ps1) as a
// readable tree: every object with its rect, and its images, texts, fonts, colors, layout groups and game scripts. For
// designing against the real layout instead of screenshots.
//
//   node scripts/prefab-tree.mjs GameObject/Player.prefab Phone            # the whole phone
//   node scripts/prefab-tree.mjs GameObject/Player.prefab Phone/HomeScreen --depth 3
//   node scripts/prefab-tree.mjs Scenes/Main.unity HUD --all              # --all: every field of game scripts
//
// The file is relative to the newest export's ExportedProject/Assets (or give a full path). The object is a name, or
// a path of names from any ancestor ("Phone/HomeScreen/AppIcons"); the first match wins.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const args = process.argv.slice(2);
const flag = (name) => { const i = args.indexOf(name); if (i < 0) return null; const v = args[i + 1]; args.splice(i, 2); return v; };
const all = args.includes("--all") ? (args.splice(args.indexOf("--all"), 1), true) : false;
const maxDepth = Number(flag("--depth") ?? 99);
const [fileArg, target] = args;
if (!fileArg || !target) {
  console.error("usage: node scripts/prefab-tree.mjs <Assets-relative .prefab|.unity> <name or a/b/c> [--depth N] [--all]");
  process.exit(1);
}

const exportsDir = path.join(root, ".gameref", "assets");
const builds = fs.existsSync(exportsDir) ? fs.readdirSync(exportsDir).filter((d) => /^build-\d+$/.test(d)).sort() : [];
if (!builds.length) { console.error("No export in .gameref/assets. Run: pwsh scripts/game-assets.ps1 export"); process.exit(1); }
const assets = path.join(exportsDir, builds.at(-1), "ExportedProject", "Assets");
const file = path.isAbsolute(fileArg) ? fileArg : path.join(assets, fileArg);
if (!fs.existsSync(file)) { console.error(`No such file: ${file}`); process.exit(1); }

// --- Unity YAML: one document per object, "--- !u!<class> &<fileID>". A small parser for the subset Unity writes.
function parseFlow(s) {
  s = s.trim();
  if (!s.startsWith("{")) return s;
  const out = {};
  for (const part of s.slice(1, -1).split(/,\s*/)) {
    const i = part.indexOf(":");
    if (i > 0) out[part.slice(0, i).trim()] = part.slice(i + 1).trim();
  }
  return out;
}
function scalar(v) {
  v = v.trim();
  if (v.startsWith("{")) return parseFlow(v);
  if ((v.startsWith("'") && v.endsWith("'")) || (v.startsWith('"') && v.endsWith('"'))) return v.slice(1, -1).replace(/''/g, "'").replace(/\\n/g, "\n");
  return v;
}
function parseBlock(lines, start, indent) {
  let obj = null, i = start;
  while (i < lines.length) {
    const line = lines[i];
    const ind = line.search(/\S/);
    if (ind < indent || ind < 0) { if (ind < 0) { i++; continue; } break; }
    const t = line.slice(ind);
    if (t.startsWith("- ")) {
      obj ??= [];
      const rest = t.slice(2);
      const m = /^([^:{'"]+):\s?(.*)$/.exec(rest);
      if (m) {
        // A map item: its first key is on the dash line, the rest are indented two more.
        const item = {};
        let j = i + 1;
        if (m[2] === "") { const [v, n] = parseBlock(lines, j, ind + 4); item[m[1]] = v ?? ""; j = n; }
        else item[m[1]] = scalar(m[2]);
        const [more, n] = parseBlock(lines, j, ind + 2);
        if (more && !Array.isArray(more)) Object.assign(item, more);
        obj.push(item);
        i = n;
      } else { obj.push(scalar(rest)); i++; }
      continue;
    }
    // Unity writes a list at its key's own indent, so the list ends at the next plain key.
    if (Array.isArray(obj)) break;
    const m = /^([^:]+):\s?(.*)$/.exec(t);
    if (!m) { i++; continue; }
    obj ??= {};
    let val = m[2];
    if (val === "") {
      const next = lines[i + 1] ?? "";
      const nind = next.search(/\S/);
      if (nind > ind || (nind === ind && next.slice(nind).startsWith("- "))) {
        const [v, n] = parseBlock(lines, i + 1, nind);
        obj[m[1]] = v; i = n; continue;
      }
      obj[m[1]] = ""; i++; continue;
    }
    // Quoted or plain values can wrap onto deeper-indented lines.
    let j = i + 1;
    while (j < lines.length && lines[j].search(/\S/) > ind && !/^\s*[^:\s]+:\s/.test(lines[j]) && !/^\s*- /.test(lines[j])) {
      val += lines[j].trim() === "" ? "\n" : " " + lines[j].trim(); j++;
    }
    obj[m[1]] = scalar(val); i = j;
  }
  return [obj, i];
}

const docs = new Map();
{
  const text = fs.readFileSync(file, "utf8");
  const parts = text.split(/^--- !u!(\d+) &(-?\d+)(?: stripped)?\r?$/m);
  for (let k = 1; k < parts.length; k += 3) {
    const lines = parts[k + 2].split(/\r?\n/);
    const type = (lines.find((l) => /^\S/.test(l)) ?? "").replace(":", "");
    const [body] = parseBlock(lines, lines.findIndex((l) => /^\S/.test(l)) + 1, 2);
    docs.set(parts[k + 1], { cls: parts[k], type, body: body ?? {} });
  }
}

// guid -> asset name, from every .meta in the export (sprites, textures, fonts, scripts, clips).
const guids = new Map();
(function walk(dir) {
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, e.name);
    if (e.isDirectory()) walk(p);
    else if (e.name.endsWith(".meta")) {
      const m = /^guid: ([0-9a-f]{32})/m.exec(fs.readFileSync(p, "utf8"));
      if (m) guids.set(m[1], path.relative(assets, p.slice(0, -5)).replace(/\\/g, "/"));
    }
  }
})(assets);

const ref = (r) => {
  if (!r || typeof r !== "object" || !r.fileID || r.fileID === "0") return null;
  if (r.guid) {
    const g = guids.get(r.guid);
    return g ? path.basename(g).replace(/\.(asset|png|ttf|otf|mat|anim|prefab)$/i, "") : (r.guid.startsWith("0000000000000000") ? "builtin" : "?" + r.guid.slice(0, 8));
  }
  const d = docs.get(r.fileID);
  if (!d) return "#" + r.fileID;
  const go = d.type === "GameObject" ? d : docs.get(d.body.m_GameObject?.fileID);
  return (go?.body.m_Name ?? "?") + (d.type !== "GameObject" ? "." + d.type : "");
};
const hex = (c) => {
  if (!c || typeof c !== "object") return "";
  const h = (x) => Math.round(Math.min(1, Math.max(0, Number(x))) * 255).toString(16).padStart(2, "0").toUpperCase();
  return "#" + h(c.r) + h(c.g) + h(c.b) + (Number(c.a) < 1 ? h(c.a) : "");
};
const v2 = (o) => (o && typeof o === "object" ? `${+(+o.x).toFixed(2)},${+(+o.y).toFixed(2)}` : "");
const ANCHOR = ["upper-left", "upper-center", "upper-right", "middle-left", "middle-center", "middle-right", "lower-left", "lower-center", "lower-right"];
const IMAGE_TYPE = ["simple", "sliced", "tiled", "filled"];

// Built-in UI components are recognized by their fields; game scripts by their .cs name.
function describe(d) {
  const b = d.body;
  if (d.type === "Canvas") return `Canvas mode=${["overlay", "camera", "world"][b.m_RenderMode] ?? b.m_RenderMode} order=${b.m_SortingOrder}${b.m_OverrideSorting === "1" ? " override" : ""}`;
  if (d.type === "CanvasGroup") return `CanvasGroup alpha=${b.m_Alpha} interactable=${b.m_Interactable} blocks=${b.m_BlocksRaycasts}`;
  if (d.type === "Animation") return `Animation clips=[${(b.m_Animations ?? []).map(ref).join(", ")}] auto=${b.m_PlayAutomatically}`;
  if (d.type === "Animator") return `Animator controller=${ref(b.m_Controller)}`;
  if (d.type === "AudioSource") return `AudioSource clip=${ref(b.m_audioClip)} vol=${b.m_Volume} mixer=${ref(b.OutputAudioMixerGroup)}`;
  if (d.type !== "MonoBehaviour") return d.type;
  const off = b.m_Enabled === "0" ? " (disabled)" : "";
  const script = guids.get(b.m_Script?.guid);
  if (b.m_FontData) {
    const f = b.m_FontData;
    return `Text${off} "${b.m_Text}" font=${ref(f.m_Font)} size=${f.m_FontSize}${f.m_BestFit === "1" ? ` fit=${f.m_MinSize}-${f.m_MaxSize}` : ""} style=${["normal", "bold", "italic", "bold-italic"][f.m_FontStyle]} align=${ANCHOR[f.m_Alignment]} color=${hex(b.m_Color)}${f.m_LineSpacing !== "1" ? ` line=${f.m_LineSpacing}` : ""}`;
  }
  if ("m_fontAsset" in b) return `TMP${off} "${b.m_text}" font=${ref(b.m_fontAsset)} size=${b.m_fontSize} color=${hex(b.m_fontColor)} align=${b.m_HorizontalAlignment}/${b.m_VerticalAlignment}`;
  if ("m_Sprite" in b && "m_Type" in b) return `Image${off} sprite=${ref(b.m_Sprite) ?? "none"} color=${hex(b.m_Color)} type=${IMAGE_TYPE[b.m_Type]}${b.m_PreserveAspect === "1" ? " keep-aspect" : ""}${b.m_RaycastTarget === "0" ? " no-raycast" : ""}${ref(b.m_Material) ? " material=" + ref(b.m_Material) : ""}`;
  if ("m_Texture" in b && "m_UVRect" in b) return `RawImage${off} texture=${ref(b.m_Texture)} color=${hex(b.m_Color)}`;
  if ("m_CellSize" in b) return `GridLayout cell=${v2(b.m_CellSize)} spacing=${v2(b.m_Spacing)} pad=${JSON.stringify(b.m_Padding)} constraint=${["flexible", "columns", "rows"][b.m_Constraint]}:${b.m_ConstraintCount} align=${ANCHOR[b.m_ChildAlignment]}`;
  if ("m_ChildForceExpandWidth" in b) return `LayoutGroup${off} spacing=${b.m_Spacing} pad=${JSON.stringify(b.m_Padding)} align=${ANCHOR[b.m_ChildAlignment]} controlW/H=${b.m_ChildControlWidth}/${b.m_ChildControlHeight} expandW/H=${b.m_ChildForceExpandWidth}/${b.m_ChildForceExpandHeight}${script ? "" : ""}`;
  if ("m_HorizontalFit" in b) return `ContentSizeFitter h=${b.m_HorizontalFit} v=${b.m_VerticalFit}`;
  if ("m_IgnoreLayout" in b) return `LayoutElement min=${b.m_MinWidth}x${b.m_MinHeight} pref=${b.m_PreferredWidth}x${b.m_PreferredHeight} flex=${b.m_FlexibleWidth}x${b.m_FlexibleHeight}`;
  if ("m_BlockingObjects" in b) return "GraphicRaycaster";
  if ("m_UiScaleMode" in b) return `CanvasScaler mode=${b.m_UiScaleMode} ref=${v2(b.m_ReferenceResolution)} match=${b.m_MatchWidthOrHeight} ppu=${b.m_ReferencePixelsPerUnit}`;
  if ("m_ShowMaskGraphic" in b) return `Mask show=${b.m_ShowMaskGraphic}`;
  if ("m_Softness" in b || ("m_Padding" in b && Object.keys(b).length < 14 && !script)) return `RectMask2D`;
  if ("m_TargetGraphic" in b && "m_Transition" in b) {
    const kind = "m_IsOn" in b ? "Toggle" : "m_MinValue" in b ? "Slider" : "m_Content" in b ? "ScrollRect" : "Button";
    const cs = b.m_Colors ?? {};
    return `${kind}${off} target=${ref(b.m_TargetGraphic)} transition=${["none", "color", "sprite", "animation"][b.m_Transition]} normal=${hex(cs.m_NormalColor)} hover=${hex(cs.m_HighlightedColor)} press=${hex(cs.m_PressedColor)} select=${hex(cs.m_SelectedColor)} fade=${cs.m_FadeDuration}`;
  }
  const name = script ? path.basename(script, ".cs") : "MonoBehaviour?" + (b.m_Script?.fileID ?? "");
  const skip = new Set(["m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"]);
  const fields = Object.entries(b).filter(([k]) => !skip.has(k)).map(([k, v]) => {
    if (v && typeof v === "object" && !Array.isArray(v) && "fileID" in v) return `${k}=${ref(v) ?? "none"}`;
    if (Array.isArray(v)) return `${k}=[${v.map((x) => (x && typeof x === "object" && "fileID" in x ? ref(x) : typeof x === "object" ? "{…}" : x)).join(", ")}]`;
    if (v && typeof v === "object") return all ? `${k}=${JSON.stringify(v)}` : null;
    return `${k}=${v}`;
  }).filter(Boolean);
  return `${name}${off}${fields.length ? " " + (all ? fields : fields.slice(0, 12)).join(" ") + (!all && fields.length > 12 ? " …" : "") : ""}`;
}

// Transform tree: each GameObject's RectTransform/Transform lists its children's transforms.
const goOf = (tf) => docs.get(tf.body.m_GameObject?.fileID);
const transformOf = (go) => (go.body.m_Component ?? []).map((c) => docs.get(c.component?.fileID)).find((d) => d && (d.type === "RectTransform" || d.type === "Transform"));
const gameObjects = [...docs.values()].filter((d) => d.type === "GameObject");
const pathNames = target.split("/");
function matches(go) {
  // Walk up the parents to check the requested path ends at this object.
  let tf = transformOf(go);
  for (let k = pathNames.length - 1; k >= 0; k--) {
    if (!tf || goOf(tf)?.body.m_Name !== pathNames[k]) return false;
    tf = docs.get(tf.body.m_Father?.fileID);
  }
  return true;
}
const start = gameObjects.find(matches);
if (!start) { console.error(`No object "${target}" in ${path.relative(assets, file)}`); process.exit(1); }

function rect(tf) {
  const b = tf.body;
  if (tf.type !== "RectTransform") return "";
  const amin = v2(b.m_AnchorMin), amax = v2(b.m_AnchorMax);
  const anchor = amin === amax ? `anchor=${amin}` : `anchors=${amin}..${amax}`;
  const scale = b.m_LocalScale && (b.m_LocalScale.x !== "1" || b.m_LocalScale.y !== "1") ? ` scale=${+(+b.m_LocalScale.x).toPrecision(3)},${+(+b.m_LocalScale.y).toPrecision(3)}` : "";
  const rot = b.m_LocalRotation && b.m_LocalRotation.z !== "0" && b.m_LocalRotation.z !== "-0" ? ` rotZ=${(2 * Math.asin(Number(b.m_LocalRotation.z)) * 180 / Math.PI).toFixed(0)}°` : "";
  return `  [size=${v2(b.m_SizeDelta)} pos=${v2(b.m_AnchoredPosition)} ${anchor} pivot=${v2(b.m_Pivot)}${scale}${rot}]`;
}
function print(go, depth) {
  const tf = transformOf(go);
  const pad = "  ".repeat(depth);
  console.log(`${pad}${go.body.m_Name}${go.body.m_IsActive === "0" ? " (inactive)" : ""}${tf ? rect(tf) : ""}`);
  for (const c of go.body.m_Component ?? []) {
    const d = docs.get(c.component?.fileID);
    if (!d || d.type === "RectTransform" || d.type === "Transform" || d.type === "CanvasRenderer") continue;
    console.log(`${pad}  · ${describe(d)}`);
  }
  if (depth >= maxDepth || !tf) return;
  for (const ch of tf.body.m_Children ?? []) {
    const ctf = docs.get(ch.fileID ?? ch);
    const cgo = ctf && goOf(ctf);
    if (cgo) print(cgo, depth + 1);
  }
}
console.log(`${path.relative(assets, file).replace(/\\/g, "/")} (${builds.at(-1)})`);
print(start, 0);
