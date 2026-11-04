using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.UI;
using Il2CppScheduleOne.UI.Phone;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[assembly: MelonInfo(typeof(HylandDriver.DriverMod), "Hyland Driver", "0.1.0", "Blazzer")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonPlatformDomain(MelonPlatformDomainAttribute.CompatibleDomains.IL2CPP)]
[assembly: System.Reflection.AssemblyVersion("0.1.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.0.0")]

namespace HylandDriver;

/// <summary>
/// Test copy only, never shipped. Runs commands from <c>UserData/Driver/cmd.txt</c> (first line: a batch id, then one
/// command per line) and writes what happened to <c>out.txt</c>, so Claude Code can work the game while Blazzer plays
/// something else: no real mouse, keyboard or focus involved. <c>scripts/dev.ps1 drive</c> is the front end; README.md
/// lists the commands. It also keeps the test copy cheap in the background: windowed, behind other windows, muted,
/// capped at 20 fps. All of that is runtime only: it never writes the game's settings file.
/// </summary>
public sealed class DriverMod : MelonMod
{
	private const int Fps = 20;

	private string cmdFile = "";
	private string outFile = "";
	private string shots = "";
	private string resFile = "";
	private float nextPoll;
	private float nextEnforce;
	private bool busy;
	private int width = 1600;
	private int height = 900;
	private bool windowPlaced;

