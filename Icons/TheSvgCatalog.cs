/*
  BetterPass - KeePass plugin
  Brand icon catalog from theSVG (https://thesvg.org).

  The manifest (~3.5 MB JSON) and downloaded SVGs are cached in
  %LOCALAPPDATA%\KeePass\BetterPass\thesvg. Brand icons remain the
  property of their respective trademark owners.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using KeePass.DataExchange;

using KeePassLib;
using KeePassLib.Utility;

namespace BetterPass
{
	internal sealed class SvgIconInfo
	{
		public string Slug = string.Empty;
		public string Title = string.Empty;
		public string Hex = string.Empty;
		public string Url = string.Empty;
		public string Domain = string.Empty;    // Registrable domain of the brand URL
		public string[] Aliases = new string[0];
		public readonly Dictionary<string, string> Variants =
			new Dictionary<string, string>(StringComparer.Ordinal);

		internal string NormSlug = string.Empty;
		internal string NormTitle = string.Empty;
		internal string[] NormAliases = new string[0];

		public string GetVariantPath(string strVariant)
		{
			string strPath;
			if(!string.IsNullOrEmpty(strVariant) && Variants.TryGetValue(strVariant, out strPath))
				return strPath;
			if(Variants.TryGetValue("default", out strPath)) return strPath;
			foreach(string str in Variants.Values) return str;
			return null;
		}

		public override string ToString() { return Title; }
	}

	internal static class TheSvgCatalog
	{
		private static readonly string[] ManifestUrls = new string[] {
			"https://cdn.jsdelivr.net/gh/glincker/thesvg@main/src/data/icons.json",
			"https://raw.githubusercontent.com/glincker/thesvg/main/src/data/icons.json"
		};
		private static readonly string[] IconBaseUrls = new string[] {
			"https://cdn.jsdelivr.net/gh/glincker/thesvg@main/public",
			"https://thesvg.org"
		};

		private const string ManifestFile = "icons.json";
		private const int MaxManifestBytes = 48 * 1024 * 1024;
		private const int MaxSvgBytes = 2 * 1024 * 1024;

		// Only collections that make sense as entry icons
		private static readonly string[] Collections = new string[] { "brands", "community" };

		private static readonly Regex g_rxVariantPath = new Regex(
			"^/icons/[A-Za-z0-9._-]+/[A-Za-z0-9._-]+\\.svg$", RegexOptions.CultureInvariant);

		private static readonly object g_oLock = new object();
		private static List<SvgIconInfo> g_lIcons = null;

		public static bool IsLoaded
		{
			get { lock(g_oLock) { return (g_lIcons != null); } }
		}

		private static string CacheDir { get { return Net.GetCacheDirectory("thesvg"); } }

		/// <summary>
		/// Loads the catalog (from cache or network). Blocking.
		/// </summary>
		public static List<SvgIconInfo> Load(bool bForceRefresh)
		{
			lock(g_oLock)
			{
				if((g_lIcons != null) && !bForceRefresh) return g_lIcons;
			}

			string strFile = Path.Combine(CacheDir, ManifestFile);
			string strJson = null;
			bool bFresh = File.Exists(strFile) && ((DateTime.UtcNow -
				File.GetLastWriteTimeUtc(strFile)).TotalDays < BpConfig.IconCatalogMaxAgeDays);

			if(bFresh && !bForceRefresh)
			{
				try
				{
					List<SvgIconInfo> lCached = Parse(File.ReadAllText(strFile, Encoding.UTF8));
					if(lCached.Count > 0)
					{
						lock(g_oLock) { g_lIcons = lCached; }
						return lCached;
					}
				}
				catch(Exception) { } // Corrupt cache: download again
			}

			if(strJson == null)
			{
				Exception exLast = null;
				foreach(string strUrl in ManifestUrls)
				{
					try
					{
						strJson = Net.DownloadText(strUrl, MaxManifestBytes);
						List<SvgIconInfo> lTest = Parse(strJson); // Validate before caching
						if(lTest.Count == 0) throw new InvalidDataException("Empty catalog.");
						WriteFileAtomic(strFile, Encoding.UTF8.GetBytes(strJson));
						lock(g_oLock) { g_lIcons = lTest; }
						return lTest;
					}
					catch(Exception ex) { exLast = ex; strJson = null; }
				}

				// Offline: use an outdated cache if present
				if(File.Exists(strFile))
				{
					try { strJson = File.ReadAllText(strFile, Encoding.UTF8); }
					catch(Exception) { strJson = null; }
				}
				if(strJson == null)
					throw new IOException("Der Icon-Katalog von thesvg.org konnte nicht geladen werden: " +
						Net.GetErrorMessage(exLast), exLast);
			}

			List<SvgIconInfo> l = Parse(strJson);
			lock(g_oLock) { g_lIcons = l; }
			return l;
		}

		private static List<SvgIconInfo> Parse(string strJson)
		{
			List<SvgIconInfo> lOut = new List<SvgIconInfo>();
			if(string.IsNullOrEmpty(strJson)) return lOut;

			// KeePass' JSON parser expects an object at the root
			string strWrapped = "{\"icons\":" + strJson.Trim() + "}";
			JsonObject jo = new JsonObject(new CharStream(strWrapped));
			object oList;
			if(!jo.Items.TryGetValue("icons", out oList)) return lOut;
			List<object> l = (oList as List<object>);
			if(l == null) return lOut;

			foreach(object o in l)
			{
				JsonObject joIcon = (o as JsonObject);
				if(joIcon == null) continue;

				string strCollection = GetString(joIcon, "collection");
				if(Array.IndexOf(Collections, strCollection) < 0) continue;

				SvgIconInfo ii = new SvgIconInfo();
				ii.Slug = GetString(joIcon, "slug");
				ii.Title = GetString(joIcon, "title");
				ii.Hex = GetString(joIcon, "hex");
				ii.Url = GetString(joIcon, "url");
				if((ii.Slug.Length == 0) || (ii.Title.Length == 0)) continue;

				object oAliases;
				List<string> lAliases = new List<string>();
				if(joIcon.Items.TryGetValue("aliases", out oAliases) && (oAliases is List<object>))
				{
					foreach(object oA in (List<object>)oAliases)
					{
						string strA = (oA as string);
						if(!string.IsNullOrEmpty(strA)) lAliases.Add(strA);
					}
				}
				ii.Aliases = lAliases.ToArray();

				object oVariants;
				JsonObject joVar = null;
				if(joIcon.Items.TryGetValue("variants", out oVariants))
					joVar = (oVariants as JsonObject);
				if(joVar != null)
				{
					foreach(KeyValuePair<string, object> kvp in joVar.Items)
					{
						string strPath = (kvp.Value as string);
						if(!string.IsNullOrEmpty(strPath) && g_rxVariantPath.IsMatch(strPath))
							ii.Variants[kvp.Key] = strPath;
					}
				}
				if(ii.Variants.Count == 0) continue;

				ii.Domain = GetRegistrableDomain(ii.Url);
				ii.NormSlug = Normalize(ii.Slug);
				ii.NormTitle = Normalize(ii.Title);
				string[] vNA = new string[ii.Aliases.Length];
				for(int i = 0; i < vNA.Length; ++i) vNA[i] = Normalize(ii.Aliases[i]);
				ii.NormAliases = vNA;

				lOut.Add(ii);
			}

			return lOut;
		}

		private static string GetString(JsonObject jo, string strKey)
		{
			object o;
			if(!jo.Items.TryGetValue(strKey, out o)) return string.Empty;
			string s = (o as string);
			return (s ?? string.Empty).Trim();
		}

		// ===================== Search & matching =====================

		public static string Normalize(string str)
		{
			if(string.IsNullOrEmpty(str)) return string.Empty;
			StringBuilder sb = new StringBuilder(str.Length);
			foreach(char ch in str.ToLowerInvariant())
			{
				if(char.IsLetterOrDigit(ch)) sb.Append(ch);
			}
			return sb.ToString();
		}

		public static List<SvgIconInfo> Search(List<SvgIconInfo> lIcons, string strQuery, int nMax)
		{
			List<SvgIconInfo> lOut = new List<SvgIconInfo>();
			if(lIcons == null) return lOut;

			string q = Normalize(strQuery);
			if(q.Length == 0)
			{
				for(int i = 0; (i < lIcons.Count) && (lOut.Count < nMax); ++i) lOut.Add(lIcons[i]);
				return lOut;
			}

			string qDomain = GetRegistrableDomain(strQuery);
			List<KeyValuePair<int, SvgIconInfo>> l = new List<KeyValuePair<int, SvgIconInfo>>();
			foreach(SvgIconInfo ii in lIcons)
			{
				int nScore = Score(ii, q, qDomain);
				if(nScore > 0) l.Add(new KeyValuePair<int, SvgIconInfo>(nScore, ii));
			}

			l.Sort(delegate(KeyValuePair<int, SvgIconInfo> a, KeyValuePair<int, SvgIconInfo> b)
			{
				int c = b.Key.CompareTo(a.Key);
				if(c != 0) return c;
				return string.Compare(a.Value.Title, b.Value.Title, StringComparison.OrdinalIgnoreCase);
			});

			for(int i = 0; (i < l.Count) && (lOut.Count < nMax); ++i) lOut.Add(l[i].Value);
			return lOut;
		}

		private static int Score(SvgIconInfo ii, string q, string qDomain)
		{
			if((qDomain.Length > 0) && (ii.Domain == qDomain)) return 1100;
			if(ii.NormSlug == q) return 1000;
			if(ii.NormTitle == q) return 950;
			foreach(string a in ii.NormAliases) { if(a == q) return 900; }
			if(ii.NormSlug.StartsWith(q, StringComparison.Ordinal)) return 700 - Math.Min(100, ii.NormSlug.Length);
			if(ii.NormTitle.StartsWith(q, StringComparison.Ordinal)) return 650 - Math.Min(100, ii.NormTitle.Length);
			if(q.Length >= 3)
			{
				if(ii.NormSlug.IndexOf(q, StringComparison.Ordinal) >= 0) return 400 - Math.Min(100, ii.NormSlug.Length);
				if(ii.NormTitle.IndexOf(q, StringComparison.Ordinal) >= 0) return 350 - Math.Min(100, ii.NormTitle.Length);
				foreach(string a in ii.NormAliases)
				{
					if(a.IndexOf(q, StringComparison.Ordinal) >= 0) return 300;
				}
			}
			return 0;
		}

		/// <summary>
		/// Finds the most likely icon for an entry (URL domain, then title).
		/// Returns null if there is no confident match.
		/// </summary>
		public static SvgIconInfo FindBestMatch(List<SvgIconInfo> lIcons, PwEntry pe)
		{
			if((lIcons == null) || (pe == null)) return null;

			string strUrl = pe.Strings.ReadSafe(PwDefs.UrlField);
			string strDomain = GetRegistrableDomain(strUrl);
			string strTitle = Normalize(pe.Strings.ReadSafe(PwDefs.TitleField));
			string strDomainLabel = string.Empty;
			if(strDomain.Length > 0)
			{
				int iDot = strDomain.IndexOf('.');
				strDomainLabel = Normalize((iDot > 0) ? strDomain.Substring(0, iDot) : strDomain);
			}

			SvgIconInfo iiBest = null;
			int nBest = 0;
			foreach(SvgIconInfo ii in lIcons)
			{
				int n = 0;
				bool bTitle = ((strTitle.Length > 0) && ((ii.NormTitle == strTitle) ||
					(ii.NormSlug == strTitle) || (Array.IndexOf(ii.NormAliases, strTitle) >= 0)));

				if((strDomain.Length > 0) && (ii.Domain == strDomain)) n = (bTitle ? 120 : 100);
				else if(bTitle) n = 90;
				else if((strDomainLabel.Length >= 3) && ((ii.NormSlug == strDomainLabel) ||
					(ii.NormTitle == strDomainLabel))) n = 80;

				if(n > nBest) { nBest = n; iiBest = ii; }
			}

			return ((nBest >= 80) ? iiBest : null);
		}

		private static readonly string[] SecondLevel = new string[] {
			"co", "com", "net", "org", "gov", "ac", "edu", "or", "ne", "go"
		};

		public static string GetRegistrableDomain(string strUrlOrHost)
		{
			if(string.IsNullOrEmpty(strUrlOrHost)) return string.Empty;
			string s = strUrlOrHost.Trim().ToLowerInvariant();
			if(s.IndexOf(' ') >= 0) return string.Empty;

			int iScheme = s.IndexOf("://", StringComparison.Ordinal);
			if(iScheme >= 0) s = s.Substring(iScheme + 3);
			else if(s.IndexOf('.') < 0) return string.Empty;

			int iEnd = s.IndexOfAny(new char[] { '/', '?', '#', ':' });
			if(iEnd >= 0) s = s.Substring(0, iEnd);
			int iAt = s.LastIndexOf('@');
			if(iAt >= 0) s = s.Substring(iAt + 1);
			s = s.Trim('.');

			string[] v = s.Split('.');
			if(v.Length < 2) return string.Empty;
			foreach(string p in v) { if(p.Length == 0) return string.Empty; }

			int n = 2;
			if((v.Length >= 3) && (v[v.Length - 1].Length == 2) &&
				(Array.IndexOf(SecondLevel, v[v.Length - 2]) >= 0)) n = 3;

			StringBuilder sb = new StringBuilder();
			for(int i = v.Length - n; i < v.Length; ++i)
			{
				if(sb.Length > 0) sb.Append('.');
				sb.Append(v[i]);
			}
			return sb.ToString();
		}

		// ===================== SVG download =====================

		/// <summary>
		/// Returns the SVG text of an icon variant (cached). Blocking.
		/// </summary>
		public static string GetSvg(SvgIconInfo ii, string strVariant)
		{
			if(ii == null) throw new ArgumentNullException("ii");
			string strPath = ii.GetVariantPath(strVariant);
			if(strPath == null) throw new InvalidDataException("Icon has no SVG file.");

			// Path is validated by g_rxVariantPath: /icons/<slug>/<name>.svg
			string strCacheName = strPath.Substring("/icons/".Length).Replace('/', '_');
			string strFile = Path.Combine(CacheDir, "svg_" + strCacheName);

			if(File.Exists(strFile))
			{
				try { return File.ReadAllText(strFile, Encoding.UTF8); }
				catch(Exception) { }
			}

			Exception exLast = null;
			foreach(string strBase in IconBaseUrls)
			{
				try
				{
					byte[] pb = Net.Download(strBase + strPath, MaxSvgBytes);
					string strSvg = Net.DecodeUtf8(pb);
					if(strSvg.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) < 0)
						throw new InvalidDataException("No SVG data.");
					try { WriteFileAtomic(strFile, pb); }
					catch(Exception) { } // Cache is optional
					return strSvg;
				}
				catch(Exception ex) { exLast = ex; }
			}

			throw new IOException("Icon konnte nicht geladen werden: " +
				Net.GetErrorMessage(exLast), exLast);
		}

		public static void ClearCache()
		{
			lock(g_oLock) { g_lIcons = null; }
			string strDir = CacheDir;
			foreach(string strFile in Directory.GetFiles(strDir))
			{
				try { File.Delete(strFile); }
				catch(Exception) { }
			}
		}

		private static void WriteFileAtomic(string strFile, byte[] pb)
		{
			string strTmp = strFile + ".tmp";
			File.WriteAllBytes(strTmp, pb);
			if(File.Exists(strFile)) File.Delete(strFile);
			File.Move(strTmp, strFile);
		}

		public static System.Drawing.Color GetBrandColor(SvgIconInfo ii)
		{
			if((ii != null) && (ii.Hex.Length == 6))
			{
				int v;
				if(int.TryParse(ii.Hex, System.Globalization.NumberStyles.HexNumber,
					System.Globalization.CultureInfo.InvariantCulture, out v))
					return System.Drawing.Color.FromArgb(255, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
			}
			return System.Drawing.Color.Black;
		}
	}
}
