/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Dialog for choosing a theSVG brand icon.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

using KeePass.UI;

namespace BetterPass
{
	internal sealed class IconPickerForm : Form
	{
		private const int MaxResults = 120;
		private const int PreviewSize = 48;

		private static readonly string[] VariantKeys = new string[] {
			"default", "color", "mono", "light", "dark" };
		private static readonly string[] VariantNames = new string[] {
			"Standard", "Farbig", "Einfarbig", "F\u00fcr helle Hintergr\u00fcnde",
			"F\u00fcr dunkle Hintergr\u00fcnde" };

		private readonly string m_strInitialQuery;
		private readonly int m_nEntries;

		private TextBox m_tbSearch;
		private ComboBox m_cmbVariant;
		private ListView m_lvIcons;
		private ImageList m_ilIcons;
		private Label m_lblStatus;
		private Button m_btnOK;
		private Button m_btnCancel;
		private LinkLabel m_lnkSource;
		private System.Windows.Forms.Timer m_tmrSearch;

		private List<SvgIconInfo> m_lCatalog = null;
		private volatile int m_iGeneration = 0;
		private volatile bool m_bClosed = false;

		public SvgIconInfo SelectedIcon { get; private set; }
		public string SelectedVariant { get; private set; }

		public IconPickerForm(string strInitialQuery, int nEntries)
		{
			m_strInitialQuery = (strInitialQuery ?? string.Empty);
			m_nEntries = nEntries;
			this.SelectedVariant = BpConfig.IconVariant;
			InitializeComponent();
		}

