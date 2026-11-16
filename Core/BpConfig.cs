/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Settings, stored in KeePass' configuration file (CustomConfig).
*/

using System;

using KeePass.App.Configuration;

namespace BetterPass
{
	internal enum ThemeDesign
	{
		Classic = 0,    // KeePass' own look, BetterPass changes nothing
		Windows11 = 1,
		Aethercert = 2
	}

	internal enum ThemeMode
	{
		Light = 0,
		Dark = 1,
		System = 2
	}

	internal static class BpConfig
	{
		private const string Prefix = "BetterPass.";

		private static AceCustomConfig g_cfg = null;

		public static event EventHandler Changed;

		public static void Initialize(AceCustomConfig cfg)
		{
			g_cfg = cfg;
		}

		// ---------- TOTP ----------
		public static bool TotpShowFields
		{
			get { return GetBool("Totp.ShowFields", true); }
			set { SetBool("Totp.ShowFields", value); }
		}

		public static bool TotpHideKey
		{
			get { return GetBool("Totp.HideKey", true); }
			set { SetBool("Totp.HideKey", value); }
		}

		// ---------- Theme ----------
		public static ThemeDesign Design
		{
			get
			{
				string s = GetString("Theme.Design", string.Empty);
				if(s == "classic") return ThemeDesign.Classic;
				if(s == "aethercert") return ThemeDesign.Aethercert;
				if(s == "win11") return ThemeDesign.Windows11;

				// Up to 1.0, "Theme.Mode" was off/dark/system
				if(GetString("Theme.Mode", "system") == "off") return ThemeDesign.Classic;
				return ThemeDesign.Windows11;
			}
			set
			{
				string s = "win11";
				if(value == ThemeDesign.Classic) s = "classic";
				else if(value == ThemeDesign.Aethercert) s = "aethercert";
				SetString("Theme.Design", s);
			}
		}

		public static ThemeMode ColorMode
		{
			get
			{
				string s = GetString("Theme.Mode", "system");
				if(s == "dark") return ThemeMode.Dark;
				if(s == "light") return ThemeMode.Light;
				return ThemeMode.System; // also the old "off"
			}
			set
			{
				string s = "system";
				if(value == ThemeMode.Dark) s = "dark";
				else if(value == ThemeMode.Light) s = "light";
				SetString("Theme.Mode", s);
			}
		}

		// ---------- Have I Been Pwned ----------
		public static bool HibpEntryCheck
		{
			get { return GetBool("Hibp.EntryCheck", true); }
			set { SetBool("Hibp.EntryCheck", value); }
		}

		public static bool HibpAutoCheck
		{
			get { return GetBool("Hibp.AutoCheck", true); }
			set { SetBool("Hibp.AutoCheck", value); }
		}

		public static bool HibpCheckOnOpen
		{
			get { return GetBool("Hibp.CheckOnOpen", false); }
			set { SetBool("Hibp.CheckOnOpen", value); }
		}

		// ---------- Password generator ----------
		// True once the BetterPass generator profiles have been added to
		// KeePass and Apple has been made the default (done only once)
		public static bool PwGenProfilesInstalled
		{
			get { return GetBool("PwGen.ProfilesInstalled", false); }
			set { SetBool("PwGen.ProfilesInstalled", value); }
		}

		public static bool HibpTagEntries
		{
			get { return GetBool("Hibp.TagEntries", false); }
			set { SetBool("Hibp.TagEntries", value); }
		}

		public const string HibpTag = "Pwned";

		// ---------- Icons ----------
		public static string IconVariant
		{
			get { return GetString("Icons.Variant", "default"); }
			set { SetString("Icons.Variant", value); }
		}

		public static int IconSize
		{
			get
			{
				long l = GetLong("Icons.Size", 64);
				if((l != 32) && (l != 64) && (l != 128)) l = 64;
				return (int)l;
			}
			set { SetLong("Icons.Size", value); }
		}

		public static int IconCatalogMaxAgeDays
		{
			get
			{
				long l = GetLong("Icons.CatalogMaxAgeDays", 7);
				if(l < 1) l = 1;
				if(l > 365) l = 365;
				return (int)l;
			}
			set { SetLong("Icons.CatalogMaxAgeDays", value); }
		}

		public static void RaiseChanged()
		{
			EventHandler h = Changed;
			if(h != null) h(null, EventArgs.Empty);
		}

		// ---------- Helpers ----------
		private static bool GetBool(string strKey, bool bDefault)
		{
			if(g_cfg == null) return bDefault;
			return g_cfg.GetBool(Prefix + strKey, bDefault);
		}

		private static void SetBool(string strKey, bool b)
		{
			if(g_cfg != null) g_cfg.SetBool(Prefix + strKey, b);
		}

		private static string GetString(string strKey, string strDefault)
		{
			if(g_cfg == null) return strDefault;
			string s = g_cfg.GetString(Prefix + strKey, strDefault);
			return (string.IsNullOrEmpty(s) ? strDefault : s);
		}

		private static void SetString(string strKey, string str)
		{
			if(g_cfg != null) g_cfg.SetString(Prefix + strKey, str);
		}

		private static long GetLong(string strKey, long lDefault)
		{
			if(g_cfg == null) return lDefault;
			return g_cfg.GetLong(Prefix + strKey, lDefault);
		}

		private static void SetLong(string strKey, long l)
		{
			if(g_cfg != null) g_cfg.SetLong(Prefix + strKey, l);
		}
	}
}
