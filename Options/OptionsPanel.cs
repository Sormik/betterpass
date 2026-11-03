/*
  BetterPass - KeePass plugin
  Settings panel, used as a tab in KeePass' options dialog
  (Tools > Options > BetterPass) and in the standalone settings dialog.
*/

using System;
using System.Drawing;
using System.Windows.Forms;

using KeePassLib.Utility;

namespace BetterPass
{
	internal sealed class OptionsPanel : UserControl
	{
		private static readonly string[] IconVariantKeys = new string[] {
			"default", "color", "mono", "light", "dark" };
		private static readonly string[] IconVariantNames = new string[] {
			"Standard", "Farbig (falls vorhanden)", "Einfarbig", "F\u00fcr helle Hintergr\u00fcnde",
			"F\u00fcr dunkle Hintergr\u00fcnde" };
		private static readonly int[] IconSizes = new int[] { 32, 64, 128 };

		private readonly TableLayoutPanel m_tlp;

		private CheckBox m_cbTotpShow;
		private CheckBox m_cbTotpHide;
		private ComboBox m_cmbDesign;
		private ComboBox m_cmbMode;
		private CheckBox m_cbHibpEntry;
		private CheckBox m_cbHibpAuto;
		private CheckBox m_cbHibpOpen;
		private CheckBox m_cbHibpTag;
		private ComboBox m_cmbIconVariant;
		private ComboBox m_cmbIconSize;
		private NumericUpDown m_nudCatalogDays;

		public OptionsPanel()
		{
			this.AutoScroll = true;
			this.Padding = new Padding(6);

			m_tlp = new TableLayoutPanel();
			m_tlp.Dock = DockStyle.Top;
			m_tlp.AutoSize = true;
			m_tlp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			m_tlp.ColumnCount = 1;
			m_tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

			BuildTotp();
			BuildTheme();
			BuildHibp();
			BuildIcons();
			BuildAbout();

			this.Controls.Add(m_tlp);
			LoadSettings();
		}

		// ===================== Layout helpers =====================

		private FlowLayoutPanel AddGroup(string strTitle)
		{
			GroupBox gb = new GroupBox();
			gb.Text = strTitle;
			gb.Dock = DockStyle.Fill;
			gb.AutoSize = true;
			gb.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			gb.Padding = new Padding(8, 6, 8, 6);
			gb.Margin = new Padding(0, 0, 0, 8);

			FlowLayoutPanel flp = new FlowLayoutPanel();
			flp.Dock = DockStyle.Fill;
			flp.FlowDirection = FlowDirection.TopDown;
			flp.WrapContents = false;
			flp.AutoSize = true;
			flp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			gb.Controls.Add(flp);

			m_tlp.RowCount += 1;
			m_tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			m_tlp.Controls.Add(gb, 0, m_tlp.RowCount - 1);
			return flp;
		}

		private static CheckBox AddCheck(FlowLayoutPanel flp, string strText, int nIndent)
		{
			CheckBox cb = new CheckBox();
			cb.Text = strText;
			cb.AutoSize = true;
			cb.Margin = new Padding(3 + nIndent, 3, 3, 3);
			flp.Controls.Add(cb);
			return cb;
		}

		private static Label AddNote(FlowLayoutPanel flp, string strText)
		{
			Label lbl = new Label();
			lbl.Text = strText;
			lbl.AutoSize = true;
			lbl.MaximumSize = new Size(420, 0);
			lbl.ForeColor = ThemeManager.DimTextColor;
			lbl.Margin = new Padding(3, 4, 3, 4);
			flp.Controls.Add(lbl);
			return lbl;
		}

		private static FlowLayoutPanel AddRow(FlowLayoutPanel flp, string strLabel, Control c)
		{
			FlowLayoutPanel row = new FlowLayoutPanel();
			row.FlowDirection = FlowDirection.LeftToRight;
			row.WrapContents = false;
			row.AutoSize = true;
			row.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			row.Margin = new Padding(0, 2, 0, 2);

			Label lbl = new Label();
			lbl.Text = strLabel;
			lbl.AutoSize = true;
			lbl.Anchor = AnchorStyles.Left;
			lbl.Margin = new Padding(3, 6, 6, 3);
			row.Controls.Add(lbl);

			c.Margin = new Padding(3, 3, 3, 3);
			row.Controls.Add(c);
			flp.Controls.Add(row);
			return row;
		}

		protected override void OnLayout(LayoutEventArgs e)
		{
			// Wrap notes to the available width (OnLayout already runs while
			// the constructor sets AutoScroll, before m_tlp exists)
			if(m_tlp == null) { base.OnLayout(e); return; }
			int w = Math.Max(200, this.ClientSize.Width - 60);
			foreach(Control gb in m_tlp.Controls)
			{
				foreach(Control flp in gb.Controls)
				{
					foreach(Control c in flp.Controls)
					{
						Label lbl = (c as Label);
						if((lbl != null) && (lbl.MaximumSize.Width > 0) && (lbl.MaximumSize.Width != w))
							lbl.MaximumSize = new Size(w, 0);
					}
				}
			}
			base.OnLayout(e);
		}

