/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Modern design for KeePass' main window and dialogs: Windows 11 or
  aethercert, each light or dark (see ThemePalette).

  Colors are applied recursively to all controls of a window; controls added
  later are themed automatically. Explicit custom colors (e.g. entry colors
  chosen by the user) are left untouched; only system colors and colors of
  BetterPass' own palettes are replaced, so designs can be switched live.

  Switching between designs and color modes takes effect immediately.
  Switching back to KeePass' classic look takes full effect after restarting
  KeePass, because original colors of open windows are not restored.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

using Microsoft.Win32;

using KeePass.Forms;
using KeePass.Plugins;

namespace BetterPass
{
	internal static class ThemeManager
	{
		private static ThemePalette g_palette = ThemePalette.Windows11(true);
		private static readonly ConditionalWeakTable<Control, object> g_themed =
			new ConditionalWeakTable<Control, object>();
		private static readonly object g_oMarker = new object();
		private static readonly Dictionary<int, bool> g_dOurColors = new Dictionary<int, bool>();

		private static IPluginHost g_host = null;
		private static bool g_bActive = false;
		private static bool g_bAppliedOnce = false;
		private static ToolStripRenderer g_tsrOriginal = null;
		private static ModernToolStripRenderer g_tsrModern = null;
		private static bool g_bInFix = false;

		public static ThemePalette Palette { get { return g_palette; } }
		public static bool IsActive { get { return g_bActive; } }
		public static bool IsDark { get { return (g_bActive && g_palette.IsDark); } }

		// Colors for plugin UI that must look right in every design
		public static Color TextColor
		{
			get { return (g_bActive ? g_palette.Text : SystemColors.WindowText); }
		}
		public static Color DimTextColor
		{
			get { return (g_bActive ? g_palette.DimText : SystemColors.GrayText); }
		}
		public static Color ErrorColor
		{
			get { return (g_bActive ? g_palette.Error : Color.Firebrick); }
		}
		public static Color SuccessColor
		{
			get { return (g_bActive ? g_palette.Success : Color.FromArgb(30, 125, 50)); }
		}
		public static Color WarningColor
		{
			get { return (g_bActive ? g_palette.Warning : Color.FromArgb(200, 60, 40)); }
		}
		public static Color AccentColor
		{
			get { return (g_bActive ? g_palette.Accent : Color.FromArgb(31, 95, 125)); }
		}

		// ===================== Lifecycle =====================

		public static void Initialize(IPluginHost host)
		{
			g_host = host;
			try { SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; }
			catch(Exception) { }
			if(host.MainWindow != null) host.MainWindow.UIStateUpdated += OnUiStateUpdated;
			Update();
		}

		public static void Terminate()
		{
			try { SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; }
			catch(Exception) { }
			if((g_host != null) && (g_host.MainWindow != null))
				g_host.MainWindow.UIStateUpdated -= OnUiStateUpdated;
			BannerPainter.Uninstall();
			g_host = null;
		}

		public static bool ShouldBeDark()
		{
			ThemeMode m = BpConfig.ColorMode;
			if(m == ThemeMode.Dark) return true;
			if(m == ThemeMode.Light) return false;
			return IsSystemDark();
		}

		public static bool IsSystemDark()
		{
			try
			{
				using(RegistryKey k = Registry.CurrentUser.OpenSubKey(
					"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", false))
				{
					if(k == null) return false;
					object o = k.GetValue("AppsUseLightTheme");
					if(o is int) return ((int)o == 0);
				}
			}
			catch(Exception) { }
			return false;
		}

		/// <summary>The palette the settings ask for, or null for KeePass' classic look.</summary>
		private static ThemePalette GetTargetPalette()
		{
			if(SystemInformation.HighContrast) return null;

			ThemeDesign d = BpConfig.Design;
			if(d == ThemeDesign.Classic) return null;
			bool bDark = ShouldBeDark();
			return ((d == ThemeDesign.Aethercert) ? ThemePalette.Aethercert(bDark) :
				ThemePalette.Windows11(bDark));
		}

