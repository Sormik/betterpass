/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Syllable password generator (Apple style), registered through KeePass'
  standard interface for custom password generators. In KeePass' password
  generator dialog it appears under "Generate using custom algorithm";
  the "Options" button there opens PwStyleForm.
*/

using System;

using KeePass.Plugins;

using KeePassLib;
using KeePassLib.Cryptography;
using KeePassLib.Cryptography.PasswordGenerator;
using KeePassLib.Security;

namespace BetterPass
{
	internal sealed class BpPwGenerator : CustomPwGenerator
	{
		// Fixed, never change: profiles reference the generator by this UUID
		private static readonly PwUuid g_uuid = new PwUuid(new byte[] {
			0x7A, 0x41, 0x2E, 0x93, 0xB5, 0x0C, 0x4D, 0x18,
			0x9E, 0x63, 0xA1, 0xF4, 0x52, 0xD7, 0x0B, 0x86 });

		public static PwUuid GeneratorUuid
		{
			get { return g_uuid; }
		}

		/// <summary>
		/// Value of PwProfile.CustomAlgorithmUuid for this generator.
		/// </summary>
		public static string GeneratorUuidString
		{
			get { return Convert.ToBase64String(g_uuid.UuidBytes); }
		}

		public override PwUuid Uuid
		{
			get { return g_uuid; }
		}

		public override string Name
		{
			get { return "BetterPass: Silben-Gruppen (Apple-Stil)"; }
		}

		public override bool SupportsOptions
		{
			get { return true; }
		}

		public override ProtectedString Generate(PwProfile prf, CryptoRandomStream crsRandomSource)
		{
			if(crsRandomSource == null) throw new ArgumentNullException("crsRandomSource");

			PwStyle s = PwStyle.Parse((prf != null) ? prf.CustomAlgorithmOptions : null);
			char[] v = s.Generate(crsRandomSource);
			ProtectedString ps = new ProtectedString(true, new string(v));
			Array.Clear(v, 0, v.Length);
			return ps;
		}

		public override string GetOptions(string strCurrentOptions)
		{
			PwStyle s = PwStyleForm.Edit(null, PwStyle.Parse(strCurrentOptions));
			return ((s != null) ? s.Serialize() : strCurrentOptions);
		}

		// ===================== Registration =====================

		private static BpPwGenerator g_inst = null;

		public static void Register(IPluginHost host)
		{
			if((host == null) || (g_inst != null)) return;

			g_inst = new BpPwGenerator();
			host.PwGeneratorPool.Add(g_inst);
		}

		public static void Unregister(IPluginHost host)
		{
			if((host == null) || (g_inst == null)) return;

			host.PwGeneratorPool.Remove(g_uuid);
			g_inst = null;
		}
	}
}
