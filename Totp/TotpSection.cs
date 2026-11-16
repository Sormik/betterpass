/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  TOTP rows in the entry dialog: code (with countdown), key, digits, period,
  algorithm and issuer.

  Storage: KeePass' native OTP fields, so {TIMEOTP} works without the plugin:
    TimeOtp-Secret-Base32, TimeOtp-Length, TimeOtp-Period, TimeOtp-Algorithm
  plus TimeOtp-Issuer. A KeePassXC-style "otp" field is used as fallback and
  only rewritten if it was fully understood when the dialog was opened.
*/

using System;
using System.Drawing;
using System.Windows.Forms;

using KeePass.Forms;
using KeePass.UI;
using KeePass.Util;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;

namespace BetterPass
{
	internal sealed class TotpSection
	{
		private const string FldPrefix = "TimeOtp-";
		private const string FldSecret = FldPrefix + "Secret";
		private const string FldSecretHex = FldPrefix + "Secret-Hex";
		private const string FldSecretB32 = FldPrefix + "Secret-Base32";
		private const string FldSecretB64 = FldPrefix + "Secret-Base64";
		private const string FldLength = FldPrefix + "Length";
		private const string FldPeriod = FldPrefix + "Period";
		private const string FldAlg = FldPrefix + "Algorithm";
		private const string FldIssuer = FldPrefix + "Issuer";
		private const string FldOtpUri = "otp"; // KeePassXC, KeePassDX, Keepass2Android

		private readonly EntryFormLayout m_layout;
		private readonly PwEntryForm m_form;
		private Control m_tbTitle;
		private Control m_tbUserName;

		private const string TipCopy = "Leertaste, Strg+C oder Doppelklick kopiert den Code";

		private TotpCodeView m_view;
		private SecureTextBoxEx m_tbKey;
		private Button m_btnShowKey;
		private NumericUpDown m_nudDigits;
		private NumericUpDown m_nudPeriod;
		private ComboBox m_cmbAlg;
		private TextBox m_tbIssuer;
		private ToolTip m_tt;
		private Timer m_timer;

		private bool m_bLoading = false;
		private bool m_bViewOnly = false;     // Entry opened read-only
		private bool m_bLocked = false;       // Data in a format we must not rewrite
		private string m_strLockNote = string.Empty;
		private bool m_bOtpUriManaged = false; // "otp" field fully understood
		private bool m_bCanGenerate = true;
		private bool m_bListDirty = false;

		private ProtectedBinary m_pbSecret = null; // Decoded secret (encrypted in memory)
		private bool m_bSecretInvalid = false;
		private string m_strCurrentCode = string.Empty;

		private TotpSection(EntryFormLayout l)
		{
			m_layout = l;
			m_form = l.Form;
		}

		public static TotpSection Attach(EntryFormLayout l)
		{
			if(l == null) return null;
			TotpSection s = new TotpSection(l);
			s.Create();
			return s;
		}

		// ===================== UI =====================

