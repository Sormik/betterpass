/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Color palettes of the two designs, each in a light and a dark variant.

  - Windows 11: neutral Fluent surfaces, accent color from Windows
  - aethercert: "Leather" tokens of the aethercert brand kit (warm paper,
    neutral ink, leather for structure, gold only as a single accent line)

  All borders are hairlines; no palette has shadows.
*/

using System;
using System.Drawing;

using Microsoft.Win32;

namespace BetterPass
{
	internal sealed class ThemePalette
	{
		public string Key = string.Empty;   // Identifies design, mode and accent
		public bool IsAethercert = false;
		public bool IsDark = true;

		// Input fields (text boxes, drop-downs, spin and date fields)
		public Color InputFill;
		public Color InputFillFocus;
		public Color InputStroke;   // Border at rest
		public Color InputBottom;   // Bottom edge at rest (Windows 11 underline)
		public bool FocusUnderline = true; // Windows 11: accent underline; aethercert: focus ring

		public Color Back;         // Window and page background
		public Color Surface;      // Inputs, lists, cards
		public Color Control;      // Buttons at rest
		public Color Hover;
		public Color Pressed;
		public Color Border;       // Hairlines, card and button borders
		public Color InputBorder;  // Text boxes, check boxes
		public Color Text;
		public Color DimText;      // Secondary text
		public Color DisabledText;
		public Color Accent;       // Primary button, checked check boxes
		public Color AccentText;   // Text on Accent
		public Color AccentHover;
		public Color Focus;        // Focus border of inputs
		public Color Highlight;    // One accent line per area (tab marker, banner)
		public Color Link;
		public Color Success;
		public Color Warning;
		public Color Error;

		public int Radius = 4;     // Corner radius at 96 DPI (cards use Radius + 2)

		public Color[] AllColors()
		{
			return new Color[] { Back, Surface, Control, Hover, Pressed, Border,
				InputBorder, Text, DimText, DisabledText, Accent, AccentText, AccentHover,
				Focus, Highlight, Link, Success, Warning, Error, InputFill, InputFillFocus,
				InputStroke, InputBottom };
		}

		// ===================== Windows 11 =====================

		public static ThemePalette Windows11(bool bDark)
		{
			Color clrAccent = GetWindowsAccent(bDark);
			ThemePalette p = new ThemePalette();
			p.Key = "win11-" + (bDark ? "dark-" : "light-") + clrAccent.ToArgb().ToString("X8");
			p.IsDark = bDark;
			p.Radius = 4;

			if(bDark)
			{
				p.Back = Hex(0x202020);
				p.Surface = Hex(0x2B2B2B);
				p.Control = Hex(0x2D2D2D);
				p.Hover = Hex(0x383838);
				p.Pressed = Hex(0x323232);
				p.Border = Hex(0x3B3B3B);
				p.InputBorder = Hex(0x4D4D4D);
				p.Text = Hex(0xFFFFFF);
				p.DimText = Hex(0xADADAD);
				p.DisabledText = Hex(0x787878);
				p.AccentText = Hex(0x000000);
				p.Success = Hex(0x6CCB5F);
				p.Warning = Hex(0xFCE100);
				p.Error = Hex(0xFF99A4);
				p.InputFill = Hex(0x2D2D2D);
				p.InputFillFocus = Hex(0x1F1F1F);
				p.InputStroke = Hex(0x3A3A3A);
				p.InputBottom = Hex(0x8B8B8B);
			}
			else
			{
				p.Back = Hex(0xF3F3F3);
				p.Surface = Hex(0xFFFFFF);
				p.Control = Hex(0xFBFBFB);
				p.Hover = Hex(0xEDEDED);
				p.Pressed = Hex(0xE4E4E4);
				p.Border = Hex(0xE0E0E0);
				p.InputBorder = Hex(0xC9C9C9);
				p.Text = Hex(0x1B1B1B);
				p.DimText = Hex(0x5F5F5F);
				p.DisabledText = Hex(0xA0A0A0);
				p.AccentText = Hex(0xFFFFFF);
				p.Success = Hex(0x0F7B0F);
				p.Warning = Hex(0x9D5D00);
				p.Error = Hex(0xC42B1C);
				p.InputFill = Hex(0xFBFBFB);
				p.InputFillFocus = Hex(0xFFFFFF);
				p.InputStroke = Hex(0xE3E3E3);
				p.InputBottom = Hex(0x8A8A8A);
			}

			p.Accent = clrAccent;
			p.AccentHover = Mix(clrAccent, p.Back, 0.12);
			p.Focus = clrAccent;
			p.Highlight = clrAccent;
			p.Link = clrAccent;
			return p;
		}

