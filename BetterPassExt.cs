/*
  BetterPass - KeePass plugin
  Entry point. KeePass requires: namespace == assembly name,
  plugin class name == namespace + "Ext".

  Features:
  - TOTP fields in the entry dialog (code with countdown, key, digits,
    period, algorithm, issuer)
  - Have I Been Pwned check (entry dialog and whole database)
  - Brand icons from thesvg.org
  - Modern design: Windows 11 or aethercert, light or dark
  - Password generator profiles: Apple style (default), Chrome, Edge,
    Firefox, 1Password, Bitwarden; Apple style is customizable
  - Settings: Tools > Options > BetterPass, or Tools > BetterPass
*/

using System;
using System.Drawing;
using System.Windows.Forms;

using KeePass.Forms;
using KeePass.Plugins;
using KeePass.UI;

using KeePassLib.Utility;

namespace BetterPass
{
	public sealed class BetterPassExt : Plugin
	{
		private IPluginHost m_host = null;

		public override bool Initialize(IPluginHost host)
		{
			if(host == null) return false;
			m_host = host;

			BpConfig.Initialize(host.CustomConfig);
			ThemeManager.Initialize(host);
			BpPwGenerator.Register(host);
			try { PwProfiles.InstallOnce(); }
			catch(Exception) { } // Never block KeePass' start

			GlobalWindowManager.WindowAdded += this.OnWindowAdded;
			host.MainWindow.FileOpened += this.OnFileOpened;
			return true;
		}

		public override void Terminate()
		{
			if(m_host == null) return;

			GlobalWindowManager.WindowAdded -= this.OnWindowAdded;
			m_host.MainWindow.FileOpened -= this.OnFileOpened;
			ThemeManager.Terminate();
			BpPwGenerator.Unregister(m_host);
			m_host = null;
		}

		public override string UpdateUrl
		{
			get { return null; }
		}

		// ===================== Menus =====================

		public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
		{
			if(t == PluginMenuType.Main)
			{
				ToolStripMenuItem tsmi = new ToolStripMenuItem("BetterPass");
				tsmi.DropDownItems.Add(CreateItem("Einstellungen \u2026", delegate(object sender, EventArgs e)
				{
					OptionsDialog.ShowDialogEx(m_host.MainWindow);
				}));
				tsmi.DropDownItems.Add(new ToolStripSeparator());
				tsmi.DropDownItems.Add(CreateItem("Alle Passw\u00f6rter mit Have I Been Pwned pr\u00fcfen \u2026",
					delegate(object sender, EventArgs e)
				{
					HibpDatabaseCheck.RunInteractive(m_host.MainWindow);
				}));
				tsmi.DropDownItems.Add(CreateItem("Passende Icons automatisch zuweisen \u2026",
					delegate(object sender, EventArgs e)
				{
					IconAssigner.AutoAssign(m_host.MainWindow);
				}));
				return tsmi;
			}

			if(t == PluginMenuType.Entry)
			{
				ToolStripMenuItem tsmi = new ToolStripMenuItem("BetterPass");
				tsmi.DropDownItems.Add(CreateItem("Icon von theSVG w\u00e4hlen \u2026",
					delegate(object sender, EventArgs e)
				{
					IconAssigner.ShowPicker(m_host.MainWindow);
				}));
				tsmi.DropDownItems.Add(CreateItem("Passende Icons automatisch zuweisen \u2026",
					delegate(object sender, EventArgs e)
				{
					IconAssigner.AutoAssign(m_host.MainWindow);
				}));
				tsmi.DropDownItems.Add(new ToolStripSeparator());
				tsmi.DropDownItems.Add(CreateItem("Alle Passw\u00f6rter mit Have I Been Pwned pr\u00fcfen \u2026",
					delegate(object sender, EventArgs e)
				{
					HibpDatabaseCheck.RunInteractive(m_host.MainWindow);
				}));
				return tsmi;
			}

			return null;
		}

		private static ToolStripMenuItem CreateItem(string strText, EventHandler h)
		{
			ToolStripMenuItem tsmi = new ToolStripMenuItem(strText);
			tsmi.Click += delegate(object sender, EventArgs e)
			{
				try { h(sender, e); }
				catch(Exception ex) { MessageService.ShowWarning("BetterPass:", ex.Message); }
			};
			return tsmi;
		}

		// ===================== Windows =====================

		private void OnWindowAdded(object sender, GwmWindowEventArgs e)
		{
			Form f = e.Form;
			if(f == null) return;

			try
			{
				OptionsForm fOpt = (f as OptionsForm);
				if(fOpt != null) OptionsFormExtender.Attach(fOpt);

				PwEntryForm fEntry = (f as PwEntryForm);
				if(fEntry != null) fEntry.Shown += this.OnEntryFormShown;

				ThemeManager.ApplyToForm(f);
			}
			catch(Exception ex)
			{
				MessageService.ShowWarning("BetterPass:", ex.Message);
			}
		}

		private void OnEntryFormShown(object sender, EventArgs e)
		{
			PwEntryForm f = (sender as PwEntryForm);
			if(f == null) return;
			f.Shown -= this.OnEntryFormShown;

			try { EntryDialogExtender.Attach(f); }
			catch(Exception ex)
			{
				MessageService.ShowWarning("BetterPass:", ex.Message);
			}
		}

		private void OnFileOpened(object sender, FileOpenedEventArgs e)
		{
			if(!BpConfig.HibpCheckOnOpen) return;
			if((e == null) || (e.Database == null)) return;

			try { HibpDatabaseCheck.RunInBackground(m_host.MainWindow, e.Database); }
			catch(Exception) { }
		}
	}

	internal static class EntryDialogExtender
	{
		public static void Attach(PwEntryForm f)
		{
			if((f == null) || f.IsDisposed) return;
			if(f.MultipleValuesEntryContext != null) return; // Several entries at once
			if(f.EntryStrings == null) return;

			bool bTotp = BpConfig.TotpShowFields;
			bool bHibp = BpConfig.HibpEntryCheck;
			if(!bTotp && !bHibp) return;

			EntryFormLayout l = EntryFormLayout.TryCreate(f);
			if(l == null) return; // Unknown dialog layout (future KeePass version)

			if(bHibp) HibpSection.Attach(l);
			if(bTotp) TotpSection.Attach(l);

			// Newly added controls must be themed as well
			ThemeManager.ApplyToForm(f);
		}
	}
}