		private void InitializeComponent()
		{
			this.SuspendLayout();

			this.Text = "Icon von theSVG w\u00e4hlen";
			this.AutoScaleMode = AutoScaleMode.Font;
			this.Font = SystemFonts.MessageBoxFont;
			this.ClientSize = new Size(620, 470);
			this.MinimumSize = new Size(480, 360);
			this.StartPosition = FormStartPosition.CenterParent;
			this.ShowInTaskbar = false;
			this.MinimizeBox = false;
			this.ShowIcon = false;

			Label lblSearch = new Label();
			lblSearch.Text = "Suche:";
			lblSearch.AutoSize = true;
			lblSearch.Location = new Point(12, 16);
			this.Controls.Add(lblSearch);

			m_tbSearch = new TextBox();
			m_tbSearch.Bounds = new Rectangle(70, 12, 330, 23);
			m_tbSearch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			m_tbSearch.TextChanged += this.OnSearchTextChanged;
			m_tbSearch.KeyDown += this.OnSearchKeyDown;
			this.Controls.Add(m_tbSearch);

			Label lblVariant = new Label();
			lblVariant.Text = "Variante:";
			lblVariant.AutoSize = true;
			lblVariant.Location = new Point(412, 16);
			lblVariant.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			this.Controls.Add(lblVariant);

			m_cmbVariant = new ComboBox();
			m_cmbVariant.DropDownStyle = ComboBoxStyle.DropDownList;
			m_cmbVariant.Bounds = new Rectangle(472, 12, 136, 23);
			m_cmbVariant.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			m_cmbVariant.Items.AddRange(VariantNames);
			int iVar = Array.IndexOf(VariantKeys, this.SelectedVariant);
			m_cmbVariant.SelectedIndex = ((iVar >= 0) ? iVar : 0);
			m_cmbVariant.SelectedIndexChanged += this.OnVariantChanged;
			this.Controls.Add(m_cmbVariant);

			m_ilIcons = new ImageList();
			m_ilIcons.ColorDepth = ColorDepth.Depth32Bit;
			m_ilIcons.ImageSize = new Size(PreviewSize, PreviewSize);

			m_lvIcons = new ListView();
			m_lvIcons.Bounds = new Rectangle(12, 44, 596, 350);
			m_lvIcons.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left |
				AnchorStyles.Right;
			m_lvIcons.View = View.LargeIcon;
			m_lvIcons.LargeImageList = m_ilIcons;
			m_lvIcons.MultiSelect = false;
			m_lvIcons.HideSelection = false;
			m_lvIcons.SelectedIndexChanged += this.OnIconSelectionChanged;
			m_lvIcons.ItemActivate += this.OnIconActivate;
			this.Controls.Add(m_lvIcons);

			m_lblStatus = new Label();
			m_lblStatus.AutoSize = false;
			m_lblStatus.AutoEllipsis = true;
			m_lblStatus.Bounds = new Rectangle(12, 402, 596, 20);
			m_lblStatus.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			this.Controls.Add(m_lblStatus);

			m_lnkSource = new LinkLabel();
			m_lnkSource.Text = "Icons: thesvg.org \u2013 Marken geh\u00f6ren ihren jeweiligen Inhabern";
			m_lnkSource.AutoSize = true;
			m_lnkSource.Location = new Point(12, 438);
			m_lnkSource.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			m_lnkSource.LinkArea = new LinkArea(7, 10);
			m_lnkSource.LinkClicked += delegate(object sender, LinkLabelLinkClickedEventArgs e)
			{
				try { System.Diagnostics.Process.Start("https://thesvg.org"); }
				catch(Exception) { }
			};
			this.Controls.Add(m_lnkSource);

			m_btnOK = new Button();
			m_btnOK.Text = (m_nEntries > 1) ? ("F\u00fcr " + m_nEntries.ToString() +
				" Eintr\u00e4ge \u00fcbernehmen") : "\u00dcbernehmen";
			m_btnOK.AutoSize = true;
			m_btnOK.MinimumSize = new Size(100, 28);
			m_btnOK.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			m_btnOK.Enabled = false;
			m_btnOK.DialogResult = DialogResult.OK;
			m_btnOK.UseVisualStyleBackColor = true;
			this.Controls.Add(m_btnOK);

			m_btnCancel = new Button();
			m_btnCancel.Text = "Abbrechen";
			m_btnCancel.Size = new Size(100, 28);
			m_btnCancel.Location = new Point(508, 432);
			m_btnCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			m_btnCancel.DialogResult = DialogResult.Cancel;
			m_btnCancel.UseVisualStyleBackColor = true;
			this.Controls.Add(m_btnCancel);

			this.AcceptButton = m_btnOK;
			this.CancelButton = m_btnCancel;

			m_tmrSearch = new System.Windows.Forms.Timer();
			m_tmrSearch.Interval = 300;
			m_tmrSearch.Tick += this.OnSearchTimerTick;

			this.ResumeLayout(false);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			m_btnOK.Location = new Point(m_btnCancel.Left - 8 - m_btnOK.Width, m_btnCancel.Top);
			ThemeManager.ApplyToForm(this);

			m_tbSearch.Text = m_strInitialQuery;
			m_tbSearch.SelectAll();
			SetStatus("Lade Icon-Katalog von thesvg.org \u2026");

			ThreadPool.QueueUserWorkItem(delegate(object o)
			{
				List<SvgIconInfo> l = null;
				string strError = null;
				try { l = TheSvgCatalog.Load(false); }
				catch(Exception ex) { strError = Net.GetErrorMessage(ex); }

				PostToUi(delegate()
				{
					if(strError != null) { SetStatus(strError); return; }
					m_lCatalog = l;
					RunSearch();
				});
			});
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			m_bClosed = true;
			++m_iGeneration;
			m_tmrSearch.Stop();
			m_tmrSearch.Dispose();
			base.OnFormClosed(e);
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing && (m_ilIcons != null))
			{
				m_ilIcons.Dispose();
				m_ilIcons = null;
			}
			base.Dispose(disposing);
		}

		private void PostToUi(MethodInvoker f)
		{
			if(m_bClosed) return;
			try
			{
				if(this.IsDisposed || !this.IsHandleCreated) return;
				this.BeginInvoke(new MethodInvoker(delegate()
				{
					if(!m_bClosed) f();
				}));
			}
			catch(Exception) { }
		}

		private void SetStatus(string strText)
		{
			m_lblStatus.Text = strText;
		}

		private string CurrentVariant
		{
			get
			{
				int i = m_cmbVariant.SelectedIndex;
				return (((i >= 0) && (i < VariantKeys.Length)) ? VariantKeys[i] : "default");
			}
		}

		// ===================== Search =====================

		private void OnSearchTextChanged(object sender, EventArgs e)
		{
			m_tmrSearch.Stop();
			m_tmrSearch.Start();
		}

		private void OnSearchKeyDown(object sender, KeyEventArgs e)
		{
			if((e.KeyCode == Keys.Down) && (m_lvIcons.Items.Count > 0))
			{
				m_lvIcons.Focus();
				m_lvIcons.Items[0].Selected = true;
				m_lvIcons.Items[0].Focused = true;
				e.Handled = true;
			}
		}

		private void OnSearchTimerTick(object sender, EventArgs e)
		{
			m_tmrSearch.Stop();
			RunSearch();
		}