		private void Create()
		{
			EntryFormLayout l = m_layout;
			m_tbTitle = EntryFormLayout.FindControl<Control>(m_form, "m_tbTitle");
			m_tbUserName = EntryFormLayout.FindControl<Control>(m_form, "m_tbUserName");
			m_bViewOnly = (m_form.EditModeEx == PwEditMode.ViewReadOnlyEntry);

			int y = l.UrlBox.Top + l.Pitch;
			int nTab = l.InsertRows(y, 5);

			Font fBase = l.TabEntry.Font;
			int nTbH = l.TextBoxHeight;
			m_tt = new ToolTip();

			// ----- Rows 1-2: code card -----
			int nCardH = l.Pitch + nTbH;
			l.AddLabel("TOTP Code:", l.LabelX, y + (nCardH - nTbH) / 2);

			m_view = new TotpCodeView();
			m_view.Font = fBase;
			m_view.Bounds = new Rectangle(l.FieldX, y, l.RightX - l.FieldX, nCardH);
			m_view.TabIndex = nTab++;
			m_view.CopyRequested += this.OnCopyRequested;
			l.Add(m_view);
			m_tt.SetToolTip(m_view, TipCopy);
			y += 2 * l.Pitch;

			// ----- Row 3: key -----
			l.AddLabel("TOTP Key:", l.LabelX, y);

			m_tbKey = new SecureTextBoxEx();
			m_tbKey.Bounds = new Rectangle(l.FieldX, y, l.ButtonX - l.Gap - l.FieldX, nTbH);
			m_tbKey.MaxLength = TotpGenerator.MaxSecretChars + 512; // room for otpauth links
			m_tbKey.TabIndex = nTab++;
			m_tbKey.EnableProtection(BpConfig.TotpHideKey);
			m_tbKey.TextChanged += this.OnKeyTextChanged;
			l.Add(m_tbKey);
			m_tt.SetToolTip(m_tbKey, "Base32-Secret oder kompletter otpauth://-Link");

			// Same look as KeePass' own show/hide button of the password row
			m_btnShowKey = new Button();
			m_btnShowKey.Name = "bp_btnShowTotpKey"; // Gets the modern eye icon (IconSet)
			CheckBox cbHidePw = EntryFormLayout.FindControl<CheckBox>(m_form, "m_cbHidePassword");
			if((cbHidePw != null) && (cbHidePw.Image != null)) m_btnShowKey.Image = cbHidePw.Image;
			else m_btnShowKey.Text = "\u2022\u2022\u2022";
			m_btnShowKey.Bounds = new Rectangle(l.ButtonX, y + (nTbH - l.ButtonHeight) / 2,
				l.ButtonWidth, l.ButtonHeight);
			m_btnShowKey.UseVisualStyleBackColor = true;
			m_btnShowKey.TabIndex = nTab++;
			m_btnShowKey.Click += this.OnShowKeyClick;
			l.Add(m_btnShowKey);
			m_tt.SetToolTip(m_btnShowKey, "Key anzeigen/verbergen");
			y += l.Pitch;

			// ----- Row 3: digits, period, algorithm -----
			l.AddLabel("Digits:", l.LabelX, y);
			int wPeriodLbl = TextRenderer.MeasureText("Period:", fBase).Width;
			int wAlgLbl = TextRenderer.MeasureText("Algorithm:", fBase).Width;
			int nAvail = l.RightX - l.FieldX;
			int nGapBig = l.Gap * 2, nGapSmall = Math.Max(2, l.Gap / 2);
			int wNud = (int)((nAvail - wPeriodLbl - wAlgLbl - 2 * nGapBig - 2 * nGapSmall) / 3.6);
			if(wNud < 40) wNud = 40;

			int x = l.FieldX;
			m_nudDigits = CreateNud(x, y, wNud, nTbH, TotpGenerator.MinDigits,
				TotpGenerator.MaxDigits, TotpGenerator.DefaultDigits, nTab++);
			x += wNud + nGapBig;

			l.AddLabel("Period:", x, y);
			x += wPeriodLbl + nGapSmall;
			m_nudPeriod = CreateNud(x, y, wNud, nTbH, TotpGenerator.MinPeriod,
				TotpGenerator.MaxPeriod, TotpGenerator.DefaultPeriod, nTab++);
			x += wNud + nGapBig;

			l.AddLabel("Algorithm:", x, y);
			x += wAlgLbl + nGapSmall;
			m_cmbAlg = new ComboBox();
			m_cmbAlg.DropDownStyle = ComboBoxStyle.DropDownList;
			m_cmbAlg.Items.AddRange(new object[] { TotpGenerator.AlgSha1,
				TotpGenerator.AlgSha256, TotpGenerator.AlgSha512 });
			m_cmbAlg.SelectedIndex = 0;
			m_cmbAlg.Bounds = new Rectangle(x, y, Math.Max(60, l.RightX - x), nTbH);
			m_cmbAlg.TabIndex = nTab++;
			m_cmbAlg.SelectedIndexChanged += this.OnValueChanged;
			l.Add(m_cmbAlg);
			y += l.Pitch;

			// ----- Row 4: issuer -----
			l.AddLabel("Issuer:", l.LabelX, y);
			m_tbIssuer = new TextBox();
			m_tbIssuer.Bounds = new Rectangle(l.FieldX, y, l.RightX - l.FieldX, nTbH);
			m_tbIssuer.MaxLength = 256;
			m_tbIssuer.TabIndex = nTab++;
			m_tbIssuer.TextChanged += this.OnValueChanged;
			l.Add(m_tbIssuer);

			LoadFromStrings();

			l.TabMain.SelectedIndexChanged += this.OnTabMainSelectedIndexChanged;
			m_form.FormClosed += this.OnFormClosed;
			m_form.Disposed += this.OnFormDisposed;

			m_timer = new Timer();
			m_timer.Interval = 250;
			m_timer.Tick += this.OnTimerTick;
			m_timer.Start();
		}

