/*
  BetterPass - KeePass plugin
  Dark mode for KeePass' main window and dialogs.

  Colors are applied recursively to all controls of a window; controls added
  later are themed automatically. Explicit custom colors (e.g. entry colors
  chosen by the user) are left untouched; only system colors are replaced.

  Switching dark mode on takes effect immediately. Switching it off takes
  full effect after restarting KeePass, because original colors of open
  windows are not restored.
*/

using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

using Microsoft.Win32;

using KeePass.Forms;
using KeePass.Plugins;

namespace BetterPass
{
	internal sealed class ThemePalette
	{
		public Color Back = Color.FromArgb(32, 32, 32);
		public Color Surface = Color.FromArgb(43, 43, 43);
		public Color Control = Color.FromArgb(55, 55, 55);
		public Color Hover = Color.FromArgb(62, 62, 64);
		public Color Pressed = Color.FromArgb(75, 75, 78);
		public Color Border = Color.FromArgb(70, 70, 70);
		public Color Text = Color.FromArgb(232, 232, 232);
		public Color DimText = Color.FromArgb(160, 160, 160);
		public Color Accent = Color.FromArgb(76, 145, 225);
		public Color Link = Color.FromArgb(110, 175, 255);
	}

	internal static class ThemeManager
	{
		private static readonly ThemePalette g_palette = new ThemePalette();
		private static readonly ConditionalWeakTable<Control, object> g_themed =
			new ConditionalWeakTable<Control, object>();
		private static readonly object g_oMarker = new object();

		private static IPluginHost g_host = null;
		private static bool g_bActive = false;
		private static bool g_bAppliedOnce = false;
		private static ToolStripRenderer g_tsrOriginal = null;
		private static DarkToolStripRenderer g_tsrDark = null;
		private static bool g_bInFix = false;

		public static ThemePalette Palette { get { return g_palette; } }
		public static bool IsDarkActive { get { return g_bActive; } }

		// Colors for plugin UI that must look right in both modes
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
			get { return (g_bActive ? Color.FromArgb(255, 110, 100) : Color.Firebrick); }
		}
		public static Color SuccessColor
		{
			get { return (g_bActive ? Color.FromArgb(110, 205, 120) : Color.FromArgb(30, 125, 50)); }
		}

		// ===================== Lifecycle =====================

		public static void Initialize(IPluginHost host)
		{
			g_host = host;
			try { SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged; }
			catch(Exception) { }
			Update();
		}

		public static void Terminate()
		{
			try { SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged; }
			catch(Exception) { }
			g_host = null;
		}