		/// <summary>
		/// Re-evaluates the settings. Returns true if KeePass should be
		/// restarted to fully apply the change (back to the classic look).
		/// </summary>
		public static bool Update()
		{
			ThemePalette p = GetTargetPalette();

			if(p != null)
			{
				if(g_bActive && (p.Key == g_palette.Key)) return false;

				bool bSwitch = g_bActive;
				g_palette = p;
				foreach(Color c in p.AllColors()) g_dOurColors[c.ToArgb()] = true;
				g_bActive = true;
				g_bAppliedOnce = true;

				ApplyRenderer();
				BannerPainter.Install();
				foreach(Form f in GetOpenForms())
				{
					ApplyToForm(f);
					if(bSwitch)
					{
						ApplyNativeRecursive(f);
						RefreshAll(f);
					}
				}
				UpdateMainIcons();
				return false;
			}

			if(!g_bActive) return false;

			// Back to classic: new windows are no longer themed; restore menus
			g_bActive = false;
			if(g_tsrOriginal != null) ToolStripManager.Renderer = g_tsrOriginal;
			BannerPainter.Uninstall();
			foreach(Form f in GetOpenForms())
			{
				if(f.IsHandleCreated) ThemeNative.TrySetTitleBar(f.Handle, null);
				RefreshAll(f);
			}
			return g_bAppliedOnce;
		}

		private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			// "Like Windows" follows light/dark; Windows 11 also follows the accent color
			if((BpConfig.ColorMode != ThemeMode.System) && (BpConfig.Design != ThemeDesign.Windows11))
				return;
			if((e.Category != UserPreferenceCategory.General) &&
				(e.Category != UserPreferenceCategory.Color) &&
				(e.Category != UserPreferenceCategory.VisualStyle)) return;

			MainForm mf = ((g_host != null) ? g_host.MainWindow : null);
			if((mf == null) || mf.IsDisposed) return;

			try
			{
				mf.BeginInvoke(new MethodInvoker(delegate()
				{
					if(g_bActive) Update(); // Switching to classic only via the settings
				}));
			}
			catch(Exception) { }
		}

		private static void OnUiStateUpdated(object sender, EventArgs e)
		{
			if(g_bActive) UpdateMainIcons();
		}

		/// <summary>
		/// Entry/group icons, toolbar and tray menu of the main window. KeePass
		/// rebuilds its icon list and updates toolbar items at runtime, so this
		/// runs again after every UI state update (cheap when nothing changed).
		/// </summary>
		private static void UpdateMainIcons()
		{
			MainForm mf = ((g_host != null) ? g_host.MainWindow : null);
			if((mf == null) || mf.IsDisposed || !IconSet.Available) return;

			try
			{
				if(IconSet.ApplyToClientIcons(mf.ClientIcons))
				{
					foreach(string strName in new string[] { "m_lvEntries", "m_tvGroups" })
					{
						Control[] v = mf.Controls.Find(strName, true);
						if((v != null) && (v.Length > 0)) v[0].Invalidate();
					}
				}

				foreach(Control c in mf.Controls)
				{
					ToolStrip ts = (c as ToolStrip);
					if(ts != null) IconSet.ApplyToStrip(ts);
					else foreach(Control cChild in c.Controls)
					{
						ToolStrip tsChild = (cChild as ToolStrip);
						if(tsChild != null) IconSet.ApplyToStrip(tsChild);
					}
				}

				NotifyIcon ni = mf.MainNotifyIcon;
				if((ni != null) && (ni.ContextMenuStrip != null)) IconSet.ApplyToStrip(ni.ContextMenuStrip);
			}
			catch(Exception) { }
		}

		private static void ApplyRenderer()
		{
			if(!(ToolStripManager.Renderer is ModernToolStripRenderer))
				g_tsrOriginal = ToolStripManager.Renderer;
			if(g_tsrModern == null) g_tsrModern = new ModernToolStripRenderer();
			ToolStripManager.Renderer = g_tsrModern;
		}

		/// <summary>
		/// KeePass resets the global renderer when its options are saved.
		/// </summary>
		public static void ReapplyRenderer()
		{
			if(g_bActive) ApplyRenderer();
		}

		private static Form[] GetOpenForms()
		{
			FormCollection fc = Application.OpenForms;
			Form[] v = new Form[fc.Count];
			for(int i = 0; i < v.Length; ++i) v[i] = fc[i];
			return v;
		}

		private static void RefreshAll(Control c)
		{
			try
			{
				c.Invalidate(true);
				RedrawFramesRecursive(c);
			}
			catch(Exception) { }
		}

