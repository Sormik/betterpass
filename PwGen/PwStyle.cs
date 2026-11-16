/*
  BetterPass - KeePass plugin
  Settings of the syllable password generator (Apple style), e.g.
  "hupvEw-bawpo6-xisqof":
  - groups of pronounceable letters (consonant-vowel-consonant-
    consonant-vowel-consonant ...), joined by a separator
  - a fixed number of digits, placed at group edges first
  - a fixed number of uppercase letters at random positions
  - no "l", so nothing can be confused with "1" or "I"

  The settings are stored in KeePass' own password generator profile
  (PwProfile.CustomAlgorithmOptions), so they can be saved, renamed and
  chosen like every other KeePass profile.
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using KeePassLib.Cryptography;

namespace BetterPass
{
	internal sealed class PwStyle
	{
		internal const string Consonants = "bcdfghjkmnpqrstvwxz";
		internal const string Vowels = "aeiouy";
		internal const string DigitChars = "0123456789";

		// Limits (also used by the settings dialog)
		public const int MinGroups = 1, MaxGroups = 8;
		public const int MinGroupLength = 3, MaxGroupLength = 12;
		public const int MaxSeparatorLength = 3;

		// Defaults = Apple (Safari, iCloud Keychain)
		public int Groups = 3;
		public int GroupLength = 6;
		public string Separator = "-";
		public int UpperCount = 1;
		public int DigitCount = 1;

		public PwStyle CloneDeep()
		{
			return (PwStyle)this.MemberwiseClone(); // Only value types and immutable strings
		}

		public bool IsAppleDefault
		{
			get { return Serialize() == new PwStyle().Serialize(); }
		}

		public int LetterCount
		{
			get { return (this.Groups * this.GroupLength); }
		}

		public int TotalLength
		{
			get { return LetterCount + ((this.Groups - 1) * this.Separator.Length); }
		}

		// ===================== Serialization =====================
		// "v=1;groups=3;glen=6;sep=-;upper=1;digits=1" (values URL-encoded)

		public string Serialize()
		{
			StringBuilder sb = new StringBuilder("v=1");
			Append(sb, "groups", this.Groups.ToString(CultureInfo.InvariantCulture));
			Append(sb, "glen", this.GroupLength.ToString(CultureInfo.InvariantCulture));
			Append(sb, "sep", this.Separator);
			Append(sb, "upper", this.UpperCount.ToString(CultureInfo.InvariantCulture));
			Append(sb, "digits", this.DigitCount.ToString(CultureInfo.InvariantCulture));
			return sb.ToString();
		}

		private static void Append(StringBuilder sb, string strKey, string strValue)
		{
			sb.Append(';');
			sb.Append(strKey);
			sb.Append('=');
			sb.Append(Uri.EscapeDataString(strValue ?? string.Empty));
		}

		/// <summary>
		/// Never throws; empty or broken input gives the Apple defaults.
		/// </summary>
		public static PwStyle Parse(string str)
		{
			PwStyle s = new PwStyle();
			if(string.IsNullOrEmpty(str)) return s;

			Dictionary<string, string> d = new Dictionary<string, string>();
			foreach(string strPart in str.Split(';'))
			{
				int i = strPart.IndexOf('=');
				if(i <= 0) continue;
				try { d[strPart.Substring(0, i).Trim()] = Uri.UnescapeDataString(strPart.Substring(i + 1)); }
				catch(Exception) { }
			}

			s.Groups = GetInt(d, "groups", s.Groups);
			s.GroupLength = GetInt(d, "glen", s.GroupLength);
			string strSep;
			if(d.TryGetValue("sep", out strSep)) s.Separator = strSep;
			s.UpperCount = GetInt(d, "upper", s.UpperCount);
			s.DigitCount = GetInt(d, "digits", s.DigitCount);
			s.Normalize();
			return s;
		}

		private static int GetInt(Dictionary<string, string> d, string strKey, int nDefault)
		{
			string str;
			int n;
			if(d.TryGetValue(strKey, out str) && int.TryParse(str, NumberStyles.Integer,
				CultureInfo.InvariantCulture, out n)) return n;
			return nDefault;
		}

		/// <summary>
		/// Clamps all values into valid ranges.
		/// </summary>
		public void Normalize()
		{
			this.Groups = Clamp(this.Groups, MinGroups, MaxGroups);
			this.GroupLength = Clamp(this.GroupLength, MinGroupLength, MaxGroupLength);

			StringBuilder sb = new StringBuilder();
			foreach(char ch in (this.Separator ?? string.Empty))
			{
				if(char.IsControl(ch) || char.IsLetterOrDigit(ch)) continue;
				if(sb.Length < MaxSeparatorLength) sb.Append(ch);
			}
			this.Separator = sb.ToString();

			this.DigitCount = Clamp(this.DigitCount, 0, LetterCount);
			this.UpperCount = Clamp(this.UpperCount, 0, LetterCount - this.DigitCount);
		}

		private static int Clamp(int n, int nMin, int nMax)
		{
			if(nMax < nMin) nMax = nMin;
			return Math.Min(Math.Max(n, nMin), nMax);
		}

		// ===================== Generation =====================

		public char[] Generate(CryptoRandomStream crs)
		{
			if(crs == null) throw new ArgumentNullException("crs");

			PwStyle s = this.CloneDeep();
			s.Normalize();

			int nLetters = s.LetterCount;
			char[] v = new char[s.TotalLength];
			int[] vPos = new int[nLetters]; // Index of each letter in v
			List<int> lEdges = new List<int>(); // Letters at group start / end

			int iOut = 0;
			for(int g = 0; g < s.Groups; ++g)
			{
				if(g > 0)
				{
					foreach(char chSep in s.Separator) v[iOut++] = chSep;
				}
				for(int i = 0; i < s.GroupLength; ++i)
				{
					string strSet = (IsVowelSlot(i) ? Vowels : Consonants);
					int iLetter = (g * s.GroupLength) + i;
					vPos[iLetter] = iOut;
					v[iOut++] = strSet[NextIndex(crs, strSet.Length)];
					if((i == 0) || (i == (s.GroupLength - 1))) lEdges.Add(iLetter);
				}
			}

			// Digits: at group edges first (like Apple), then anywhere
			bool[] vUsed = new bool[nLetters];
			List<int> lCand = new List<int>(lEdges);
			for(int n = 0; n < s.DigitCount; ++n)
			{
				if(lCand.Count == 0)
				{
					for(int i = 0; i < nLetters; ++i) { if(!vUsed[i]) lCand.Add(i); }
				}
				int iLetter = TakeRandom(crs, lCand);
				vUsed[iLetter] = true;
				v[vPos[iLetter]] = DigitChars[NextIndex(crs, DigitChars.Length)];
			}

			// Uppercase: any remaining letter
			lCand.Clear();
			for(int i = 0; i < nLetters; ++i) { if(!vUsed[i]) lCand.Add(i); }
			for(int n = 0; n < s.UpperCount; ++n)
			{
				int iLetter = TakeRandom(crs, lCand);
				v[vPos[iLetter]] = char.ToUpperInvariant(v[vPos[iLetter]]);
			}

			return v;
		}

		// Pattern C V C C V C C V C ...: easy to read and to type on a phone
		private static bool IsVowelSlot(int i)
		{
			return ((i % 3) == 1);
		}

		// ===================== Strength =====================

		/// <summary>
		/// Approximate strength in bits. Apple style: about 72 bits.
		/// </summary>
		public double EstimateBits()
		{
			PwStyle s = this.CloneDeep();
			s.Normalize();

			double d = 0.0;
			for(int i = 0; i < s.GroupLength; ++i)
				d += Log2(IsVowelSlot(i) ? Vowels.Length : Consonants.Length);
			d *= s.Groups;

			// A digit replaces a letter (mostly a consonant at a group edge):
			// lose that letter, gain its position and the digit
			int nLetters = s.LetterCount;
			int nEdges = s.Groups * 2;
			for(int i = 0; i < s.DigitCount; ++i)
			{
				int nPos = ((i < nEdges) ? (nEdges - i) : (nLetters - i));
				d += Log2(nPos) - Log2(Consonants.Length) + Log2(DigitChars.Length);
			}
			d -= LogFactorial(s.DigitCount); // Positions are a set, not a sequence

			d += LogBinomial(nLetters - s.DigitCount, s.UpperCount);
			return Math.Max(0.0, d);
		}

		private static double Log2(double d)
		{
			return ((d <= 1.0) ? 0.0 : (Math.Log(d) / Math.Log(2.0)));
		}

		private static double LogFactorial(int n)
		{
			double d = 0.0;
			for(int i = 2; i <= n; ++i) d += Log2(i);
			return d;
		}

		private static double LogBinomial(int n, int k)
		{
			if((k < 0) || (k > n)) return 0.0;
			return (LogFactorial(n) - LogFactorial(k) - LogFactorial(n - k));
		}

		// ===================== Random helpers =====================

		private static int TakeRandom(CryptoRandomStream crs, List<int> l)
		{
			int i = NextIndex(crs, l.Count);
			int n = l[i];
			l.RemoveAt(i);
			return n;
		}

		// Uniform random index in [0, n), rejection sampling (no modulo bias)
		internal static int NextIndex(CryptoRandomStream crs, int n)
		{
			if(n <= 0) throw new ArgumentOutOfRangeException("n");
			ulong uN = (ulong)n;
			ulong uLimit = ulong.MaxValue - (ulong.MaxValue % uN);
			while(true)
			{
				ulong u = crs.GetRandomUInt64();
				if(u < uLimit) return (int)(u % uN);
			}
		}
	}
}
