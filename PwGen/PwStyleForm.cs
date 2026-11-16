/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Options of the syllable password generator (Apple style), with a live
  preview. Opened by the "Options" button in KeePass' password generator
  dialog and from Tools > Options > BetterPass.
*/

using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

using KeePass.UI;

using KeePassLib.Cryptography;

namespace BetterPass
{
	internal sealed class PwStyleForm : Form
	{
		private readonly NumericUpDown m_nudGroups;
		private readonly NumericUpDown m_nudGroupLength;
		private readonly TextBox m_tbSeparator;
		private readonly NumericUpDown m_nudUpper;
		private readonly NumericUpDown m_nudDigits;
		private readonly Label m_lblPreview;
		private readonly Label m_lblInfo;

		private bool m_bLoading = false;
		private PwStyle m_style;

		/// <summary>
		/// Shows the dialog. Returns the new settings, or null if cancelled.
		/// </summary>
		public static PwStyle Edit(IWin32Window owner, PwStyle s)
		{
			using(PwStyleForm f = new PwStyleForm(s ?? new PwStyle()))
			{
				DialogResult dr = ((owner != null) ? f.ShowDialog(owner) : f.ShowDialog());
				return ((dr == DialogResult.OK) ? f.m_style.CloneDeep() : null);
			}
		}

		private PwStyleForm(PwStyle s)
		{
			m_style = s.CloneDeep();
			m_style.Normalize();

			this.Text = "Silben-Passwort (Apple-Stil) \u2013 Optionen";
			this.AutoScaleMode = AutoScaleMode.Font;
			this.Font = SystemFonts.MessageBoxFont;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.StartPosition = FormStartPosition.CenterParent;
			this.ShowInTaskbar = false;
			this.MinimizeBox = false;
			this.MaximizeBox = false;
			this.ShowIcon = false;
			this.AutoSize = true;
			this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			this.Padding = new Padding(12);

			TableLayoutPanel tlp = new TableLayoutPanel();
			tlp.ColumnCount = 2;
			tlp.AutoSize = true;
			tlp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			tlp.Dock = DockStyle.Fill;
			tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

			m_nudGroups = AddNumber(tlp, "Anzahl Gruppen:", PwStyle.MinGroups, PwStyle.MaxGroups);
			m_nudGroupLength = AddNumber(tlp, "Zeichen pro Gruppe:", PwStyle.MinGroupLength,
				PwStyle.MaxGroupLength);

			m_tbSeparator = new TextBox();
			m_tbSeparator.MaxLength = PwStyle.MaxSeparatorLength;
			m_tbSeparator.Width = 60;
			m_tbSeparator.TextChanged += this.OnValueChanged;
			AddRow(tlp, "Trennzeichen:", m_tbSeparator);

			m_nudUpper = AddNumber(tlp, "Gro\u00dfbuchstaben (genau):", 0, 96);
			m_nudDigits = AddNumber(tlp, "Ziffern (genau):", 0, 96);

			Label lblHint = new Label();
			lblHint.Text = "Ziffern stehen bevorzugt am Anfang oder Ende einer Gruppe. " +
				"Kein \u201el\u201c, damit nichts mit \u201e1\u201c oder \u201eI\u201c verwechselt wird.";
			lblHint.AutoSize = true;
			lblHint.MaximumSize = new Size(380, 0);
			lblHint.ForeColor = ThemeManager.DimTextColor;
			lblHint.Margin = new Padding(3, 6, 3, 6);
			AddSpanning(tlp, lblHint);

			GroupBox gbPreview = new GroupBox();
			gbPreview.Text = "Vorschau";
			gbPreview.AutoSize = true;
			gbPreview.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			gbPreview.Dock = DockStyle.Fill;
			gbPreview.Padding = new Padding(8, 6, 8, 6);

			FlowLayoutPanel flpPreview = new FlowLayoutPanel();
			flpPreview.FlowDirection = FlowDirection.TopDown;
			flpPreview.WrapContents = false;
			flpPreview.AutoSize = true;
			flpPreview.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			flpPreview.Dock = DockStyle.Fill;

			m_lblPreview = new Label();
			m_lblPreview.AutoSize = true;
			m_lblPreview.Font = new Font("Consolas", this.Font.SizeInPoints + 1.5f);
			m_lblPreview.Margin = new Padding(3, 3, 3, 3);
			flpPreview.Controls.Add(m_lblPreview);

			m_lblInfo = new Label();
			m_lblInfo.AutoSize = true;
			m_lblInfo.ForeColor = ThemeManager.DimTextColor;
			m_lblInfo.Margin = new Padding(3, 3, 3, 3);
			flpPreview.Controls.Add(m_lblInfo);

			Button btnNew = new Button();
			btnNew.Text = "Neue Beispiele";
			btnNew.AutoSize = true;
			btnNew.UseVisualStyleBackColor = true;
			btnNew.Click += delegate(object sender, EventArgs e) { UpdatePreview(); };
			flpPreview.Controls.Add(btnNew);

			gbPreview.Controls.Add(flpPreview);
			AddSpanning(tlp, gbPreview);

			FlowLayoutPanel flpButtons = new FlowLayoutPanel();
			flpButtons.FlowDirection = FlowDirection.LeftToRight;
			flpButtons.AutoSize = true;
			flpButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			flpButtons.Dock = DockStyle.Fill;
			flpButtons.Margin = new Padding(0, 10, 0, 0);

			Button btnDefault = new Button();
			btnDefault.Text = "Apple-Standard";
			btnDefault.AutoSize = true;
			btnDefault.UseVisualStyleBackColor = true;
			btnDefault.Click += delegate(object sender, EventArgs e)
			{
				m_style = new PwStyle();
				LoadValues();
			};
			flpButtons.Controls.Add(btnDefault);

			Button btnOK = new Button();
			btnOK.Text = "OK";
			btnOK.Size = new Size(88, 26);
			btnOK.DialogResult = DialogResult.OK;
			btnOK.UseVisualStyleBackColor = true;
			btnOK.Margin = new Padding(40, 3, 3, 3);
			flpButtons.Controls.Add(btnOK);

			Button btnCancel = new Button();
			btnCancel.Text = "Abbrechen";
			btnCancel.Size = new Size(88, 26);
			btnCancel.DialogResult = DialogResult.Cancel;
			btnCancel.UseVisualStyleBackColor = true;
			flpButtons.Controls.Add(btnCancel);

			AddSpanning(tlp, flpButtons);

			this.AcceptButton = btnOK;
			this.CancelButton = btnCancel;
			this.Controls.Add(tlp);

			LoadValues();
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			// Standard KeePass dialog handling (theming, locking, window list)
			GlobalWindowManager.AddWindow(this);
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			GlobalWindowManager.RemoveWindow(this);
			base.OnFormClosed(e);
		}