		private static void RedrawFramesRecursive(Control c)
		{
			if(c.IsHandleCreated) ThemeNative.RedrawFrame(c.Handle);
			foreach(Control cChild in c.Controls) RedrawFramesRecursive(cChild);
		}

		// ===================== Applying =====================

		public static void ApplyToForm(Form f)
		{
			if(!g_bActive || (f == null) || f.IsDisposed) return;

			try
			{
				f.SuspendLayout();
				ApplyControl(f);
			}
			catch(Exception) { }
			finally
			{
				try { f.ResumeLayout(true); }
				catch(Exception) { }
			}

			// Some KeePass dialogs set list view themes in their Load handler,
			// after our handler; apply native themes once more when shown
			if(MarkFlag(f, "shown"))
				f.Shown += delegate(object sender, EventArgs e) { ApplyNativeRecursive(f); };
		}

		private static bool IsMarked(Control c)
		{
			object o;
			return g_themed.TryGetValue(c, out o);
		}

		private static bool Mark(Control c)
		{
			if(IsMarked(c)) return false;
			try { g_themed.Add(c, g_oMarker); }
			catch(ArgumentException) { return false; }
			return true;
		}

		private static readonly ConditionalWeakTable<Control, object> g_flags =
			new ConditionalWeakTable<Control, object>();

		private static bool MarkFlag(Control c, string strFlag)
		{
			object o;
			if(g_flags.TryGetValue(c, out o)) return false;
			try { g_flags.Add(c, strFlag); }
			catch(ArgumentException) { return false; }
			return true;
		}