		public static bool ShouldBeDark()
		{
			if(SystemInformation.HighContrast) return false;

			ThemeMode m = BpConfig.Theme;
			if(m == ThemeMode.Dark) return true;
			if(m == ThemeMode.Off) return false;
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

		/// <summary>
		/// Re-evaluates the setting. Returns true if KeePass should be
		/// restarted to fully apply the change (dark mode switched off).
		/// </summary>
		public static bool Update()
		{
			bool bDark = ShouldBeDark();
			if(bDark == g_bActive) return false;

			if(bDark)
			{
				g_bActive = true;
				g_bAppliedOnce = true;
				ApplyRenderer();
				foreach(Form f in GetOpenForms()) ApplyToForm(f);
				return false;
			}

			// Switching off: new windows are no longer themed; restore menus
			g_bActive = false;
			if(g_tsrOriginal != null) ToolStripManager.Renderer = g_tsrOriginal;
			foreach(Form f in GetOpenForms()) RefreshAll(f);
			return g_bAppliedOnce;
		}

		private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
		{
			if(BpConfig.Theme != ThemeMode.System) return;
			if((e.Category != UserPreferenceCategory.General) &&
				(e.Category != UserPreferenceCategory.Color) &&
				(e.Category != UserPreferenceCategory.VisualStyle)) return;

			MainForm mf = ((g_host != null) ? g_host.MainWindow : null);
			if((mf == null) || mf.IsDisposed) return;

			try
			{
				mf.BeginInvoke(new MethodInvoker(delegate()
				{
					if(!g_bActive && ShouldBeDark()) Update();
				}));
			}
			catch(Exception) { }
		}

		private static void ApplyRenderer()
		{
			if(!(ToolStripManager.Renderer is DarkToolStripRenderer))
				g_tsrOriginal = ToolStripManager.Renderer;
			if(g_tsrDark == null) g_tsrDark = new DarkToolStripRenderer();
			ToolStripManager.Renderer = g_tsrDark;
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
			try { c.Invalidate(true); }
			catch(Exception) { }
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
			ToolStrip ts = (c as ToolStrip);
			DataGridView dgv = (c as DataGridView);
			LinkLabel ll = (c as LinkLabel);

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
				ApplyButton(btn, p);
			}
			else if(tb != null)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
				if(bFirst)
				{
					tb.BackColorChanged += OnInputBackColorChanged;
					RichTextBox rtb = (tb as RichTextBox);
					if(rtb != null)
					{
						rtb.TextChanged += OnRichTextChanged;
						RecolorRichText(rtb);
					}
				}
			}
			else if(lv != null)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
				if(bFirst) ListViewHeaderFixer.Attach(lv);
			}
			else if(tv != null)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
				tv.LineColor = p.DimText;
			}
			else if(cmb != null)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
				if((cmb.DrawMode == DrawMode.Normal) && (cmb.FlatStyle != FlatStyle.Flat))
					cmb.FlatStyle = FlatStyle.Flat;
			}
			else if(c is UpDownBase)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
			}
			else if(c is ListBox)
			{
				SetBack(c, p.Surface);
				SetFore(c, p.Text);
			}
			else if(ts != null)
			{
				if(ts.RenderMode != ToolStripRenderMode.ManagerRenderMode)
				{
					if(g_tsrDark == null) g_tsrDark = new DarkToolStripRenderer();
					ts.Renderer = g_tsrDark;
				}
				SetBack(c, p.Back);
				SetFore(c, p.Text);
				foreach(ToolStripItem tsi in ts.Items)
				{
					ToolStripControlHost h = (tsi as ToolStripControlHost);
					if((h != null) && (h.Control != null)) ApplyControl(h.Control);
				}
			}
			else if(dgv != null)
			{
				dgv.EnableHeadersVisualStyles = false;
				dgv.BackgroundColor = p.Back;
				dgv.GridColor = p.Border;
				dgv.DefaultCellStyle.BackColor = p.Surface;
				dgv.DefaultCellStyle.ForeColor = p.Text;
				dgv.DefaultCellStyle.SelectionBackColor = p.Accent;
				dgv.DefaultCellStyle.SelectionForeColor = Color.White;
				dgv.ColumnHeadersDefaultCellStyle.BackColor = p.Control;
				dgv.ColumnHeadersDefaultCellStyle.ForeColor = p.Text;
			}
			else if(ll != null)
			{
				SetFore(c, p.Text);
				ll.LinkColor = p.Link;
				ll.ActiveLinkColor = p.Link;
				ll.VisitedLinkColor = p.Link;
			}
			else if((c is Label) || (c is GroupBox) || (c is Panel) || (c is SplitContainer) ||
				(c is UserControl) || (c is TableLayoutPanel) || (c is FlowLayoutPanel))
			{
				SetBack(c, p.Back);
				SetFore(c, p.Text);
			}
			// Other controls (PictureBox, ProgressBar, custom KeePass controls, ...)
			// keep their own painting

			if(bFirst)
			{
				c.ControlAdded += OnControlAdded;
				c.HandleCreated += OnHandleCreated;
			}
			if(c.IsHandleCreated) ApplyNative(c);

			foreach(Control cChild in c.Controls) ApplyControl(cChild);
		}

		private static void ApplyButton(ButtonBase btn, ThemePalette p)
		{
			CheckBox cb = (btn as CheckBox);
			RadioButton rb = (btn as RadioButton);
			bool bLooksLikeButton = (((cb == null) && (rb == null)) ||
				((cb != null) && (cb.Appearance == Appearance.Button)) ||
				((rb != null) && (rb.Appearance == Appearance.Button)));

			if(!bLooksLikeButton)
			{
				// Plain check box / radio button: only the text color
				SetFore(btn, p.Text);
				if(btn.BackColor.IsSystemColor) btn.BackColor = Color.Transparent;
				return;
			}

			btn.UseVisualStyleBackColor = false;
			btn.FlatStyle = FlatStyle.Flat;
			btn.FlatAppearance.BorderColor = p.Border;
			btn.FlatAppearance.MouseOverBackColor = p.Hover;
			btn.FlatAppearance.MouseDownBackColor = p.Pressed;
			if(cb != null) btn.FlatAppearance.CheckedBackColor = p.Pressed;
			SetBack(btn, p.Control);
			SetFore(btn, p.Text);
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

			if(c is Form) ThemeNative.TrySetDarkTitleBar(c.Handle, true);
			else if(c is ListView)
			{
				ThemeNative.TrySetWindowTheme(c.Handle, "DarkMode_Explorer");
				IntPtr hHeader = ThemeNative.SendMessage(c.Handle, ThemeNative.LVM_GETHEADER,
					IntPtr.Zero, IntPtr.Zero);
				if(hHeader != IntPtr.Zero) ThemeNative.TrySetWindowTheme(hHeader, "DarkMode_ItemsView");
			}
			else if((c is TreeView) || (c is TextBoxBase) || (c is ListBox) ||
				(c is Panel) || (c is UserControl))
				ThemeNative.TrySetWindowTheme(c.Handle, "DarkMode_Explorer");
			else if(c is ComboBox)
				ThemeNative.TrySetWindowTheme(c.Handle, "DarkMode_CFD");
		}

		private static void SetBack(Control c, Color clr)
		{
			Color cur = c.BackColor;
			if(cur.IsSystemColor || (cur == SystemColors.Window) || (cur == SystemColors.Control) ||
				(cur.ToArgb() == Color.White.ToArgb()))
				c.BackColor = clr;
		}

		private static void SetFore(Control c, Color clr)
		{
			Color cur = c.ForeColor;
			if(cur.IsSystemColor || (cur.ToArgb() == Color.Black.ToArgb()))
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

		// KeePass resets text box colors (e.g. after a validation error)
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
				try { c.BackColor = g_palette.Surface; }
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