		// ===================== Sections =====================

		private void BuildTotp()
		{
			FlowLayoutPanel flp = AddGroup("TOTP (Einmal-Codes)");
			m_cbTotpShow = AddCheck(flp, "TOTP-Felder im Eintragsdialog anzeigen", 0);
			m_cbTotpHide = AddCheck(flp, "TOTP-Key standardm\u00e4\u00dfig verbergen", 0);
			AddNote(flp, "Gespeichert wird in den KeePass-eigenen Feldern (TimeOtp-*), der " +
				"Platzhalter {TIMEOTP} funktioniert also auch in Auto-Type.");
		}

		private void BuildTheme()
		{
			FlowLayoutPanel flp = AddGroup("Darstellung");

			// Order matches ThemeDesign / ThemeMode
			m_cmbDesign = new ComboBox();
			m_cmbDesign.DropDownStyle = ComboBoxStyle.DropDownList;
			m_cmbDesign.Items.AddRange(new object[] { "Klassisch (KeePass)", "Windows 11", "aethercert" });
			m_cmbDesign.Width = 180;
			AddRow(flp, "Design:", m_cmbDesign);

			m_cmbMode = new ComboBox();
			m_cmbMode.DropDownStyle = ComboBoxStyle.DropDownList;
			m_cmbMode.Items.AddRange(new object[] { "Hell", "Dunkel", "Wie Windows" });
			m_cmbMode.Width = 180;
			AddRow(flp, "Farbmodus:", m_cmbMode);

			m_cmbDesign.SelectedIndexChanged += delegate(object sender, EventArgs e)
			{
				m_cmbMode.Enabled = (m_cmbDesign.SelectedIndex != (int)ThemeDesign.Classic);
			};

			AddNote(flp, "Windows 11: flache Fl\u00e4chen, Haarlinien und die Akzentfarbe von " +
				"Windows. aethercert: Farben und Formen des aethercert-Brandkits. Beide " +
				"gelten f\u00fcr Hauptfenster, Men\u00fcs, Dialoge und Eingabefelder.");
			AddNote(flp, "\u00c4nderungen wirken sofort. Zur\u00fcck zu \u201eKlassisch\u201c " +
				"wird nach einem Neustart von KeePass vollst\u00e4ndig wirksam. Im " +
				"Hochkontrastmodus von Windows bleibt KeePass klassisch.");
		}

		private void BuildHibp()
		{
			FlowLayoutPanel flp = AddGroup("Have I Been Pwned");
			m_cbHibpEntry = AddCheck(flp, "Passwort im Eintragsdialog pr\u00fcfen", 0);
			m_cbHibpAuto = AddCheck(flp, "Automatisch beim \u00d6ffnen und Tippen pr\u00fcfen " +
				"(sonst nur per Schaltfl\u00e4che)", 18);
			m_cbHibpOpen = AddCheck(flp, "Alle Passw\u00f6rter nach dem \u00d6ffnen einer " +
				"Datenbank im Hintergrund pr\u00fcfen", 0);
			m_cbHibpTag = AddCheck(flp, "Betroffene Eintr\u00e4ge mit dem Tag \"" +
				BpConfig.HibpTag + "\" markieren", 0);
			AddNote(flp, "Datenschutz: Es werden nur die ersten 5 Zeichen des SHA-1-Hashes " +
				"an api.pwnedpasswords.com \u00fcbertragen (k-Anonymit\u00e4t). Das Passwort " +
				"selbst verl\u00e4sst den Rechner nicht.");

			m_cbHibpEntry.CheckedChanged += delegate(object sender, EventArgs e)
			{
				m_cbHibpAuto.Enabled = m_cbHibpEntry.Checked;
			};
		}

		private void BuildIcons()
		{
			FlowLayoutPanel flp = AddGroup("Icons von theSVG");

			m_cmbIconVariant = new ComboBox();
			m_cmbIconVariant.DropDownStyle = ComboBoxStyle.DropDownList;
			m_cmbIconVariant.Items.AddRange(IconVariantNames);
			m_cmbIconVariant.Width = 220;
			AddRow(flp, "Bevorzugte Variante:", m_cmbIconVariant);

			m_cmbIconSize = new ComboBox();
			m_cmbIconSize.DropDownStyle = ComboBoxStyle.DropDownList;
			foreach(int n in IconSizes)
				m_cmbIconSize.Items.Add(n.ToString() + " \u00d7 " + n.ToString() + " Pixel");
			m_cmbIconSize.Width = 160;
			AddRow(flp, "Icongr\u00f6\u00dfe in der Datenbank:", m_cmbIconSize);

			m_nudCatalogDays = new NumericUpDown();
			m_nudCatalogDays.Minimum = 1;
			m_nudCatalogDays.Maximum = 365;
			m_nudCatalogDays.Width = 60;
			FlowLayoutPanel row = AddRow(flp, "Icon-Katalog aktualisieren alle", m_nudCatalogDays);
			Label lblDays = new Label();
			lblDays.Text = "Tage";
			lblDays.AutoSize = true;
			lblDays.Margin = new Padding(3, 6, 3, 3);
			row.Controls.Add(lblDays);

			Button btnClear = new Button();
			btnClear.Text = "Icon-Cache leeren";
			btnClear.AutoSize = true;
			btnClear.UseVisualStyleBackColor = true;
			btnClear.Margin = new Padding(3, 6, 3, 3);
			btnClear.Click += delegate(object sender, EventArgs e)
			{
				try
				{
					TheSvgCatalog.ClearCache();
					MessageService.ShowInfo("Der Icon-Cache wurde geleert.");
				}
				catch(Exception ex) { MessageService.ShowWarning(ex.Message); }
			};
			flp.Controls.Add(btnClear);

			AddNote(flp, "Icons zuweisen: Eintr\u00e4ge markieren, Rechtsklick \u2192 BetterPass. " +
				"Die Icons stammen von thesvg.org; Marken geh\u00f6ren ihren jeweiligen Inhabern.");
		}