	public override void OnInitializeMelon()
	{
		string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "Driver");
		shots = Path.Combine(dir, "shots");
		Directory.CreateDirectory(shots);
		cmdFile = Path.Combine(dir, "cmd.txt");
		outFile = Path.Combine(dir, "out.txt");
		resFile = Path.Combine(dir, "res.txt");
		// The size a `res` command chose outlives reloads and restarts (HotReload re-runs this on every push).
		if (File.Exists(resFile))
		{
			string[] parts = File.ReadAllText(resFile).Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h))
			{
				width = w;
				height = h;
			}
		}
		// A batch from before a restart would run against a game that isn't where its sender thought it was.
		if (File.Exists(cmdFile))
		{
			File.Move(cmdFile, cmdFile + ".stale", overwrite: true);
		}
		LoggerInstance.Msg($"Hyland Driver 0.1.0: commands from {cmdFile}");
	}

	public override void OnUpdate()
	{
		float now = Time.unscaledTime;
		if (now >= nextEnforce)
		{
			nextEnforce = now + 2f;
			Enforce();
		}
		if (!busy && now >= nextPoll)
		{
			nextPoll = now + 0.2f;
			if (File.Exists(cmdFile))
			{
				Begin();
			}
		}
	}

	/// <summary>The game re-applies its own display settings on start, so this wins by repeating, not by going first.</summary>
	private void Enforce()
	{
		Application.runInBackground = true;
		AudioListener.volume = 0f;
		if (QualitySettings.vSyncCount != 0)
		{
			QualitySettings.vSyncCount = 0;
		}
		if (Application.targetFrameRate != Fps)
		{
			Application.targetFrameRate = Fps;
		}
		if (Screen.fullScreenMode != FullScreenMode.Windowed || Screen.width != width || Screen.height != height)
		{
			Screen.SetResolution(width, height, FullScreenMode.Windowed);
		}
		if (!windowPlaced)
		{
			windowPlaced = Window.Place("back", width, height) != null;
		}
	}

	private void Begin()
	{
		string[] lines;
		try
		{
			lines = File.ReadAllLines(cmdFile);
			File.Delete(cmdFile);
		}
		catch (IOException)
		{
			return; // still being written; the next poll gets it
		}
		busy = true;
		MelonCoroutines.Start(Run(lines));
	}

	private IEnumerator Run(string[] lines)
	{
		StringBuilder o = new StringBuilder();
		o.AppendLine(lines.Length > 0 ? lines[0] : "?");
		for (int i = 1; i < lines.Length; i++)
		{
			string line = lines[i].Trim();
			if (line.Length == 0)
			{
				continue;
			}
			o.Append("> ").AppendLine(line);
			Result r = new Result();
			IEnumerator? step = null;
			try
			{
				step = Command(line, r);
			}
			catch (Exception ex)
			{
				r.Fail(ex);
			}
			// Stepped by hand so a throw inside a command is caught (no yield inside try/catch in C#), with nested
			// enumerators (a command's Wait) run here rather than trusting the coroutine scheduler with managed ones.
			Stack<IEnumerator> stack = new Stack<IEnumerator>();
			if (step != null)
			{
				stack.Push(step);
			}
			while (stack.Count > 0)
			{
				object? current;
				try
				{
					if (!stack.Peek().MoveNext())
					{
						stack.Pop();
						continue;
					}
					current = stack.Peek().Current;
				}
				catch (Exception ex)
				{
					r.Fail(ex);
					break;
				}
				if (current is IEnumerator nested)
				{
					stack.Push(nested);
					continue;
				}
				yield return current;
			}
			o.Append(r.Text);
			if (r.Failed)
			{
				o.AppendLine("STOPPED: the rest of the batch didn't run.");
				LoggerInstance.Warning($"Driver: '{line}' failed: {r.Text.ToString().Trim()}");
				break;
			}
			yield return null;
		}
		string tmp = outFile + ".tmp";
		File.WriteAllText(tmp, o.ToString());
		File.Move(tmp, outFile, overwrite: true);
		busy = false;
	}

	private sealed class Result
	{
		internal readonly StringBuilder Text = new StringBuilder();
		internal bool Failed;

		internal void Line(string s) => Text.Append("  ").AppendLine(s);

		internal void Fail(string s)
		{
			Failed = true;
			Line("ERROR: " + s);
		}

		internal void Fail(Exception ex) => Fail($"{ex.GetType().Name}: {ex.Message}");
	}

	private IEnumerator Command(string line, Result r)
	{
		int space = line.IndexOf(' ');
		string verb = (space < 0 ? line : line.Substring(0, space)).ToLowerInvariant();
		string rest = space < 0 ? "" : line.Substring(space + 1).Trim();
		switch (verb)
		{
			case "phone":
				return Phone(rest, r);
			case "back":
				return Back(r);
			case "find":
				return Find(rest, r);
			case "click":
				return Click(rest, r);
			case "scroll":
				return Scroll(rest, r);
			case "type":
				return Type(rest, r);
			case "tree":
				return Tree(rest, r);
			case "shot":
				return Shot(rest, r);
			case "wait":
				return Wait(float.Parse(rest, CultureInfo.InvariantCulture), r);
			case "res":
				return Res(rest, r);
			case "window":
				return WindowCmd(rest, r);
			case "state":
				return State(r);
			case "ui":
				return Popup(rest, r);
			default:
				r.Fail($"unknown command '{verb}'. phone, back, find, click, scroll, type, tree, shot, wait, res, window, state, ui");
				return Nothing();
		}
	}

	private static IEnumerator Nothing()
	{
		yield break;
	}

	private static IEnumerator Wait(float seconds, Result r)
	{
		float until = Time.realtimeSinceStartup + seconds;
		while (Time.realtimeSinceStartup < until)
		{
			yield return null;
		}
	}

	// ---- phone ---------------------------------------------------------------------------------------------------

	/// <summary>The phone key's own path: GameplayMenu on its Phone screen, then Open/Close.</summary>
	private static IEnumerator Phone(string arg, Result r)
	{
		GameplayMenu menu = Singleton<GameplayMenu>.Instance;
		if (menu == null)
		{
			r.Fail("no GameplayMenu: not in a world yet?");
			yield break;
		}
		switch (arg)
		{
			case "open":
				if (menu.CurrentScreen != GameplayMenu.EGameplayScreen.Phone)
				{
					menu.SetScreen(GameplayMenu.EGameplayScreen.Phone);
				}
				if (!menu.IsOpen)
				{
					menu.Open();
				}
				break;
			case "close":
				if (menu.IsOpen)
				{
					menu.Close();
				}
				break;
			default:
				r.Fail("phone open | phone close");
				yield break;
		}
		yield return Wait(0.6f, r);
		r.Line($"phone {(PlayerSingleton<Il2CppScheduleOne.UI.Phone.Phone>.Instance.IsOpen ? "open" : "closed")}, menu {(menu.IsOpen ? "open" : "closed")}");
	}

	/// <summary>Escape, as the game hands it out: newest exit listener first, until one uses it.</summary>
	private static IEnumerator Back(Result r)
	{
		var listeners = GameInput.exitListeners;
		ExitAction action = new ExitAction(ExitType.Primary);
		string? by = null;
		for (int i = listeners.Count - 1; i >= 0; i--)
		{
			listeners[i].listenerFunction.Invoke(action);
			if (action.Used)
			{
				by = listeners[i].listenerFunction.Method?.Name;
				break;
			}
		}
		r.Line(action.Used ? $"escape used ({by})" : "escape: nobody used it");
		yield return Wait(0.4f, r);
	}

	// ---- finding UI ----------------------------------------------------------------------------------------------

	private sealed class Hit
	{
		internal RectTransform Rect = null!;
		internal string Path = "";
		internal string? Text;
		internal int Score;
	}

	/// <summary>
	/// Active UI objects matching a query: "/A/B" matches the end of a hierarchy path, anything else an object's name or
	/// its text (exact beats contains, case ignored). A trailing " #n" picks the n-th hit (1-based).
	/// </summary>
	private static List<Hit> Search(string query, out int? pick)
	{
		pick = null;
		int hash = query.LastIndexOf(" #", StringComparison.Ordinal);
		if (hash >= 0 && int.TryParse(query.Substring(hash + 2), out int n))
		{
			pick = n;
			query = query.Substring(0, hash).Trim();
		}
		query = query.Trim('"');
		bool byPath = query.StartsWith("/", StringComparison.Ordinal);
		List<Hit> hits = new List<Hit>();
		foreach (RectTransform rt in Object.FindObjectsOfType<RectTransform>())
		{
			if (rt == null || !rt.gameObject.activeInHierarchy)
			{
				continue;
			}
			string? text = TextOf(rt);
			int score = 0;
			if (byPath)
			{
				if (PathOf(rt).EndsWith(query, StringComparison.OrdinalIgnoreCase))
				{
					score = 2;
				}
			}
			else if (text != null && string.Equals(text.Trim(), query, StringComparison.OrdinalIgnoreCase))
			{
				score = 3; // what's written on screen beats an object's name ("Messages": the icon's label, not the app root)
			}
			else if (string.Equals(rt.name, query, StringComparison.OrdinalIgnoreCase))
			{
				score = 2;
			}
			else if (rt.name.Contains(query, StringComparison.OrdinalIgnoreCase) || (text != null && text.Contains(query, StringComparison.OrdinalIgnoreCase)))
			{
				score = 1;
			}
			if (score > 0)
			{
				hits.Add(new Hit { Rect = rt, Path = PathOf(rt), Text = text, Score = score });
			}
		}
		return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Path, StringComparer.Ordinal).ToList();
	}

	/// <summary>One hit, or a failure that lists the candidates so the next command can name one.</summary>
	private static Hit? One(string query, Result r, Func<Hit, bool>? usable = null)
	{
		List<Hit> hits = Search(query, out int? pick);
		if (usable != null)
		{
			hits = hits.Where(usable).ToList();
		}
		if (hits.Count == 0)
		{
			r.Fail($"nothing active matches '{query}'");
			return null;
		}
		if (pick != null)
		{
			if (pick < 1 || pick > hits.Count)
			{
				r.Fail($"#{pick} is out of range: {hits.Count} hits");
				return null;
			}
			return hits[pick.Value - 1];
		}
		List<Hit> best = hits.Where(h => h.Score == hits[0].Score).ToList();
		if (best.Count > 1)
		{
			r.Fail($"'{query}' is ambiguous ({best.Count} hits); add #n:");
			for (int i = 0; i < Math.Min(best.Count, 15); i++)
			{
				r.Line($"#{i + 1} {Describe(best[i])}");
			}
			return null;
		}
		return best[0];
	}

	private static IEnumerator Find(string query, Result r)
	{
		List<Hit> hits = Search(query, out _);
		r.Line($"{hits.Count} hits");
		for (int i = 0; i < Math.Min(hits.Count, 40); i++)
		{
			r.Line($"#{i + 1} {Describe(hits[i])}");
		}
		yield break;
	}

	private static string Describe(Hit h)
	{
		Rect s = ScreenRect(h.Rect);
		string text = h.Text == null ? "" : $" \"{Short(h.Text)}\"";
		return $"{h.Path}{text} screen {s.x:0},{s.y:0} {s.width:0}x{s.height:0}{(Clickable(h.Rect) != null ? " [clickable]" : "")}";
	}

	private static string? TextOf(Component c)
	{
		TMP_Text tmp = c.GetComponent<TMP_Text>();
		if (tmp != null)
		{
			return tmp.text;
		}
		Text text = c.GetComponent<Text>();
		return text != null ? text.text : null;
	}

	private static string PathOf(Transform t)
	{
		StringBuilder sb = new StringBuilder();
		for (Transform? p = t; p != null; p = p.parent)
		{
			sb.Insert(0, "/" + p.name);
		}
		return sb.ToString();
	}

	private static string Short(string s)
	{
		s = s.Replace('\n', ' ').Trim();
		return s.Length > 50 ? s.Substring(0, 50) + "…" : s;
	}

	/// <summary>The object's rect in screen pixels (origin bottom left), through its canvas's camera.</summary>
	private static Rect ScreenRect(RectTransform rt)
	{
		Il2CppStructArray<Vector3> corners = new Il2CppStructArray<Vector3>(4);
		rt.GetWorldCorners(corners);
		Canvas? canvas = null;
		// GetComponentInParent<T>(true) throws under Il2CppInterop; walk up by hand.
		for (Transform? p = rt; p != null; p = p.parent)
		{
			Canvas c = p.GetComponent<Canvas>();
			if (c != null)
			{
				canvas = c.rootCanvas;
			}
		}
		Camera? cam = null;
		if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
		{
			cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
		}
		float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
		for (int i = 0; i < 4; i++)
		{
			Vector2 s = RectTransformUtility.WorldToScreenPoint(cam, corners[i]);
			xMin = Math.Min(xMin, s.x);
			yMin = Math.Min(yMin, s.y);
			xMax = Math.Max(xMax, s.x);
			yMax = Math.Max(yMax, s.y);
		}
		return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
	}

	// ---- acting on UI --------------------------------------------------------------------------------------------

	/// <summary>The first object at or above <paramref name="t"/> that takes a click, as the EventSystem would pick it.</summary>
	private static GameObject? Clickable(Transform t)
	{
		try
		{
			return ExecuteEvents.GetEventHandler<IPointerClickHandler>(t.gameObject);
		}
		catch
		{
			for (Transform? p = t; p != null; p = p.parent)
			{
				if (p.GetComponent<Selectable>() != null || p.GetComponent<EventTrigger>() != null)
				{
					return p.gameObject;
				}
			}
			return null;
		}
	}

	private static PointerEventData Pointer(RectTransform rt)
	{
		PointerEventData data = new PointerEventData(EventSystem.current);
		data.button = PointerEventData.InputButton.Left;
		data.position = ScreenRect(rt).center;
		data.clickCount = 1;
		return data;
	}

	/// <summary>The topmost UI object at a screen point, as the game's EventSystem would hit it with the mouse there.</summary>
	private static GameObject? RaycastTop(Vector2 point)
	{
		EventSystem es = EventSystem.current;
		if (es == null)
		{
			return null;
		}
		PointerEventData data = new PointerEventData(es);
		data.position = point;
		var results = new Il2CppSystem.Collections.Generic.List<RaycastResult>();
		es.RaycastAll(data, results);
		return results.Count > 0 ? results[0].gameObject : null;
	}

	/// <summary>
	/// A click where the match is on screen: whatever the EventSystem hits at its center takes it, like the real mouse
	/// (a row's button is often a sibling of its text, not a parent). Without a raycast hit, the match's own handler.
	/// </summary>
	private static IEnumerator Click(string query, Result r)
	{
		Hit? hit = One(query, r);
		if (hit == null)
		{
			yield break;
		}
		PointerEventData data = Pointer(hit.Rect);
		GameObject? top = RaycastTop(data.position);
		// Whatever is on top there, or (a label outside its button, like an app icon's name) the match's own handler.
		GameObject? target = (top != null ? Clickable(top.transform) : null) ?? Clickable(hit.Rect);
		if (target == null)
		{
			r.Fail($"nothing takes a click at {hit.Path} ({(top != null ? "topmost there: " + PathOf(top.transform) : "no raycast hit")})");
			yield break;
		}
		data.pointerPress = target;
		data.rawPointerPress = top != null ? top : hit.Rect.gameObject;
		ExecuteEvents.Execute(target, data, ExecuteEvents.pointerEnterHandler);
		ExecuteEvents.Execute(target, data, ExecuteEvents.pointerDownHandler);
		ExecuteEvents.Execute(target, data, ExecuteEvents.pointerUpHandler);
		ExecuteEvents.Execute(target, data, ExecuteEvents.pointerClickHandler);
		ExecuteEvents.Execute(target, data, ExecuteEvents.pointerExitHandler);
		r.Line($"clicked {PathOf(target.transform)}");
		yield return Wait(0.4f, r);
	}

	private static IEnumerator Scroll(string arg, Result r)
	{
		int split = arg.LastIndexOf(' ');
		if (split < 0 || !float.TryParse(arg.Substring(split + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float to))
		{
			r.Fail("scroll <query> <0..1>  (1 = top)");
			yield break;
		}
		Hit? hit = One(arg.Substring(0, split), r);
		if (hit == null)
		{
			yield break;
		}
		ScrollRect? sr = null;
		for (Transform? p = hit.Rect; p != null && sr == null; p = p.parent)
		{
			sr = p.GetComponent<ScrollRect>();
		}
		if (sr == null)
		{
			sr = hit.Rect.GetComponentInChildren<ScrollRect>();
		}
		if (sr == null)
		{
			r.Fail($"no ScrollRect at, above or under {hit.Path}");
			yield break;
		}
		sr.verticalNormalizedPosition = Mathf.Clamp01(to);
		r.Line($"scrolled {PathOf(sr.transform)} to {to:0.##}");
		yield return Wait(0.2f, r);
	}

	private static IEnumerator Type(string arg, Result r)
	{
		int split = arg.IndexOf(" = ", StringComparison.Ordinal);
		if (split < 0)
		{
			r.Fail("type <query> = <text>");
			yield break;
		}
		Hit? hit = One(arg.Substring(0, split), r);
		if (hit == null)
		{
			yield break;
		}
		string text = arg.Substring(split + 3);
		TMP_InputField tmp = hit.Rect.GetComponentInParent<TMP_InputField>();
		InputField legacy = hit.Rect.GetComponentInParent<InputField>();
		if (tmp != null)
		{
			tmp.text = text;
		}
		else if (legacy != null)
		{
			legacy.text = text;
		}
		else
		{
			r.Fail($"no input field at or above {hit.Path}");
			yield break;
		}
		r.Line($"typed into {hit.Path}");
	}

	// ---- looking -------------------------------------------------------------------------------------------------

	private static IEnumerator Tree(string arg, Result r)
	{
		int depth = 3;
		int split = arg.LastIndexOf(' ');
		if (split > 0 && int.TryParse(arg.Substring(split + 1), out int d))
		{
			depth = d;
			arg = arg.Substring(0, split);
		}
		Hit? hit = One(arg, r);
		if (hit == null)
		{
			yield break;
		}
		r.Line(hit.Path);
		int lines = 0;
		Dump(hit.Rect, 0, depth, r, ref lines);
	}

	private static void Dump(Transform t, int level, int depth, Result r, ref int lines)
	{
		if (lines++ > 400)
		{
			if (lines == 402)
			{
				r.Line("… (400 lines; ask for a deeper object)");
			}
			return;
		}
		StringBuilder sb = new StringBuilder(new string(' ', level * 2));
		sb.Append(t.name);
		if (!t.gameObject.activeSelf)
		{
			sb.Append(" (off)");
		}
		RectTransform rt = t.TryCast<RectTransform>();
		if (rt != null)
		{
			Rect s = ScreenRect(rt);
			sb.Append($" size {rt.rect.width:0}x{rt.rect.height:0} pos {rt.anchoredPosition.x:0},{rt.anchoredPosition.y:0} screen {s.x:0},{s.y:0} {s.width:0}x{s.height:0}");
			if (rt.localScale != Vector3.one)
			{
				sb.Append($" scale {rt.localScale.x:0.##},{rt.localScale.y:0.##}");
			}
		}
		CanvasGroup fade = t.GetComponent<CanvasGroup>();
		if (fade != null)
		{
			sb.Append($" alpha {fade.alpha:0.##}{(fade.blocksRaycasts ? "" : " no-raycasts")}");
		}
		Image img = t.GetComponent<Image>();
		if (img != null)
		{
			sb.Append($" img {(img.sprite != null ? img.sprite.name : "-")} #{ColorUtility.ToHtmlStringRGBA(img.color)}{(img.enabled ? "" : " disabled")}");
		}
		TMP_Text tmp = t.GetComponent<TMP_Text>();
		if (tmp != null)
		{
			sb.Append($" tmp {tmp.fontSize:0.#}px #{ColorUtility.ToHtmlStringRGBA(tmp.color)} \"{Short(tmp.text)}\"");
		}
		Text text = t.GetComponent<Text>();
		if (text != null)
		{
			sb.Append($" text {text.fontSize}px #{ColorUtility.ToHtmlStringRGBA(text.color)} \"{Short(text.text)}\"");
		}
		var components = t.GetComponents<Component>();
		sb.Append(" [");
		for (int i = 0; i < components.Length; i++)
		{
			sb.Append(i == 0 ? "" : ", ").Append(components[i].GetIl2CppType().Name);
		}
		sb.Append(']');
		r.Line(sb.ToString());
		if (level >= depth)
		{
			if (t.childCount > 0)
			{
				r.Line(new string(' ', level * 2 + 2) + $"({t.childCount} children)");
			}
			return;
		}
		for (int i = 0; i < t.childCount; i++)
		{
			Dump(t.GetChild(i), level + 1, depth, r, ref lines);
		}
	}

	private IEnumerator Shot(string arg, Result r)
	{
		string name = arg;
		string? query = null;
		int space = arg.IndexOf(' ');
		if (space > 0)
		{
			name = arg.Substring(0, space);
			query = arg.Substring(space + 1).Trim();
		}
		if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
		{
			r.Fail("shot <name> [<query to crop to>]");
			yield break;
		}
		Rect? crop = null;
		if (query != null)
		{
			Hit? hit = One(query, r);
			if (hit == null)
			{
				yield break;
			}
			crop = ScreenRect(hit.Rect);
		}
		yield return new WaitForEndOfFrame();
		Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
		if (crop != null)
		{
			const int pad = 8;
			int x = Mathf.Clamp((int)crop.Value.x - pad, 0, tex.width - 1);
			int y = Mathf.Clamp((int)crop.Value.y - pad, 0, tex.height - 1);
			int w = Mathf.Clamp((int)crop.Value.width + pad * 2, 1, tex.width - x);
			int h = Mathf.Clamp((int)crop.Value.height + pad * 2, 1, tex.height - y);
			Texture2D part = new Texture2D(w, h, TextureFormat.RGBA32, false);
			part.SetPixels(tex.GetPixels(x, y, w, h));
			part.Apply();
			Object.Destroy(tex);
			tex = part;
		}
		byte[] png = ImageConversion.EncodeToPNG(tex).ToArray();
		int tw = tex.width, th = tex.height;
		Object.Destroy(tex);
		string path = Path.Combine(shots, name + ".png");
		File.WriteAllBytes(path, png);
		r.Line($"shot {path} ({tw}x{th})");
	}

	private static IEnumerator State(Result r)
	{
		GameplayMenu menu = Singleton<GameplayMenu>.Instance;
		var phone = PlayerSingleton<Il2CppScheduleOne.UI.Phone.Phone>.Instance;
		r.Line($"screen {Screen.width}x{Screen.height} {Screen.fullScreenMode}, {1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f):0} fps, focused {Application.isFocused}");
		r.Line(menu == null || phone == null
			? "not in a world (no GameplayMenu/Phone yet)"
			: $"phone {(phone.IsOpen ? "open" : "closed")}, app {(Il2CppScheduleOne.UI.Phone.Phone.ActiveApp != null ? Il2CppScheduleOne.UI.Phone.Phone.ActiveApp.name : "-")}, menu {(menu.IsOpen ? "open" : "closed")} on {menu.CurrentScreen}");
		yield break;
	}

	/// <summary>
	/// Opens one of Messages' popups over the open chat, to look at it without waiting for a customer's offer. Their own
	/// callbacks are left empty, so look and close (back): don't press their confirm buttons.
	/// </summary>
	private static IEnumerator Popup(string what, Result r)
	{
		var app = PlayerSingleton<Il2CppScheduleOne.UI.Phone.Messages.MessagesApp>.Instance;
		var conversation = app != null ? app.currentConversation : null;
		if (conversation == null)
		{
			r.Fail("open a chat first (phone open; click Messages; click <name>)");
			yield break;
		}
		switch (what)
		{
			case "dealwindow":
				app!.DealWindowSelector.SetIsOpen(true, conversation, null);
				break;
			case "counteroffer":
				var products = Il2CppScheduleOne.Product.ProductManager.DiscoveredProducts;
				if (products == null || products.Count == 0)
				{
					r.Fail("no discovered products in this world");
					yield break;
				}
				app!.CounterofferInterface.Open(products[0], 2, 100f, conversation, null);
				break;
			default:
				r.Fail("ui dealwindow | ui counteroffer");
				yield break;
		}
		r.Line($"opened {what} over {conversation.ContactName}");
		yield return Wait(0.6f, r);
	}

	private IEnumerator Res(string arg, Result r)
	{
		string[] parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h) || w < 640 || h < 360)
		{
			r.Fail("res <width> <height>  (at least 640 360)");
			yield break;
		}
		width = w;
		height = h;
		File.WriteAllText(resFile, $"{w} {h}");
		Screen.SetResolution(w, h, FullScreenMode.Windowed);
		// The window and the UI's screen positions settle over a few frames; a click before that misses.
		yield return Wait(1f, r);
		r.Line($"screen {Screen.width}x{Screen.height}");
	}

	private IEnumerator WindowCmd(string arg, Result r)
	{
		string? done = Window.Place(arg, width, height);
		if (done == null)
		{
			r.Fail(arg is "back" or "hide" or "show" ? "no Unity window found for this process" : "window back | hide | show");
		}
		else
		{
			r.Line(done);
		}
		yield break;
	}
}