		private NumericUpDown CreateNud(int x, int y, int w, int h, int nMin, int nMax,
			int nValue, int nTab)
		{
			NumericUpDown nud = new NumericUpDown();
			nud.Minimum = nMin;
			nud.Maximum = nMax;
			nud.Value = nValue;
			nud.Bounds = new Rectangle(x, y, w, h);
			nud.TabIndex = nTab;
			nud.ValueChanged += this.OnValueChanged;
			m_layout.Add(nud);
			return nud;
		}

		private void UpdateEditState()
		{
			bool bRO = (m_bViewOnly || m_bLocked);
			m_tbKey.ReadOnly = bRO;
			m_nudDigits.Enabled = !bRO;
			m_nudPeriod.Enabled = !bRO;
			m_cmbAlg.Enabled = !bRO;
			m_tbIssuer.ReadOnly = bRO;

			string strTip = TipCopy;
			if(m_bLocked)
				strTip = "Nur Anzeige: " + m_strLockNote + ".\r\nBearbeitung im Tab \"Advanced\".";
			m_tt.SetToolTip(m_view, strTip);
			m_tt.SetToolTip(m_tbKey, (m_bLocked ? strTip : "Base32-Secret oder kompletter otpauth://-Link"));
		}

		// ===================== Load =====================

		private void LoadFromStrings()
		{
			ProtectedStringDictionary d = m_form.EntryStrings;
			if(d == null) return;

			m_bLocked = false;
			m_strLockNote = string.Empty;
			m_bOtpUriManaged = false;
			m_bCanGenerate = true;

			int nDigits = TotpGenerator.DefaultDigits;
			int nPeriod = TotpGenerator.DefaultPeriod;
			string strAlg = TotpGenerator.AlgSha1;
			string strIssuer = d.ReadSafe(FldIssuer);
			byte[] pbSecret = null;     // decoded
			string strDisplayB32 = null; // Base32 text for the key box

			// Same precedence as KeePass' {TIMEOTP}
			bool bNative = (d.Exists(FldSecret) || d.Exists(FldSecretHex) ||
				d.Exists(FldSecretB32) || d.Exists(FldSecretB64));

			if(bNative)
			{
				pbSecret = ReadNativeSecret(d);
				if((pbSecret == null) || (pbSecret.Length == 0))
				{
					// An invalid Base32 secret (typo) stays editable here;
					// other formats are only shown
					bool bB32Only = d.Exists(FldSecretB32) && d.GetSafe(FldSecret).IsEmpty &&
						d.GetSafe(FldSecretHex).IsEmpty;
					if(!bB32Only)
					{
						m_bCanGenerate = false;
						Lock("Das gespeicherte Secret ist ung\u00fcltig");
					}
				}
				else if(!d.Exists(FldSecretB32))
				{
					// Other native variant; shown as Base32
					strDisplayB32 = TotpGenerator.EncodeBase32(pbSecret);
				}

				string strLen = d.ReadSafe(FldLength).Trim();
				if(strLen.Length > 0)
				{
					int n;
					if(int.TryParse(strLen, out n) && (n >= TotpGenerator.MinDigits) &&
						(n <= TotpGenerator.MaxDigits)) nDigits = n;
					else Lock("TimeOtp-Length \"" + strLen + "\" wird nicht unterst\u00fctzt");
				}

				string strPer = d.ReadSafe(FldPeriod).Trim();
				if(strPer.Length > 0)
				{
					int n;
					if(int.TryParse(strPer, out n) && (n >= TotpGenerator.MinPeriod) &&
						(n <= TotpGenerator.MaxPeriod)) nPeriod = n;
					else Lock("TimeOtp-Period \"" + strPer + "\" wird nicht unterst\u00fctzt");
				}

				string strAlgRaw = d.ReadSafe(FldAlg);
				string strAlgN = TotpGenerator.TryNormalizeAlgorithm(strAlgRaw);
				if(strAlgN != null) strAlg = strAlgN;
				else
				{
					m_bCanGenerate = false;
					Lock("TimeOtp-Algorithm \"" + strAlgRaw + "\" wird nicht unterst\u00fctzt");
				}

				// An additional "otp" field is only kept in sync if fully understood
				if(d.Exists(FldOtpUri))
				{
					OtpAuthData oaX = TotpGenerator.ParseOtpAuth(d.ReadSafe(FldOtpUri));
					m_bOtpUriManaged = ((oaX != null) && oaX.Editable);
				}
			}
			else if(d.Exists(FldOtpUri))
			{
				OtpAuthData oa = TotpGenerator.ParseOtpAuth(d.ReadSafe(FldOtpUri));
				if(oa == null)
				{
					m_bCanGenerate = false;
					Lock("Das Feld \"otp\" hat ein unbekanntes Format");
				}
				else
				{
					pbSecret = TotpGenerator.DecodeBase32(oa.Secret);
					strDisplayB32 = oa.Secret;
					if(oa.Digits > 0) nDigits = oa.Digits;
					if(oa.Period > 0) nPeriod = oa.Period;
					if(oa.Algorithm.Length > 0) strAlg = oa.Algorithm;
					strIssuer = oa.Issuer;
					m_bCanGenerate = (oa.CanGenerate && (pbSecret != null));

					if(!oa.Editable) Lock(oa.Note);
					else m_bOtpUriManaged = true;
				}
			}

			m_bLoading = true;
			try
			{
				if(bNative && d.Exists(FldSecretB32) && (strDisplayB32 == null))
					m_tbKey.TextEx = d.Get(FldSecretB32);
				else if(!string.IsNullOrEmpty(strDisplayB32))
					m_tbKey.TextEx = new ProtectedString(true, strDisplayB32);
				else m_tbKey.TextEx = ProtectedString.Empty;

				m_nudDigits.Value = Clamp(nDigits, m_nudDigits);
				m_nudPeriod.Value = Clamp(nPeriod, m_nudPeriod);
				int iAlg = m_cmbAlg.Items.IndexOf(strAlg);
				m_cmbAlg.SelectedIndex = ((iAlg >= 0) ? iAlg : 0);
				if(m_tbIssuer.Text != strIssuer) m_tbIssuer.Text = strIssuer;
			}
			finally { m_bLoading = false; }

			SetSecretCache(pbSecret, (pbSecret == null) && (bNative || d.Exists(FldOtpUri)));
			if(pbSecret != null) Array.Clear(pbSecret, 0, pbSecret.Length);

			UpdateEditState();
			UpdateCode();
		}

