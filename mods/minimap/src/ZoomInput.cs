using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HylandMinimap;

internal sealed class ZoomInput
{
	private bool bindingsInitialized;

	private KeyCode previousZoomIn;

	private KeyCode previousZoomOut;

	private Key zoomIn;

	private Key zoomOut;

	internal bool Update(Settings settings)
	{
		KeyCode value = settings.ZoomInKey.Value;
		KeyCode value2 = settings.ZoomOutKey.Value;
		if (!bindingsInitialized || value != previousZoomIn || value2 != previousZoomOut)
		{
			previousZoomIn = value;
			previousZoomOut = value2;
			zoomIn = ToInputKey(value);
			zoomOut = ToInputKey(value2);
			bindingsInitialized = true;
		}
		Keyboard current = Keyboard.current;
		if (current == null)
		{
			return false;
		}
		bool flag = zoomIn != Key.None && current[zoomIn].wasPressedThisFrame;
		bool flag2 = zoomOut != Key.None && current[zoomOut].wasPressedThisFrame;
		if (flag == flag2)
		{
			return false;
		}
		float num = MapMath.NextZoom(settings.Zoom.Value, settings.ZoomStep.Value, flag);
		if (settings.Zoom.Value == num)
		{
			return false;
		}
		settings.Zoom.Value = num;
		settings.Save();
		return true;
	}

	private static Key ToInputKey(KeyCode keyCode)
	{
		if (keyCode == KeyCode.None || !Enum.IsDefined(typeof(KeyCode), keyCode))
		{
			return Key.None;
		}
		string text = keyCode.ToString();
		if (text.StartsWith("Mouse", StringComparison.Ordinal) || text.StartsWith("Joystick", StringComparison.Ordinal))
		{
			return Key.None;
		}
		text = text switch
		{
			"Return" => "Enter", 
			"LeftControl" => "LeftCtrl", 
			"RightControl" => "RightCtrl", 
			"BackQuote" => "Backquote", 
			"Numlock" => "NumLock", 
			"Menu" => "ContextMenu", 
			"Print" => "PrintScreen", 
			_ => text, 
		};
		if (text.StartsWith("Alpha", StringComparison.Ordinal))
		{
			text = "Digit" + text.Substring(5);
		}
		else if (text.StartsWith("Keypad", StringComparison.Ordinal))
		{
			text = "Numpad" + text.Substring(6);
		}
		if (!Enum.TryParse<Key>(text, ignoreCase: true, out var result) || !Enum.IsDefined(typeof(Key), result))
		{
			return Key.None;
		}
		return result;
	}
}