		private void BuildAbout()
		{
			FlowLayoutPanel flp = AddGroup("\u00dcber");
			string strVer = typeof(OptionsPanel).Assembly.GetName().Version.ToString(3);
			AddNote(flp, "BetterPass " + strVer + " \u2013 TOTP-Felder, modernes Design " +
				"(Windows 11 oder aethercert, hell und dunkel), Have-I-Been-Pwned-Pr\u00fcfung " +
				"und Marken-Icons f\u00fcr KeePass 2.x.");
		}

		// ===================== Load / save =====================

		private void LoadSettings()
		{
			m_cbTotpShow.Checked = BpConfig.TotpShowFields;
			m_cbTotpHide.Checked = BpConfig.TotpHideKey;

			m_cmbDesign.SelectedIndex = (int)BpConfig.Design;
			m_cmbMode.SelectedIndex = (int)BpConfig.ColorMode;
			m_cmbMode.Enabled = (BpConfig.Design != ThemeDesign.Classic);

			m_cbHibpEntry.Checked = BpConfig.HibpEntryCheck;
			m_cbHibpAuto.Checked = BpConfig.HibpAutoCheck;
			m_cbHibpAuto.Enabled = m_cbHibpEntry.Checked;
			m_cbHibpOpen.Checked = BpConfig.HibpCheckOnOpen;
			m_cbHibpTag.Checked = BpConfig.HibpTagEntries;

			int iVar = Array.IndexOf(IconVariantKeys, BpConfig.IconVariant);
			m_cmbIconVariant.SelectedIndex = ((iVar >= 0) ? iVar : 0);
			int iSize = Array.IndexOf(IconSizes, BpConfig.IconSize);
			m_cmbIconSize.SelectedIndex = ((iSize >= 0) ? iSize : 1);
			m_nudCatalogDays.Value = BpConfig.IconCatalogMaxAgeDays;
		}

		public void SaveSettings()
		{
			BpConfig.TotpShowFields = m_cbTotpShow.Checked;
			BpConfig.TotpHideKey = m_cbTotpHide.Checked;

			int iDesign = m_cmbDesign.SelectedIndex;
			BpConfig.Design = (((iDesign >= 0) && (iDesign <= 2)) ? (ThemeDesign)iDesign :
				ThemeDesign.Windows11);
			int iMode = m_cmbMode.SelectedIndex;
			BpConfig.ColorMode = (((iMode >= 0) && (iMode <= 2)) ? (ThemeMode)iMode :
				ThemeMode.System);

			BpConfig.HibpEntryCheck = m_cbHibpEntry.Checked;
			BpConfig.HibpAutoCheck = m_cbHibpAuto.Checked;
			BpConfig.HibpCheckOnOpen = m_cbHibpOpen.Checked;
			BpConfig.HibpTagEntries = m_cbHibpTag.Checked;

			int iVar = m_cmbIconVariant.SelectedIndex;
			BpConfig.IconVariant = (((iVar >= 0) && (iVar < IconVariantKeys.Length)) ?
				IconVariantKeys[iVar] : "default");
			int iSize = m_cmbIconSize.SelectedIndex;
			BpConfig.IconSize = (((iSize >= 0) && (iSize < IconSizes.Length)) ? IconSizes[iSize] : 64);
			BpConfig.IconCatalogMaxAgeDays = (int)m_nudCatalogDays.Value;
		}

		/// <summary>
		/// Applies settings that affect the running application.
		/// </summary>
		public static void ApplyAfterSave(IWin32Window owner)
		{
			bool bRestart = ThemeManager.Update();
			BpConfig.RaiseChanged();

			if(bRestart)
				MessageBox.Show(owner, "Das klassische Aussehen von KeePass ist nach einem " +
					"Neustart vollst\u00e4ndig wiederhergestellt.", "BetterPass", MessageBoxButtons.OK,
					MessageBoxIcon.Information);
		}
	}
}