		private void Lock(string strNote)
		{
			m_bLocked = true;
			if(m_strLockNote.Length == 0) m_strLockNote = (strNote ?? string.Empty);
		}

		private static byte[] ReadNativeSecret(ProtectedStringDictionary d)
		{
			try
			{
				ProtectedString ps = d.Get(FldSecret);
				if((ps != null) && !ps.IsEmpty) return ps.ReadUtf8();

				ps = d.Get(FldSecretHex);
				if((ps != null) && !ps.IsEmpty) return TotpGenerator.DecodeHex(ps.ReadString());

				ps = d.Get(FldSecretB32);
				if((ps != null) && !ps.IsEmpty)
				{
					char[] v = ps.ReadChars();
					byte[] pbAscii = TotpGenerator.NormalizeBase32(v);
					Array.Clear(v, 0, v.Length);
					byte[] pb = TotpGenerator.DecodeBase32(pbAscii);
					Array.Clear(pbAscii, 0, pbAscii.Length);
					return pb;
				}

				ps = d.Get(FldSecretB64);
				if((ps != null) && !ps.IsEmpty)
					return Convert.FromBase64String(ps.ReadString().Trim());
			}
			catch(Exception) { }

			return null;
		}

		private void SetSecretCache(byte[] pbSecret, bool bInvalid)
		{
			m_pbSecret = (((pbSecret != null) && (pbSecret.Length > 0)) ?
				new ProtectedBinary(true, pbSecret) : null);
			m_bSecretInvalid = bInvalid;
		}

		// ===================== Write =====================

