// Reading MelonLoader's Latest.log: one line at a time for the log sheet, and the whole run for "where is the game at".

/** Where a running game is, read off its log. */
export type GameStage = "starting" | "preparing" | "loading" | "menu" | "connecting" | "joined" | "closing";

export const stageLabels: Record<GameStage, string> = {
  starting: "Starting Schedule I…",
  preparing: "Preparing the game…",
  loading: "Loading mods…",
  menu: "At the main menu",
  connecting: "Connecting to the server…",
  joined: "On the server",
  closing: "Closing the game…",
};

export type LogLevel = "info" | "warn" | "error";

export type LogLine = {
  /** Unique within a launcher session, so the log sheet keys rows by it. */
  id: number;
  raw: string;
  time: string | null;
  /** The [Mod] tag, when the line has one. */
  source: string | null;
  text: string;
  level: LogLevel;
  /** MelonLoader's "------" separators. */
  rule: boolean;
};

// "[02:35:19.521] [DedicatedServerClient] Target server updated" → time, source, text
const LINE = /^\[(\d\d:\d\d:\d\d)\.\d+\]\s?(?:\[([^\]]+)\]\s?)?(.*)$/;

/** Parsed once, when the line arrives. */
export function parseLine(raw: string, id: number): LogLine {
  const match = LINE.exec(raw);
  const [time, source, text] = match ? [match[1], match[2] ?? null, match[3]] : [null, null, raw];
  // Only tagged lines count: an exception's stack and "---> inner exception" lines belong to the [ERROR] above them.
  const level: LogLevel = /\[(error|fatal)\]/i.test(raw) ? "error" : /\[warn(ing)?\]/i.test(raw) ? "warn" : "info";
  return { id, raw, time, source, text, level, rule: /^-{5,}$/.test(text.trim()) };
}

/** The newest line worth showing in one line of the status: no separators, no blank text. */
export function latestMessage(lines: LogLine[]): string | null {
  for (let i = lines.length - 1; i >= 0; i--) {
    const line = lines[i];
    if (!line.rule && line.text.trim()) return line.source ? `[${line.source}] ${line.text}` : line.text;
  }
  return null;
}

// Newest marker wins, so the log is read from the end. The client-mod strings come from DedicatedServerClient 1.1.6.
const MARKERS: [RegExp, GameStage][] = [
  [/Notifying client mods of shutdown/, "closing"],
  [/Disconnecting from dedicated server/, "menu"],
  [/Main scene loaded in dedicated server mode|Quest system initialization completed/, "joined"],
  [/Starting dedicated server connection/, "connecting"],
  [/mod initialized successfully|AssetBundle loaded/, "menu"],
  [/^\[[\d:.]+\] Loading Mods\.\.\./, "loading"],
  [/No Generation Needed/, "loading"],
  [/\[Il2CppAssemblyGenerator\]/, "preparing"],
];

/** The stage the newest marker in these lines points to, or null when none of them is a marker. Fed new lines as
 * they arrive, so a long session never loses its stage when old lines scroll out of the kept window. */
export function latestStage(lines: LogLine[]): GameStage | null {
  for (let i = lines.length - 1; i >= 0; i--) {
    for (const [pattern, stage] of MARKERS) {
      if (pattern.test(lines[i].raw)) return stage;
    }
  }
  return null;
}

// "C:\Program Files (x86)\Steam\steamapps\common\Schedule I" → "C:\…\common\Schedule I"
export function shortPath(path: string) {
  const parts = path.split("\\");
  return parts.length > 4 ? `${parts[0]}\\…\\${parts.slice(-2).join("\\")}` : path;
}
