/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Have I Been Pwned - Pwned Passwords range API (k-anonymity).

  Only the first 5 hex characters of the SHA-1 hash leave the computer.
  The password itself and the full hash are never transmitted.
  https://haveibeenpwned.com/API/v3#PwnedPasswords
*/

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BetterPass
{
	internal static class HibpClient
	{
		private const string RangeUrl = "https://api.pwnedpasswords.com/range/";
		private const int MaxResponseBytes = 4 * 1024 * 1024;
		private const int MaxCachedPrefixes = 4096;

		private static readonly object g_oLock = new object();
		private static readonly Dictionary<string, Dictionary<string, int>> g_dCache =
			new Dictionary<string, Dictionary<string, int>>();

		public static string ComputeSha1Hex(byte[] pbUtf8)
		{
			if(pbUtf8 == null) throw new ArgumentNullException("pbUtf8");

			byte[] pbHash;
			using(SHA1 sha = SHA1.Create())
			{
				pbHash = sha.ComputeHash(pbUtf8);
			}

			StringBuilder sb = new StringBuilder(40);
			foreach(byte b in pbHash) sb.Append(b.ToString("X2"));
			Array.Clear(pbHash, 0, pbHash.Length);
			return sb.ToString();
		}

		/// <summary>
		/// Returns how often the password with the given SHA-1 hash appears in
		/// known breaches (0 = not found). Blocking; call from a worker thread.
		/// </summary>
		public static int GetBreachCount(string strSha1Hex)
		{
			if((strSha1Hex == null) || (strSha1Hex.Length != 40))
				throw new ArgumentException("Invalid SHA-1 hash.");

			string strPrefix = strSha1Hex.Substring(0, 5).ToUpperInvariant();
			string strSuffix = strSha1Hex.Substring(5).ToUpperInvariant();

			Dictionary<string, int> d = null;
			lock(g_oLock)
			{
				g_dCache.TryGetValue(strPrefix, out d);
			}

			if(d == null)
			{
				string strData = Net.DownloadText(RangeUrl + strPrefix, MaxResponseBytes);
				d = ParseRange(strData);

				lock(g_oLock)
				{
					if(g_dCache.Count >= MaxCachedPrefixes) g_dCache.Clear();
					g_dCache[strPrefix] = d;
				}
			}

			int n;
			if(d.TryGetValue(strSuffix, out n)) return n;
			return 0;
		}

		private static Dictionary<string, int> ParseRange(string strData)
		{
			Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.Ordinal);
			if(string.IsNullOrEmpty(strData)) return d;

			string[] vLines = strData.Split(new char[] { '\r', '\n' },
				StringSplitOptions.RemoveEmptyEntries);
			foreach(string strLine in vLines)
			{
				int iSep = strLine.IndexOf(':');
				if(iSep <= 0) continue;

				string strSuffix = strLine.Substring(0, iSep).Trim().ToUpperInvariant();
				int n;
				if(!int.TryParse(strLine.Substring(iSep + 1).Trim(), out n)) continue;
				if(n <= 0) continue; // Padding entries

				d[strSuffix] = n;
			}

			if(vLines.Length > 0 && d.Count == 0)
				throw new FormatException("Unerwartete Antwort von api.pwnedpasswords.com.");

			return d;
		}

		public static string FormatCount(int n)
		{
			return n.ToString("N0", System.Globalization.CultureInfo.CurrentCulture);
		}
	}
}