		private void WriteToStrings()
		{
			if(m_bLoading || m_bViewOnly || m_bLocked) return;

			ProtectedStringDictionary d = m_form.EntryStrings;
			if(d == null) return;

			char[] vKey = m_tbKey.TextEx.ReadChars();
			byte[] pbAscii = TotpGenerator.NormalizeBase32(vKey);
			Array.Clear(vKey, 0, vKey.Length);

			d.Remove(FldSecret);
			d.Remove(FldSecretHex);
			d.Remove(FldSecretB32);
			d.Remove(FldSecretB64);

			if(pbAscii.Length == 0)
			{
				d.Remove(FldLength);
				d.Remove(FldPeriod);
				d.Remove(FldAlg);
				d.Remove(FldIssuer);
				if(m_bOtpUriManaged) d.Remove(FldOtpUri);
			}
			else
			{
				int nDigits = (int)m_nudDigits.Value;
				int nPeriod = (int)m_nudPeriod.Value;
				string strAlg = GetSelectedAlg();
				string strIssuer = m_tbIssuer.Text.Trim();

				ProtectedString psSecret = new ProtectedString(true, pbAscii);
				d.Set(FldSecretB32, psSecret);
				d.Set(FldLength, new ProtectedString(false, nDigits.ToString()));
				d.Set(FldPeriod, new ProtectedString(false, nPeriod.ToString()));
				d.Set(FldAlg, new ProtectedString(false, strAlg));

				if(strIssuer.Length > 0) d.Set(FldIssuer, new ProtectedString(false, strIssuer));
				else d.Remove(FldIssuer);

				if(m_bOtpUriManaged)
				{
					string strUser = ((m_tbUserName != null) ? m_tbUserName.Text : string.Empty);
					d.Set(FldOtpUri, new ProtectedString(true, TotpGenerator.BuildOtpAuth(
						psSecret.ReadString(), nDigits, nPeriod, strAlg, strIssuer, strUser)));
				}
			}

			Array.Clear(pbAscii, 0, pbAscii.Length);
			m_bListDirty = true;
		}

		private string GetSelectedAlg()
		{
			string str = (m_cmbAlg.SelectedItem as string);
			return (string.IsNullOrEmpty(str) ? TotpGenerator.AlgSha1 : str);
		}

		private static decimal Clamp(int n, NumericUpDown nud)
		{
			decimal v = n;
			if(v < nud.Minimum) v = nud.Minimum;
			if(v > nud.Maximum) v = nud.Maximum;
			return v;
		}

		// ===================== Code display =====================

		private void UpdateCode()
		{
			if(m_view == null) return;
			int nPeriod = (int)m_nudPeriod.Value;

			if(!m_bCanGenerate || m_bSecretInvalid || (m_pbSecret == null))
			{
				m_strCurrentCode = string.Empty;

				if(m_bSecretInvalid)
					m_view.ShowMessage("Ung\u00fcltiger Key \u2013 erwartet wird ein Base32-Secret " +
						"oder ein otpauth://-Link.", true);
				else if(!m_bCanGenerate && m_bLocked) m_view.ShowMessage(m_strLockNote, false);
				else if(!m_bCanGenerate) { } // Keep the message set by OnKeyTextChanged
				else if(m_bViewOnly) m_view.ShowMessage("Kein TOTP eingerichtet.", false);
				else m_view.ShowMessage("Kein TOTP eingerichtet. Key oder otpauth://-Link " +
					"unten einf\u00fcgen.", false);
				return;
			}

			byte[] pb = m_pbSecret.ReadData();
			try
			{
				int nRemaining;
				string strCode = TotpGenerator.Generate(pb, (int)m_nudDigits.Value, nPeriod,
					GetSelectedAlg(), DateTime.UtcNow, out nRemaining);
				m_strCurrentCode = strCode;
				m_view.ShowCode(strCode, nRemaining, nPeriod);
			}
			catch(Exception)
			{
				m_strCurrentCode = string.Empty;
				m_view.ShowMessage("Der Code konnte nicht berechnet werden.", true);
			}
			finally { Array.Clear(pb, 0, pb.Length); }
		}

		// ===================== Events =====================

		private void OnKeyTextChanged(object sender, EventArgs e)
		{
			if(m_bLoading || m_bViewOnly || m_bLocked) return;

			char[] v = m_tbKey.TextEx.ReadChars();
			try
			{
				if(TotpGenerator.IsOtpAuthUri(v))
				{
					OtpAuthData oa = TotpGenerator.ParseOtpAuth(new string(v));
					if((oa != null) && oa.Editable)
					{
						ApplyOtpAuth(oa);
						return; // ApplyOtpAuth wrote everything
					}

					// Incomplete (still typing) or unsupported link: show the reason,
					// never store the link text as secret
					SetSecretCache(null, false);
					m_bCanGenerate = false;
					string strReason = "otpauth-Link wird nicht unterst\u00fctzt" +
						(((oa != null) && (oa.Note.Length > 0)) ? (": " + oa.Note) : ".");
					m_view.ShowMessage(strReason, true);
					m_tt.SetToolTip(m_tbKey, strReason);
					m_strCurrentCode = string.Empty;
					return;
				}

				byte[] pbAscii = TotpGenerator.NormalizeBase32(v);
				byte[] pbSecret = TotpGenerator.DecodeBase32(pbAscii);
				bool bEmpty = (pbAscii.Length == 0);
				Array.Clear(pbAscii, 0, pbAscii.Length);

				SetSecretCache(pbSecret, !bEmpty && (pbSecret == null));
				if(pbSecret != null) Array.Clear(pbSecret, 0, pbSecret.Length);
				m_bCanGenerate = true;
				UpdateEditState(); // Resets tooltips changed for otpauth links
			}
			finally { Array.Clear(v, 0, v.Length); }

			WriteToStrings();
			UpdateCode();
		}