		/// <summary>
		/// Windows' accent color in the shade Windows 11 uses for controls:
		/// "Dark 1" on light backgrounds, "Light 2" on dark backgrounds.
		/// </summary>
		private static Color GetWindowsAccent(bool bDark)
		{
			try
			{
				using(RegistryKey k = Registry.CurrentUser.OpenSubKey(
					"Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", false))
				{
					byte[] pb = ((k != null) ? (k.GetValue("AccentPalette") as byte[]) : null);
					if((pb != null) && (pb.Length >= 32))
					{
						int i = (bDark ? 1 : 4) * 4; // RGBA entries
						return Color.FromArgb(pb[i], pb[i + 1], pb[i + 2]);
					}
				}
			}
			catch(Exception) { }

			return (bDark ? Hex(0x60CDFF) : Hex(0x005FB8)); // Windows 11 default blue
		}

		// ===================== aethercert =====================

		public static ThemePalette Aethercert(bool bDark)
		{
			ThemePalette p = new ThemePalette();
			p.Key = "aethercert-" + (bDark ? "dark" : "light");
			p.IsDark = bDark;
			p.Radius = 6; // radius-md: buttons

			if(bDark)
			{
				p.Back = Hex(0x0C0C0C);         // Deep Ink
				p.Surface = Hex(0x151515);      // Ink Card
				p.Control = Hex(0x232323);      // secondary
				p.Hover = Hex(0x2C2C2C);
				p.Pressed = Hex(0x333333);
				p.Border = Hex(0x2A2A2A);
				p.InputBorder = Hex(0x707070);  // input
				p.Text = Hex(0xFAF9F5);
				p.DimText = Hex(0xACAAA5);      // muted-foreground
				p.DisabledText = Hex(0x707070);
				p.Accent = Hex(0xF0EEE6);       // primary
				p.AccentText = Hex(0x141413);
				p.AccentHover = Hex(0xD8D6CF);
				p.Focus = Hex(0x8A8A8A);        // ring
				p.Link = Hex(0xF2B705);
				p.Success = Hex(0x7FA078);
				p.Error = Hex(0xC4614B);        // destructive
			}
			else
			{
				p.Back = Hex(0xF0EEE6);         // Base White (paper)
				p.Surface = Hex(0xFAF9F5);      // Highlight White (cards)
				p.Control = Hex(0xE6E1D6);      // secondary
				p.Hover = Hex(0xDCD6CA);
				p.Pressed = Hex(0xD2CBBD);
				p.Border = Hex(0xD9D4C8);
				p.InputBorder = Hex(0x8A7D70);  // input
				p.Text = Hex(0x141413);         // Base Dark
				p.DimText = Hex(0x504034);      // Leather
				p.DisabledText = Hex(0x8A7D70);
				p.Accent = Hex(0x2F2621);       // Dark Leather (primary)
				p.AccentText = Hex(0xFAF9F5);
				p.AccentHover = Hex(0x463B34);
				p.Focus = Hex(0x504034);        // ring
				p.Link = Hex(0x141413);         // links get the gold underline instead
				p.Success = Hex(0x46663F);      // success, darkened for text on paper
				p.Error = Hex(0x9E3B2C);        // destructive
			}

			p.Highlight = Hex(0xF2B705);        // Gold: the one contrast, same in both modes
			p.Warning = Hex(0xE07B39);          // Orange, so it never looks like gold

			// Inputs: card fill, "input" border, 2 px focus ring in "ring"
			p.IsAethercert = true;
			p.InputFill = p.Surface;
			p.InputFillFocus = p.Surface;
			p.InputStroke = p.InputBorder;
			p.InputBottom = p.InputBorder;
			p.FocusUnderline = false;
			return p;
		}

		// ===================== Helpers =====================

		public static Color Hex(int rgb)
		{
			return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
		}

		/// <summary>Mixes clrB into clrA (f = share of clrB, 0..1).</summary>
		public static Color Mix(Color clrA, Color clrB, double f)
		{
			if(f < 0.0) f = 0.0;
			if(f > 1.0) f = 1.0;
			return Color.FromArgb((int)Math.Round(clrA.R + (clrB.R - clrA.R) * f),
				(int)Math.Round(clrA.G + (clrB.G - clrA.G) * f),
				(int)Math.Round(clrA.B + (clrB.B - clrA.B) * f));
		}
	}
}