/// <summary>Moves the game's own window without ever activating it, so whatever Blazzer is playing keeps focus.</summary>
internal static class Window
{
	private delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);

	[DllImport("user32.dll")]
	private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

	[DllImport("user32.dll")]
	private static extern bool IsIconic(IntPtr hwnd);

	[DllImport("user32.dll")]
	private static extern bool ShowWindow(IntPtr hwnd, int cmd);

	private static readonly IntPtr Bottom = new IntPtr(1);
	private const uint NoSize = 0x1, NoMove = 0x2, NoActivate = 0x10;
	private const int ShowNoActivate = 4;

	private static IntPtr Find()
	{
		uint me = (uint)Environment.ProcessId;
		IntPtr found = IntPtr.Zero;
		StringBuilder cls = new StringBuilder(64);
		EnumWindows((hwnd, _) =>
		{
			GetWindowThreadProcessId(hwnd, out uint pid);
			if (pid != me)
			{
				return true;
			}
			cls.Clear();
			GetClassName(hwnd, cls, cls.Capacity);
			if (cls.ToString() != "UnityWndClass")
			{
				return true;
			}
			found = hwnd;
			return false;
		}, IntPtr.Zero);
		return found;
	}

	/// <summary>back: behind every other window. hide: off screen (it keeps rendering). show: on screen, still behind.</summary>
	internal static string? Place(string how, int width, int height)
	{
		IntPtr hwnd = Find();
		if (hwnd == IntPtr.Zero)
		{
			return null;
		}
		if (IsIconic(hwnd))
		{
			ShowWindow(hwnd, ShowNoActivate); // a minimized Unity window renders nothing to capture
		}
		switch (how)
		{
			case "back":
				SetWindowPos(hwnd, Bottom, 0, 0, 0, 0, NoSize | NoMove | NoActivate);
				return "window sent behind the others";
			case "hide":
				SetWindowPos(hwnd, Bottom, -width - 200, 40, 0, 0, NoSize | NoActivate);
				return "window moved off screen (still rendering)";
			case "show":
				SetWindowPos(hwnd, Bottom, 80, 80, 0, 0, NoSize | NoActivate);
				return "window on screen at 80,80, behind the others";
			default:
				return null;
		}
	}
}