		private static void ApplyControl(Control c)
		{
			if(c == null) return;
			bool bFirst = Mark(c);

			ThemePalette p = g_palette;

			Form f = (c as Form);
			TabControl tc = (c as TabControl);
			TabPage tp = (c as TabPage);
			ButtonBase btn = (c as ButtonBase);
			TextBoxBase tb = (c as TextBoxBase);
			ListView lv = (c as ListView);
			TreeView tv = (c as TreeView);
			ComboBox cmb = (c as ComboBox);
			UpDownBase ud = (c as UpDownBase);
			ToolStrip ts = (c as ToolStrip);
			DataGridView dgv = (c as DataGridView);
			LinkLabel ll = (c as LinkLabel);
			GroupBox gb = (c as GroupBox);

			if(f != null)
			{
				SetBack(c, p.Back);
				SetFore(c, p.Text);
			}
			else if(tc != null)
			{
				if(bFirst) TabControlPainter.Attach(tc);
			}
			else if(tp != null)
			{
				tp.UseVisualStyleBackColor = false;
				SetBack(c, p.Back);
				SetFore(c, p.Text);
			}
			else if(btn != null)
			{
				ApplyButton(btn, p, bFirst);
			}
			else if(tb != null)
			{
				SetBack(c, (BorderPainter.IsInputControl(tb) ? p.InputFill : p.Surface));
				SetFore(c, p.Text);
				if(bFirst)
				{
					BorderPainter.Attach(tb);
					tb.BackColorChanged += OnInputBackColorChanged;
					RichTextBox rtb = (tb as RichTextBox);
					if(rtb != null)
					{
						rtb.TextChanged += OnRichTextChanged;
						RecolorRichText(rtb);
					}
				}
				else if(tb is RichTextBox) RecolorRichText((RichTextBox)tb);
			}
			else if(lv != null)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
				if(bFirst)
				{
					BorderPainter.Attach(lv);
					ListViewHeaderFixer.Attach(lv);
					lv.BackColorChanged += OnInputBackColorChanged;
				}
			}
			else if(tv != null)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
				tv.LineColor = p.DimText;
				if(bFirst)
				{
					BorderPainter.Attach(tv);
					tv.BackColorChanged += OnInputBackColorChanged;
				}
			}
			else if(cmb != null)
			{
				SetBack(c, p.InputFill);
				SetFore(c, p.Text);
				if(bFirst) ComboPainter.Attach(cmb);
			}
			else if(ud != null)
			{
				SetBack(c, p.InputFill);
				SetFore(c, p.Text);
				if(bFirst)
				{
					BorderPainter.Attach(ud);
					SpinPainter.Attach(ud);
				}
			}
			else if(c is ListBox)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
				if(bFirst) BorderPainter.Attach(c);
			}
			else if(c is DateTimePicker)
			{
				if(bFirst) BorderPainter.Attach(c);
			}
			else if(ts != null)
			{
				if(ts.RenderMode != ToolStripRenderMode.ManagerRenderMode)
				{
					if(g_tsrModern == null) g_tsrModern = new ModernToolStripRenderer();
					ts.Renderer = g_tsrModern;
				}
				SetBack(c, p.Back);
				SetFore(c, p.Text);
				IconSet.ApplyToStrip(ts);
				foreach(ToolStripItem tsi in ts.Items)
				{
					ToolStripControlHost h = (tsi as ToolStripControlHost);
					if((h != null) && (h.Control != null)) ApplyControl(h.Control);
				}
			}
			else if(dgv != null)
			{
				dgv.EnableHeadersVisualStyles = false;
				dgv.BorderStyle = BorderStyle.FixedSingle;
				dgv.BackgroundColor = p.Surface;
				dgv.GridColor = p.Border;
				dgv.DefaultCellStyle.BackColor = p.Surface;
				dgv.DefaultCellStyle.ForeColor = p.Text;
				dgv.DefaultCellStyle.SelectionBackColor = p.Hover;
				dgv.DefaultCellStyle.SelectionForeColor = p.Text;
				dgv.ColumnHeadersDefaultCellStyle.BackColor = p.Back;
				dgv.ColumnHeadersDefaultCellStyle.ForeColor = p.DimText;
			}
			else if(ll != null)
			{
				SetFore(c, p.Text);
				ll.LinkColor = p.Link;
				ll.ActiveLinkColor = p.Link;
				ll.VisitedLinkColor = p.Link;
			}
			else if(gb != null)
			{
				SetBack(c, p.Back);
				SetFore(c, p.Text);
				if(bFirst) GroupBoxPainter.Attach(gb);
			}
			else if(Gfx.IsType(c, "QualityProgressBar"))
			{
				if(bFirst) MeterPainter.Attach(c);
				c.Invalidate();
			}
			else if((c is Label) || (c is Panel) || (c is SplitContainer) ||
				(c is UserControl) || (c is TableLayoutPanel) || (c is FlowLayoutPanel))
			{
				SetBack(c, p.Back);
				SetFore(c, p.Text);
			}
			// Other controls (PictureBox, ProgressBar, custom KeePass controls, ...)
			// keep their own painting

			if(c.ContextMenuStrip != null) IconSet.ApplyToStrip(c.ContextMenuStrip);

			if(bFirst)
			{
				c.ControlAdded += OnControlAdded;
				c.HandleCreated += OnHandleCreated;
			}
			if(c.IsHandleCreated) ApplyNative(c);

			foreach(Control cChild in c.Controls) ApplyControl(cChild);
		}

		private static void ApplyButton(ButtonBase btn, ThemePalette p, bool bFirst)
		{
			CheckBox cb = (btn as CheckBox);
			RadioButton rb = (btn as RadioButton);
			bool bLooksLikeButton = (((cb == null) && (rb == null)) ||
				((cb != null) && (cb.Appearance == Appearance.Button)) ||
				((rb != null) && (rb.Appearance == Appearance.Button)));

			if(!bLooksLikeButton)
			{
				// Plain check box / radio button: text color and a modern glyph
				SetFore(btn, p.Text);
				if(btn.BackColor.IsSystemColor) btn.BackColor = Color.Transparent;
				if(bFirst) CheckGlyphPainter.Attach(btn);
				return;
			}

			IconSet.ApplyToButton(btn);

			// The native split button ("Tools") is drawn by Windows; dark/light
			// native theme only (see ApplyNative)
			if(Gfx.IsType(btn, "SplitButtonEx")) return;

			btn.UseVisualStyleBackColor = false;
			SetBack(btn, p.Control);
			SetFore(btn, p.Text);

			// Color picker buttons paint a color swatch themselves
			if(Gfx.IsType(btn, "ColorButtonEx"))
			{
				btn.FlatStyle = FlatStyle.Flat;
				btn.FlatAppearance.BorderColor = p.Border;
				btn.FlatAppearance.MouseOverBackColor = p.Hover;
				btn.FlatAppearance.MouseDownBackColor = p.Pressed;
				return;
			}

			if(bFirst) ButtonPainter.Attach(btn);
			else btn.Invalidate();
		}

		private static void ApplyNativeRecursive(Control c)
		{
			if(!g_bActive || (c == null) || c.IsDisposed) return;
			if(c.IsHandleCreated) ApplyNative(c);
			foreach(Control cChild in c.Controls) ApplyNativeRecursive(cChild);
		}

		private static void ApplyNative(Control c)
		{
			if(!g_bActive) return;
			bool bDark = g_palette.IsDark;

			if(c is Form) ThemeNative.TrySetTitleBar(c.Handle, g_palette);
			else if(c is ListView)
			{
				ThemeNative.TrySetWindowTheme(c.Handle, (bDark ? "DarkMode_Explorer" : "Explorer"));
				IntPtr hHeader = ThemeNative.SendMessage(c.Handle, ThemeNative.LVM_GETHEADER,
					IntPtr.Zero, IntPtr.Zero);
				if(hHeader != IntPtr.Zero)
					ThemeNative.TrySetWindowTheme(hHeader, (bDark ? "DarkMode_ItemsView" : "ItemsView"));
			}
			else if((c is TreeView) || (c is TextBoxBase) || (c is ListBox) ||
				(c is Panel) || (c is UserControl) || Gfx.IsType(c, "SplitButtonEx"))
				ThemeNative.TrySetWindowTheme(c.Handle, (bDark ? "DarkMode_Explorer" : "Explorer"));
			else if(c is ComboBox)
				ThemeNative.TrySetWindowTheme(c.Handle, (bDark ? "DarkMode_CFD" : null));
		}

		private static bool IsOurColor(Color c)
		{
			return g_dOurColors.ContainsKey(c.ToArgb());
		}

		private static void SetBack(Control c, Color clr)
		{
			Color cur = c.BackColor;
			if(cur == clr) return;
			if(cur.IsSystemColor || (cur.ToArgb() == Color.White.ToArgb()) || IsOurColor(cur))
				c.BackColor = clr;
		}

		private static void SetFore(Control c, Color clr)
		{
			Color cur = c.ForeColor;
			if(cur == clr) return;
			if(cur.IsSystemColor || (cur.ToArgb() == Color.Black.ToArgb()) || IsOurColor(cur))
				c.ForeColor = clr;
		}

		// ===================== Events =====================

		private static void OnControlAdded(object sender, ControlEventArgs e)
		{
			if(!g_bActive) return;
			try { ApplyControl(e.Control); }
			catch(Exception) { }
		}

		private static void OnHandleCreated(object sender, EventArgs e)
		{
			if(!g_bActive) return;
			Control c = (sender as Control);
			if(c != null) ApplyNative(c);
		}

		// KeePass resets text box colors (e.g. after a validation error) and
		// the group tree color (when a database is opened)
		private static void OnInputBackColorChanged(object sender, EventArgs e)
		{
			if(!g_bActive || g_bInFix) return;
			Control c = (sender as Control);
			if(c == null) return;

			Color cur = c.BackColor;
			if((cur == SystemColors.Window) || (cur == SystemColors.Control) ||
				(cur.ToArgb() == Color.White.ToArgb()))
			{
				g_bInFix = true;
				try
				{
					c.BackColor = ((BorderPainter.IsInputControl(c) && !(c is ListView) &&
						!(c is TreeView)) ? (c.ContainsFocus ? g_palette.InputFillFocus :
						g_palette.InputFill) : g_palette.Surface);
				}
				finally { g_bInFix = false; }
			}
		}

		// Rich text uses the system text color ("auto") unless recolored
		private static void OnRichTextChanged(object sender, EventArgs e)
		{
			if(!g_bActive) return;
			RichTextBox rtb = (sender as RichTextBox);
			if((rtb == null) || (rtb.Focused && !rtb.ReadOnly)) return;
			RecolorRichText(rtb);
		}

		private static void RecolorRichText(RichTextBox rtb)
		{
			if(!g_bActive || g_bInFix || !rtb.IsHandleCreated) return;
			if(rtb.TextLength > 200000) return;

			g_bInFix = true;
			try
			{
				ThemeNative.SendMessage(rtb.Handle, ThemeNative.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
				int iStart = rtb.SelectionStart, nLen = rtb.SelectionLength;
				rtb.SelectAll();
				rtb.SelectionColor = g_palette.Text;
				rtb.Select(iStart, nLen);
			}
			catch(Exception) { }
			finally
			{
				ThemeNative.SendMessage(rtb.Handle, ThemeNative.WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
				rtb.Invalidate();
				g_bInFix = false;
			}
		}
	}
}