		private void ApplyOtpAuth(OtpAuthData oa)
		{
			m_bLoading = true;
			try
			{
				m_tbKey.TextEx = new ProtectedString(true, oa.Secret);
				if(oa.Digits > 0) m_nudDigits.Value = Clamp(oa.Digits, m_nudDigits);
				if(oa.Period > 0) m_nudPeriod.Value = Clamp(oa.Period, m_nudPeriod);
				int iAlg = m_cmbAlg.Items.IndexOf(TotpGenerator.NormalizeAlgorithm(oa.Algorithm));
				m_cmbAlg.SelectedIndex = ((iAlg >= 0) ? iAlg : 0);
				if(oa.Issuer.Length > 0) m_tbIssuer.Text = oa.Issuer;

				// Like KeePass' own otpauth import: fill an empty title / user name
				if((m_tbTitle != null) && (m_tbTitle.Text.Trim().Length == 0) &&
					(oa.Issuer.Length > 0)) m_tbTitle.Text = oa.Issuer;
				if((m_tbUserName != null) && (m_tbUserName.Text.Trim().Length == 0) &&
					(oa.Account.Length > 0)) m_tbUserName.Text = oa.Account;
			}
			finally { m_bLoading = false; }

			byte[] pbSecret = TotpGenerator.DecodeBase32(oa.Secret);
			SetSecretCache(pbSecret, pbSecret == null);
			if(pbSecret != null) Array.Clear(pbSecret, 0, pbSecret.Length);
			m_bCanGenerate = true;

			WriteToStrings();
			UpdateCode();
		}

		private void OnValueChanged(object sender, EventArgs e)
		{
			if(m_bLoading) return;
			WriteToStrings();
			UpdateCode();
		}

		private void OnShowKeyClick(object sender, EventArgs e)
		{
			m_tbKey.EnableProtection(!m_tbKey.UseSystemPasswordChar);
		}

		private void OnCopyRequested(object sender, EventArgs e)
		{
			if(m_strCurrentCode.Length == 0) return;
			if(ClipboardUtil.Copy(m_strCurrentCode, false, true, m_form.EntryRef, null,
				m_form.Handle)) m_view.ConfirmCopied();
		}

		private void OnTimerTick(object sender, EventArgs e)
		{
			UpdateCode();
		}

		private void OnTabMainSelectedIndexChanged(object sender, EventArgs e)
		{
			if(m_layout.TabMain.SelectedTab == m_layout.TabEntry)
			{
				// Fields may have been edited on the "Advanced" tab
				LoadFromStrings();
			}
			else if(m_bListDirty && !m_bViewOnly)
			{
				// Refresh the field list on the "Advanced" tab
				m_form.UpdateEntryStrings(true, false);
				m_form.UpdateEntryStrings(false, false);
				m_bListDirty = false;
			}
		}

		private void OnFormClosed(object sender, FormClosedEventArgs e)
		{
			if(m_timer != null)
			{
				m_timer.Stop();
				m_timer.Tick -= this.OnTimerTick;
				m_timer.Dispose();
				m_timer = null;
			}
			if(m_tt != null) { m_tt.Dispose(); m_tt = null; }
			m_pbSecret = null;
			m_strCurrentCode = string.Empty;

			m_layout.TabMain.SelectedIndexChanged -= this.OnTabMainSelectedIndexChanged;
			m_form.FormClosed -= this.OnFormClosed;
		}

		private void OnFormDisposed(object sender, EventArgs e)
		{
			m_form.Disposed -= this.OnFormDisposed;
		}
	}
}