		// ===================== Layout helpers =====================

		private void AddRow(TableLayoutPanel tlp, string strLabel, Control c)
		{
			Label lbl = new Label();
			lbl.Text = strLabel;
			lbl.AutoSize = true;
			lbl.Anchor = AnchorStyles.Left;
			lbl.Margin = new Padding(3, 6, 12, 3);

			c.Anchor = AnchorStyles.Left;
			tlp.RowCount += 1;
			tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			tlp.Controls.Add(lbl, 0, tlp.RowCount - 1);
			tlp.Controls.Add(c, 1, tlp.RowCount - 1);
		}

		private NumericUpDown AddNumber(TableLayoutPanel tlp, string strLabel, int nMin, int nMax)
		{
			NumericUpDown nud = new NumericUpDown();
			nud.Minimum = nMin;
			nud.Maximum = nMax;
			nud.Width = 60;
			nud.ValueChanged += this.OnValueChanged;
			AddRow(tlp, strLabel, nud);
			return nud;
		}

		private static void AddSpanning(TableLayoutPanel tlp, Control c)
		{
			tlp.RowCount += 1;
			tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			tlp.Controls.Add(c, 0, tlp.RowCount - 1);
			tlp.SetColumnSpan(c, 2);
		}

		// ===================== Values =====================

		private void LoadValues()
		{
			m_bLoading = true;
			try
			{
				m_nudGroups.Value = m_style.Groups;
				m_nudGroupLength.Value = m_style.GroupLength;
				m_tbSeparator.Text = m_style.Separator;
				m_nudUpper.Value = Math.Min(m_style.UpperCount, (int)m_nudUpper.Maximum);
				m_nudDigits.Value = Math.Min(m_style.DigitCount, (int)m_nudDigits.Maximum);
			}
			finally { m_bLoading = false; }
			UpdatePreview();
		}

		private void OnValueChanged(object sender, EventArgs e)
		{
			if(m_bLoading) return;

			PwStyle s = new PwStyle();
			s.Groups = (int)m_nudGroups.Value;
			s.GroupLength = (int)m_nudGroupLength.Value;
			s.Separator = m_tbSeparator.Text;
			s.UpperCount = (int)m_nudUpper.Value;
			s.DigitCount = (int)m_nudDigits.Value;
			s.Normalize();
			m_style = s;

			// Show the clamped values (e.g. more digits than letters)
			m_bLoading = true;
			try
			{
				if((int)m_nudUpper.Value != s.UpperCount) m_nudUpper.Value = s.UpperCount;
				if((int)m_nudDigits.Value != s.DigitCount) m_nudDigits.Value = s.DigitCount;
			}
			finally { m_bLoading = false; }

			UpdatePreview();
		}

		private void UpdatePreview()
		{
			StringBuilder sb = new StringBuilder();
			byte[] pbKey = CryptoRandom.Instance.GetRandomBytes(64);
			CryptoRandomStream crs = new CryptoRandomStream(CrsAlgorithm.ChaCha20, pbKey);
			for(int i = 0; i < 3; ++i)
			{
				if(i > 0) sb.AppendLine();
				sb.Append(new string(m_style.Generate(crs)));
			}
			m_lblPreview.Text = sb.ToString();

			double dBits = m_style.EstimateBits();
			string strRating = ((dBits >= 70.0) ? "stark" : ((dBits >= 50.0) ? "mittel" : "schwach"));
			m_lblInfo.Text = "L\u00e4nge: " + m_style.TotalLength.ToString(CultureInfo.CurrentCulture) +
				" Zeichen \u00b7 St\u00e4rke: ca. " + Math.Round(dBits).ToString(CultureInfo.CurrentCulture) +
				" Bit (" + strRating + ")" + (m_style.IsAppleDefault ? " \u00b7 Apple-Standard" : string.Empty);
		}
	}
}