		private void OnVariantChanged(object sender, EventArgs e)
		{
			RunSearch(); // Previews depend on the variant
		}

		private void RunSearch()
		{
			if(m_lCatalog == null) return;

			int iGen = ++m_iGeneration;
			List<SvgIconInfo> lResults = TheSvgCatalog.Search(m_lCatalog, m_tbSearch.Text, MaxResults);

			m_lvIcons.BeginUpdate();
			m_lvIcons.Items.Clear();
			m_ilIcons.Images.Clear();
			m_ilIcons.Images.Add(CreatePlaceholder());
			foreach(SvgIconInfo ii in lResults)
			{
				ListViewItem lvi = new ListViewItem(ii.Title, 0);
				lvi.Tag = ii;
				lvi.ToolTipText = ii.Title + " (" + ii.Slug + ")";
				m_lvIcons.Items.Add(lvi);
			}
			m_lvIcons.EndUpdate();
			m_lvIcons.ShowItemToolTips = true;
			UpdateButtons();

			if(lResults.Count == 0)
			{
				SetStatus("Keine Icons gefunden. Tipp: Markenname oder Domain eingeben.");
				return;
			}
			SetStatus(lResults.Count.ToString() + ((lResults.Count >= MaxResults) ?
				"+ Treffer \u2013 Suche verfeinern f\u00fcr weitere" : " Treffer"));

			if(lResults.Count > 0)
			{
				m_lvIcons.Items[0].Selected = true;
				m_lvIcons.Items[0].Focused = true;
			}

			// Load previews one after another in the background
			string strVariant = this.CurrentVariant;
			ThreadPool.QueueUserWorkItem(delegate(object o)
			{
				for(int i = 0; i < lResults.Count; ++i)
				{
					if(m_bClosed || (iGen != m_iGeneration)) return;

					Bitmap bmp = null;
					try { bmp = IconAssigner.RenderPreview(lResults[i], strVariant, PreviewSize); }
					catch(Exception) { bmp = null; }
					if(bmp == null) continue;
					if(m_bClosed || (iGen != m_iGeneration)) { bmp.Dispose(); return; }

					int iItem = i;
					PostToUi(delegate()
					{
						if((iGen != m_iGeneration) || (iItem >= m_lvIcons.Items.Count))
						{
							bmp.Dispose();
							return;
						}
						m_ilIcons.Images.Add(bmp);
						m_lvIcons.Items[iItem].ImageIndex = m_ilIcons.Images.Count - 1;
						bmp.Dispose(); // ImageList keeps its own copy
					});
				}
			});
		}

		private static Bitmap CreatePlaceholder()
		{
			Bitmap bmp = new Bitmap(PreviewSize, PreviewSize);
			using(Graphics g = Graphics.FromImage(bmp))
			{
				g.Clear(Color.Transparent);
				g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
				using(Pen p = new Pen(Color.FromArgb(90, 128, 128, 128), 2f))
				{
					p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;
					g.DrawEllipse(p, 10, 10, PreviewSize - 20, PreviewSize - 20);
				}
			}
			return bmp;
		}

		// ===================== Selection =====================

		private SvgIconInfo GetSelected()
		{
			if(m_lvIcons.SelectedItems.Count != 1) return null;
			return (m_lvIcons.SelectedItems[0].Tag as SvgIconInfo);
		}

		private void UpdateButtons()
		{
			m_btnOK.Enabled = (GetSelected() != null);
		}

		private void OnIconSelectionChanged(object sender, EventArgs e)
		{
			UpdateButtons();
			SvgIconInfo ii = GetSelected();
			if(ii == null) return;

			string strVariant = this.CurrentVariant;
			string strInfo = ii.Title;
			if(ii.Url.Length > 0) strInfo += "  \u2013  " + ii.Url;
			if(!ii.Variants.ContainsKey(strVariant))
				strInfo += "  (Variante nicht vorhanden, Standard wird verwendet)";
			SetStatus(strInfo);
		}

		private void OnIconActivate(object sender, EventArgs e)
		{
			if(GetSelected() != null)
			{
				this.DialogResult = DialogResult.OK;
				this.Close();
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if(this.DialogResult == DialogResult.OK)
			{
				SvgIconInfo ii = GetSelected();
				if(ii == null) { e.Cancel = true; return; }
				this.SelectedIcon = ii;
				this.SelectedVariant = this.CurrentVariant;
			}
			base.OnFormClosing(e);
		}
	}
}
