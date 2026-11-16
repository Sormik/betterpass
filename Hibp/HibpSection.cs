/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  "Leak check" row in the entry dialog (below the quality bar).
*/

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using KeePass.Forms;
using KeePass.UI;

using KeePassLib.Security;

namespace BetterPass
{
	internal sealed class HibpSection
	{
		private readonly EntryFormLayout m_layout;
		private readonly PwEntryForm m_form;
		private SecureTextBoxEx m_tbPassword;

		private Label m_lblStatus;
		private Button m_btnCheck;
		private System.Windows.Forms.Timer m_tmrDebounce;
		private ToolTip m_tt;

		private int m_iRequest = 0;  // Increased for every check; stale results are ignored
		private bool m_bClosed = false;

		private HibpSection(EntryFormLayout l)
		{
			m_layout = l;
			m_form = l.Form;
		}

		public static HibpSection Attach(EntryFormLayout l)
		{
			if(l == null) return null;

			SecureTextBoxEx tbPw = EntryFormLayout.FindControl<SecureTextBoxEx>(l.Form, "m_tbPassword");
			if(tbPw == null) return null;

			HibpSection s = new HibpSection(l);
			s.m_tbPassword = tbPw;
			s.Create();
			return s;
		}

		private void Create()
		{
			EntryFormLayout l = m_layout;
			int y = l.UrlBox.Top; // New row goes where the URL row is now
			int nTab = l.InsertRows(y, 1);

			l.AddLabel("Leak-Check:", l.LabelX, y);

			string strBtn = "Pr\u00fcfen";
			int wBtn = Math.Max(l.ButtonWidth * 2, TextRenderer.MeasureText(strBtn,
				l.TabEntry.Font).Width + 24);
			int xBtn = l.RightX + 1 - wBtn;

			m_lblStatus = new Label();
			m_lblStatus.AutoSize = false;
			m_lblStatus.AutoEllipsis = true;
			m_lblStatus.TextAlign = ContentAlignment.MiddleLeft;
			m_lblStatus.Bounds = new Rectangle(l.FieldX, y, xBtn - l.Gap - l.FieldX,
				l.TextBoxHeight);
			l.Add(m_lblStatus);

			m_btnCheck = new Button();
			m_btnCheck.Text = strBtn;
			m_btnCheck.Bounds = new Rectangle(xBtn, y + (l.TextBoxHeight - l.ButtonHeight) / 2,
				wBtn, l.ButtonHeight);
			m_btnCheck.UseVisualStyleBackColor = true;
			m_btnCheck.TabIndex = nTab;
			m_btnCheck.Click += this.OnCheckClick;
			l.Add(m_btnCheck);

			m_tt = new ToolTip();
			m_tt.SetToolTip(m_btnCheck, "Passwort bei Have I Been Pwned pr\u00fcfen.\r\n" +
				"Es werden nur die ersten 5 Zeichen des SHA-1-Hashes \u00fcbertragen.");

			m_tmrDebounce = new System.Windows.Forms.Timer();
			m_tmrDebounce.Interval = 900;
			m_tmrDebounce.Tick += this.OnDebounceTick;

			m_tbPassword.TextChanged += this.OnPasswordChanged;
			m_form.FormClosed += this.OnFormClosed;

			if(BpConfig.HibpAutoCheck) StartCheck();
			else SetStatus("Nicht gepr\u00fcft", ThemeManager.DimTextColor, string.Empty);
		}

		private void SetStatus(string strText, Color clr, string strTip)
		{
			if(m_bClosed || m_lblStatus.IsDisposed) return;
			m_lblStatus.Text = strText;
			m_lblStatus.ForeColor = clr;
			m_tt.SetToolTip(m_lblStatus, string.IsNullOrEmpty(strTip) ? strText : strTip);
		}

		private void OnPasswordChanged(object sender, EventArgs e)
		{
			++m_iRequest; // Invalidates a running check
			m_tmrDebounce.Stop();

			if(BpConfig.HibpAutoCheck)
			{
				SetStatus("\u2026", ThemeManager.DimTextColor, string.Empty);
				m_tmrDebounce.Start();
			}
			else SetStatus("Nicht gepr\u00fcft", ThemeManager.DimTextColor, string.Empty);
		}

		private void OnDebounceTick(object sender, EventArgs e)
		{
			m_tmrDebounce.Stop();
			StartCheck();
		}

		private void OnCheckClick(object sender, EventArgs e)
		{
			m_tmrDebounce.Stop();
			StartCheck();
		}

		private void StartCheck()
		{
			if(m_bClosed) return;

			ProtectedString ps = m_tbPassword.TextEx;
			if((ps == null) || ps.IsEmpty)
			{
				SetStatus("Kein Passwort", ThemeManager.DimTextColor, string.Empty);
				return;
			}

			byte[] pb = ps.ReadUtf8();
			string strHash;
			try
			{
				if(HibpDatabaseCheck.StartsWithRef(pb))
				{
					SetStatus("Feldreferenz \u2013 wird beim referenzierten Eintrag gepr\u00fcft",
						ThemeManager.DimTextColor, string.Empty);
					return;
				}
				strHash = HibpClient.ComputeSha1Hex(pb);
			}
			finally { Array.Clear(pb, 0, pb.Length); }

			int iRequest = ++m_iRequest;
			SetStatus("Wird gepr\u00fcft \u2026", ThemeManager.DimTextColor, string.Empty);
			m_btnCheck.Enabled = false;

			ThreadPool.QueueUserWorkItem(delegate(object o)
			{
				int n = -1;
				string strError = null;
				try { n = HibpClient.GetBreachCount(strHash); }
				catch(Exception ex) { strError = Net.GetErrorMessage(ex); }

				try
				{
					if(m_bClosed || m_form.IsDisposed || !m_form.IsHandleCreated) return;
					m_form.BeginInvoke(new MethodInvoker(delegate()
					{
						OnCheckCompleted(iRequest, n, strError);
					}));
				}
				catch(Exception) { } // Form closed in the meantime
			});
		}

		private void OnCheckCompleted(int iRequest, int n, string strError)
		{
			if(m_bClosed) return;
			m_btnCheck.Enabled = true;
			if(iRequest != m_iRequest) return; // Password changed meanwhile

			if(strError != null)
				SetStatus("Pr\u00fcfung nicht m\u00f6glich: " + strError,
					ThemeManager.DimTextColor, strError);
			else if(n > 0)
				SetStatus("\u26a0 In " + HibpClient.FormatCount(n) +
					" Datenlecks gefunden \u2013 bitte \u00e4ndern!", ThemeManager.ErrorColor,
					"Dieses Passwort ist " + HibpClient.FormatCount(n) +
					"-mal in bekannten Datenlecks aufgetaucht (Have I Been Pwned).");
			else
				SetStatus("\u2713 In keinem bekannten Datenleck gefunden",
					ThemeManager.SuccessColor, string.Empty);
		}

		private void OnFormClosed(object sender, FormClosedEventArgs e)
		{
			m_bClosed = true;
			++m_iRequest;
			if(m_tmrDebounce != null)
			{
				m_tmrDebounce.Stop();
				m_tmrDebounce.Tick -= this.OnDebounceTick;
				m_tmrDebounce.Dispose();
				m_tmrDebounce = null;
			}
			if(m_tt != null) { m_tt.Dispose(); m_tt = null; }

			m_tbPassword.TextChanged -= this.OnPasswordChanged;
			m_form.FormClosed -= this.OnFormClosed;
		}
	}
}
