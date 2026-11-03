/*
  BetterPass - KeePass plugin
  TOTP (RFC 6238), Base32 (RFC 4648) and otpauth:// URI handling.

  All code in this plugin is C# 5, so that KeePass can compile it as PLGX.
*/

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace BetterPass
{
	internal sealed class OtpAuthData
	{
		public string Secret = string.Empty;
		public int Digits = 0;   // 0 = not specified
		public int Period = 0;   // 0 = not specified
		public string Algorithm = string.Empty;
		public string Issuer = string.Empty;
		public string Account = string.Empty;

		// False if the URI contains something this plugin cannot round-trip
		// (HOTP, Steam encoder, unknown parameters, ...). Such URIs are shown
		// read-only and never rewritten.
		public bool Editable = true;

		// False if no standard TOTP code can be computed (e.g. Steam, HOTP)
		public bool CanGenerate = true;

		public string Note = string.Empty;
	}

	internal static class TotpGenerator
	{
		// Same values that KeePass uses for TimeOtp-Algorithm
		public const string AlgSha1 = "HMAC-SHA-1";
		public const string AlgSha256 = "HMAC-SHA-256";
		public const string AlgSha512 = "HMAC-SHA-512";

		public const int DefaultDigits = 6;
		public const int DefaultPeriod = 30;

		// KeePass' {TIMEOTP} supports 1 to 8 digits
		public const int MinDigits = 1;
		public const int MaxDigits = 8;
		public const int MinPeriod = 1;
		public const int MaxPeriod = 3600;

		public const int MaxSecretChars = 1024;

		private const string B32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
		private static readonly DateTime UnixEpoch =
			new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

		// ===================== Base32 =====================

		private static bool IsIgnoredB32Char(char ch)
		{
			return (char.IsWhiteSpace(ch) || (ch == '-') || (ch == '='));
		}

		// Normalizes user input (spaces, dashes, padding removed, upper case)
		// into ASCII bytes. Works on char arrays so that no extra managed
		// strings with the secret are created.
		public static byte[] NormalizeBase32(char[] vInput)
		{
			if(vInput == null) return new byte[0];

			int n = 0;
			foreach(char ch in vInput)
			{
				if(!IsIgnoredB32Char(ch)) ++n;
			}

			byte[] pb = new byte[n];
			int i = 0;
			foreach(char ch in vInput)
			{
				if(IsIgnoredB32Char(ch)) continue;
				char chU = char.ToUpperInvariant(ch);
				pb[i++] = ((chU < 128) ? (byte)chU : (byte)'?');
			}
			return pb;
		}

		public static string NormalizeBase32(string str)
		{
			if(string.IsNullOrEmpty(str)) return string.Empty;
			byte[] pb = NormalizeBase32(str.ToCharArray());
			string strOut = Encoding.ASCII.GetString(pb);
			Array.Clear(pb, 0, pb.Length);
			return strOut;
		}

		// Input: normalized ASCII bytes. Returns null if invalid.
		public static byte[] DecodeBase32(byte[] pbAscii)
		{
			if((pbAscii == null) || (pbAscii.Length == 0)) return null;

			byte[] pbOut = new byte[(pbAscii.Length * 5) / 8];
			int iOut = 0, nBuffer = 0, nBits = 0;
			foreach(byte b in pbAscii)
			{
				int v = B32Alphabet.IndexOf((char)b);
				if(v < 0) { Array.Clear(pbOut, 0, pbOut.Length); return null; }

				nBuffer = ((nBuffer << 5) | v) & 0xFFFF;
				nBits += 5;
				if(nBits >= 8)
				{
					nBits -= 8;
					if(iOut < pbOut.Length) pbOut[iOut++] = (byte)((nBuffer >> nBits) & 0xFF);
				}
			}

			if(iOut == 0) return null;
			if(iOut == pbOut.Length) return pbOut;

			byte[] pbTrim = new byte[iOut];
			Array.Copy(pbOut, pbTrim, iOut);
			Array.Clear(pbOut, 0, pbOut.Length);
			return pbTrim;
		}

		public static byte[] DecodeBase32(string str)
		{
			if(string.IsNullOrEmpty(str)) return null;
			byte[] pbAscii = NormalizeBase32(str.ToCharArray());
			byte[] pb = DecodeBase32(pbAscii);
			Array.Clear(pbAscii, 0, pbAscii.Length);
			return pb;
		}

		public static string EncodeBase32(byte[] pb)
		{
			if((pb == null) || (pb.Length == 0)) return string.Empty;

			StringBuilder sb = new StringBuilder((pb.Length * 8) / 5 + 1);
			int nBuffer = 0, nBits = 0;
			foreach(byte b in pb)
			{
				nBuffer = ((nBuffer << 8) | b) & 0xFFFF;
				nBits += 8;
				while(nBits >= 5)
				{
					nBits -= 5;
					sb.Append(B32Alphabet[(nBuffer >> nBits) & 0x1F]);
				}
			}
			if(nBits > 0)
				sb.Append(B32Alphabet[(nBuffer << (5 - nBits)) & 0x1F]);

			return sb.ToString();
		}

		public static byte[] DecodeHex(string str)
		{
			if(string.IsNullOrEmpty(str)) return null;

			StringBuilder sb = new StringBuilder();
			foreach(char ch in str)
			{
				if(!char.IsWhiteSpace(ch)) sb.Append(ch);
			}
			string s = sb.ToString();
			if((s.Length == 0) || ((s.Length % 2) != 0)) return null;

			byte[] pb = new byte[s.Length / 2];
			for(int i = 0; i < pb.Length; ++i)
			{
				int hi = HexValue(s[i * 2]), lo = HexValue(s[i * 2 + 1]);
				if((hi < 0) || (lo < 0)) return null;
				pb[i] = (byte)((hi << 4) | lo);
			}
			return pb;
		}

		private static int HexValue(char ch)
		{
			if((ch >= '0') && (ch <= '9')) return ch - '0';
			if((ch >= 'a') && (ch <= 'f')) return ch - 'a' + 10;
			if((ch >= 'A') && (ch <= 'F')) return ch - 'A' + 10;
			return -1;
		}

		// ===================== Algorithms =====================

		// Returns null for unknown algorithms
		public static string TryNormalizeAlgorithm(string str)
		{
			if(string.IsNullOrEmpty(str)) return AlgSha1;

			string s = str.Trim().ToUpperInvariant().Replace("HMAC", string.Empty).Replace(
				"-", string.Empty).Replace("_", string.Empty);
			if((s == "SHA1") || (s.Length == 0)) return AlgSha1;
			if(s == "SHA256") return AlgSha256;
			if(s == "SHA512") return AlgSha512;
			return null;
		}

		public static string NormalizeAlgorithm(string str)
		{
			string s = TryNormalizeAlgorithm(str);
			return (s ?? AlgSha1);
		}

		public static string AlgorithmToUriForm(string strAlg)
		{
			string s = NormalizeAlgorithm(strAlg);
			if(s == AlgSha256) return "SHA256";
			if(s == AlgSha512) return "SHA512";
			return "SHA1";
		}

		// ===================== Code generation =====================

		public static string Generate(byte[] pbSecret, int nDigits, int nPeriod,
			string strAlg, DateTime dtUtc, out int nSecondsRemaining)
		{
			if((pbSecret == null) || (pbSecret.Length == 0))
				throw new ArgumentException("Secret is empty.");
			if((nDigits < 1) || (nDigits > 10)) nDigits = DefaultDigits;
			if(nPeriod < 1) nPeriod = DefaultPeriod;

			long lUnix = (long)Math.Floor((dtUtc - UnixEpoch).TotalSeconds);
			long lCounter = lUnix / nPeriod;
			nSecondsRemaining = nPeriod - (int)(lUnix % nPeriod);

			byte[] pbMsg = new byte[8];
			for(int i = 7; i >= 0; --i)
			{
				pbMsg[i] = (byte)(lCounter & 0xFF);
				lCounter >>= 8;
			}

			byte[] pbHash;
			string strA = NormalizeAlgorithm(strAlg);
			HMAC h;
			if(strA == AlgSha256) h = new HMACSHA256(pbSecret);
			else if(strA == AlgSha512) h = new HMACSHA512(pbSecret);
			else h = new HMACSHA1(pbSecret);
			try { pbHash = h.ComputeHash(pbMsg); }
			finally { h.Clear(); }

			int o = pbHash[pbHash.Length - 1] & 0x0F;
			long lBin = ((long)(pbHash[o] & 0x7F) << 24) |
				((long)pbHash[o + 1] << 16) |
				((long)pbHash[o + 2] << 8) |
				(long)pbHash[o + 3];
			Array.Clear(pbHash, 0, pbHash.Length);

			long lMod = 1;
			for(int i = 0; i < nDigits; ++i) lMod *= 10;

			return (lBin % lMod).ToString().PadLeft(nDigits, '0');
		}

		// "123456" -> "123 456", "12345678" -> "1234 5678"
		/// <summary>
		/// Digit groups for display ("123456" -> "123", "456"). The groups are
		/// drawn with a separator; the code itself never contains one.
		/// </summary>
		public static string[] SplitCode(string str)
		{
			if(string.IsNullOrEmpty(str)) return new string[0];
			if(str.Length < 6) return new string[] { str };
			int h = (str.Length + 1) / 2;
			return new string[] { str.Substring(0, h), str.Substring(h) };
		}

		// ===================== otpauth:// =====================

		public static bool IsOtpAuthUri(char[] v)
		{
			const string strPrefix = "otpauth://";
			if((v == null) || (v.Length < strPrefix.Length)) return false;

			int iStart = 0;
			while((iStart < v.Length) && char.IsWhiteSpace(v[iStart])) ++iStart;
			if(v.Length - iStart < strPrefix.Length) return false;

			for(int i = 0; i < strPrefix.Length; ++i)
			{
				if(char.ToLowerInvariant(v[iStart + i]) != strPrefix[i]) return false;
			}
			return true;
		}

		// Returns null if the string is not an otpauth:// URI at all
		public static OtpAuthData ParseOtpAuth(string strUri)
		{
			if(string.IsNullOrEmpty(strUri)) return null;
			string s = strUri.Trim();
			const string strScheme = "otpauth://";
			if(!s.StartsWith(strScheme, StringComparison.OrdinalIgnoreCase)) return null;

			OtpAuthData d = new OtpAuthData();
			try
			{
				string strRest = s.Substring(strScheme.Length);
				int iSlash = strRest.IndexOf('/');
				string strType = ((iSlash >= 0) ? strRest.Substring(0, iSlash) : strRest);
				strRest = ((iSlash >= 0) ? strRest.Substring(iSlash + 1) : string.Empty);

				if(!strType.Equals("totp", StringComparison.OrdinalIgnoreCase))
				{
					d.Editable = false;
					d.CanGenerate = false;
					d.Note = "Typ \"" + strType + "\" wird nicht unterst\u00fctzt";
				}

				int iQ = strRest.IndexOf('?');
				string strLabel = ((iQ >= 0) ? strRest.Substring(0, iQ) : strRest);
				string strQuery = ((iQ >= 0) ? strRest.Substring(iQ + 1) : string.Empty);

				strLabel = Unescape(strLabel).Trim();
				int iSep = strLabel.IndexOf(':');
				if(iSep >= 0)
				{
					d.Issuer = strLabel.Substring(0, iSep).Trim();
					d.Account = strLabel.Substring(iSep + 1).Trim();
				}
				else d.Account = strLabel;

				foreach(string strPair in strQuery.Split('&'))
				{
					if(strPair.Length == 0) continue;
					int iEq = strPair.IndexOf('=');
					string strKey = Unescape((iEq >= 0) ? strPair.Substring(0, iEq) : strPair);
					string strValue = ((iEq >= 0) ? Unescape(strPair.Substring(iEq + 1)) : string.Empty);
					strKey = strKey.Trim().ToLowerInvariant();

					int n;
					if(strKey == "secret") d.Secret = NormalizeBase32(strValue);
					else if(strKey == "issuer")
					{
						if(strValue.Trim().Length > 0) d.Issuer = strValue.Trim();
					}
					else if(strKey == "digits")
					{
						if(int.TryParse(strValue, out n)) d.Digits = n;
						else MarkReadOnly(d, "Ung\u00fcltige Stellenzahl");
					}
					else if(strKey == "period")
					{
						if(int.TryParse(strValue, out n)) d.Period = n;
						else MarkReadOnly(d, "Ung\u00fcltige Periode");
					}
					else if(strKey == "algorithm")
					{
						string strAlg = TryNormalizeAlgorithm(strValue);
						if(strAlg != null) d.Algorithm = strAlg;
						else
						{
							d.CanGenerate = false;
							MarkReadOnly(d, "Algorithmus \"" + strValue + "\" wird nicht unterst\u00fctzt");
						}
					}
					else if(strKey == "encoder")
					{
						// e.g. Steam: different code alphabet
						d.CanGenerate = false;
						MarkReadOnly(d, "Encoder \"" + strValue + "\" wird nicht unterst\u00fctzt");
					}
					else MarkReadOnly(d, "Unbekannter Parameter \"" + strKey + "\"");
				}
			}
			catch(Exception)
			{
				d.Editable = false;
				d.CanGenerate = false;
				d.Note = "otpauth-Link ist ung\u00fcltig";
				return d;
			}

			if(d.Secret.Length == 0)
			{
				d.CanGenerate = false;
				MarkReadOnly(d, "Kein Secret im otpauth-Link");
			}
			if((d.Digits != 0) && ((d.Digits < MinDigits) || (d.Digits > MaxDigits)))
				MarkReadOnly(d, "Stellenzahl au\u00dferhalb von " + MinDigits + "-" + MaxDigits);
			if((d.Period != 0) && ((d.Period < MinPeriod) || (d.Period > MaxPeriod)))
				MarkReadOnly(d, "Periode au\u00dferhalb von " + MinPeriod + "-" + MaxPeriod);
			if(d.Secret.Length > MaxSecretChars)
			{
				d.CanGenerate = false;
				MarkReadOnly(d, "Secret ist zu lang");
			}

			return d;
		}

		private static void MarkReadOnly(OtpAuthData d, string strNote)
		{
			d.Editable = false;
			if(d.Note.Length == 0) d.Note = strNote;
		}

		public static string BuildOtpAuth(string strSecret, int nDigits, int nPeriod,
			string strAlg, string strIssuer, string strAccount)
		{
			string strIss = (strIssuer ?? string.Empty).Trim();
			string strAcc = (strAccount ?? string.Empty).Trim();
			if(strAcc.Length == 0) strAcc = "KeePass";

			string strLabel = ((strIss.Length > 0) ? (Uri.EscapeDataString(strIss) +
				":") : string.Empty) + Uri.EscapeDataString(strAcc);

			StringBuilder sb = new StringBuilder();
			sb.Append("otpauth://totp/");
			sb.Append(strLabel);
			sb.Append("?secret=");
			sb.Append(NormalizeBase32(strSecret));
			if(strIss.Length > 0)
			{
				sb.Append("&issuer=");
				sb.Append(Uri.EscapeDataString(strIss));
			}
			sb.Append("&period=");
			sb.Append(nPeriod.ToString());
			sb.Append("&digits=");
			sb.Append(nDigits.ToString());
			sb.Append("&algorithm=");
			sb.Append(AlgorithmToUriForm(strAlg));
			return sb.ToString();
		}

		private static string Unescape(string str)
		{
			if(string.IsNullOrEmpty(str)) return string.Empty;
			return Uri.UnescapeDataString(str.Replace('+', ' '));
		}
	}
}
