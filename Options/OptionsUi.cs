/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  - OptionsFormExtender: adds a "BetterPass" tab to Tools > Options
  - OptionsDialog: standalone settings dialog (Tools > BetterPass > Einstellungen)
*/

using System;
using System.Drawing;
using System.Windows.Forms;

using KeePass.Forms;

namespace BetterPass
{
	internal static class OptionsFormExtender
	{
		public static void Attach(OptionsForm f)
		{
			if(f == null) return;

			Control[] v = f.Controls.Find("m_tabMain", true);
			TabControl tc = (((v != null) && (v.Length > 0)) ? (v[0] as TabControl) : null);
			if(tc == null) return;

			TabPage tp = new TabPage("BetterPass");
			tp.UseVisualStyleBackColor = true;
			OptionsPanel panel = new OptionsPanel();
			panel.Dock = DockStyle.Fill;
			tp.Controls.Add(panel);
			tc.TabPages.Add(tp);

			f.FormClosed += delegate(object sender, FormClosedEventArgs e)
			{
				// KeePass re-initializes the menu renderer after ShowDialog returns
				// (UIUtil.Initialize); reapply ours once that has happened
				MainForm mfR = KeePass.Program.MainForm;
				if((mfR != null) && mfR.IsHandleCreated && !mfR.IsDisposed)
					mfR.BeginInvoke(new MethodInvoker(ThemeManager.ReapplyRenderer));
				else ThemeManager.ReapplyRenderer();

				if(f.DialogResult != DialogResult.OK) return;
				panel.SaveSettings();
				OptionsPanel.ApplyAfterSave(KeePass.Program.MainForm);
			};
		}
	}

	internal sealed class OptionsDialog : Form
	{
		private readonly OptionsPanel m_panel;

		public OptionsDialog()
		{
			this.Text = "BetterPass \u2013 Einstellungen";
			this.AutoScaleMode = AutoScaleMode.Font;
			this.Font = SystemFonts.MessageBoxFont;
			this.ClientSize = new Size(560, 600);
			this.MinimumSize = new Size(460, 400);
			this.StartPosition = FormStartPosition.CenterParent;
			this.ShowInTaskbar = false;
			this.MinimizeBox = false;
			this.MaximizeBox = false;
			this.ShowIcon = false;

			Panel pnlButtons = new Panel();
			pnlButtons.Dock = DockStyle.Bottom;
			pnlButtons.Height = 46;

			Button btnCancel = new Button();
			btnCancel.Text = "Abbrechen";
			btnCancel.Size = new Size(96, 28);
			btnCancel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnCancel.Location = new Point(pnlButtons.Width - 108, 9);
			btnCancel.DialogResult = DialogResult.Cancel;
			btnCancel.UseVisualStyleBackColor = true;
			pnlButtons.Controls.Add(btnCancel);

			Button btnOK = new Button();
			btnOK.Text = "OK";
			btnOK.Size = new Size(96, 28);
			btnOK.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			btnOK.Location = new Point(pnlButtons.Width - 212, 9);
			btnOK.DialogResult = DialogResult.OK;
			btnOK.UseVisualStyleBackColor = true;
			pnlButtons.Controls.Add(btnOK);

			m_panel = new OptionsPanel();
			m_panel.Dock = DockStyle.Fill;

			this.Controls.Add(m_panel);
			this.Controls.Add(pnlButtons);
			this.AcceptButton = btnOK;
			this.CancelButton = btnCancel;
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			ThemeManager.ApplyToForm(this);
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			if(this.DialogResult == DialogResult.OK)
			{
				m_panel.SaveSettings();
				OptionsPanel.ApplyAfterSave(this.Owner);
			}
			base.OnFormClosed(e);
		}

		public static void ShowDialogEx(IWin32Window owner)
		{
			using(OptionsDialog dlg = new OptionsDialog())
			{
				dlg.ShowDialog(owner);
			}
		}
	}
}
