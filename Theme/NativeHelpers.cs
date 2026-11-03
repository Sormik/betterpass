/*
  BetterPass - KeePass plugin
  Window subclasses for controls that WinForms cannot color by itself:
  - TabControlPainter: flat tab strip (text tabs, accent marker under the
    selected tab, hairline below the strip, no frame around the page)
  - ListViewHeaderFixer: light text in dark list view column headers;
    dark background of empty, disabled list views (Windows paints them
    with the system button face color, ignoring the background color)
*/

using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using KeePass.Resources;

namespace BetterPass
{
	internal sealed class TabControlPainter : NativeWindow
	{
		private readonly TabControl m_tc;
		private int m_iHot = -1;

		private TabControlPainter(TabControl tc)
		{
			m_tc = tc;
			tc.HandleCreated += this.OnHandleCreated;
			tc.HandleDestroyed += this.OnHandleDestroyed;
			tc.MouseMove += this.OnMouseMove;
			tc.MouseLeave += delegate(object sender, EventArgs e) { SetHot(-1); };
			if(tc.IsHandleCreated) AssignHandle(tc.Handle);
		}

		private void OnMouseMove(object sender, MouseEventArgs e)
		{
			int iHot = -1;
			for(int i = 0; i < m_tc.TabCount; ++i)
			{
				if(m_tc.GetTabRect(i).Contains(e.Location)) { iHot = i; break; }
			}
			SetHot(iHot);
		}

		private void SetHot(int i)
		{
			if(i == m_iHot) return;
			m_iHot = i;
			m_tc.Invalidate();
		}

		public static void Attach(TabControl tc)
		{
			if(tc == null) return;
			if(tc.Alignment != TabAlignment.Top) return; // Only the common layout
			new TabControlPainter(tc);
		}

		private void OnHandleCreated(object sender, EventArgs e)
		{
			if(this.Handle == IntPtr.Zero) AssignHandle(m_tc.Handle);
		}

		private void OnHandleDestroyed(object sender, EventArgs e)
		{
			ReleaseHandle();
		}

		protected override void WndProc(ref Message m)
		{
			if(ThemeManager.IsActive)
			{
				if(m.Msg == ThemeNative.WM_ERASEBKGND)
				{
					m.Result = (IntPtr)1;
					return;
				}
				if(m.Msg == ThemeNative.WM_PAINT)
				{
					ThemeNative.PAINTSTRUCT ps;
					IntPtr hdc = ThemeNative.BeginPaint(m.HWnd, out ps);
					try
					{
						using(Graphics g = Graphics.FromHdc(hdc))
						{
							PaintAll(g);
						}
					}
					catch(Exception) { }
					finally { ThemeNative.EndPaint(m.HWnd, ref ps); }
					m.Result = IntPtr.Zero;
					return;
				}
			}

			base.WndProc(ref m);
		}

		private void PaintAll(Graphics gTarget)
		{
			Rectangle rcClient = m_tc.ClientRectangle;
			if((rcClient.Width <= 0) || (rcClient.Height <= 0)) return;

			ThemePalette p = ThemeManager.Palette;
			using(BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(gTarget, rcClient))
			{
				Graphics g = bg.Graphics;
				Color clrBack = Gfx.ParentBack(m_tc);
				using(SolidBrush brBack = new SolidBrush(clrBack))
				{
					g.FillRectangle(brBack, rcClient);
				}

				// Hairline between the strip and the page; no frame around the page
				int yStrip = 0;
				for(int i = 0; i < m_tc.TabCount; ++i)
					yStrip = Math.Max(yStrip, m_tc.GetTabRect(i).Bottom);
				if(m_tc.TabCount > 0)
				{
					using(Pen pen = new Pen(p.Border))
					{
						g.DrawLine(pen, rcClient.Left, yStrip, rcClient.Right - 1, yStrip);
					}
				}

				int iSel = m_tc.SelectedIndex;
				int nMarkerH = Math.Max(2, Gfx.Scale(g, 3));
				for(int i = 0; i < m_tc.TabCount; ++i)
				{
					Rectangle rc = m_tc.GetTabRect(i);
					bool bSel = (i == iSel);
					bool bHot = (i == m_iHot);

					if(bHot && !bSel)
					{
						Rectangle rcHot = new Rectangle(rc.X + 1, rc.Y + Gfx.Scale(g, 2),
							rc.Width - 2, rc.Height - Gfx.Scale(g, 4));
						Gfx.FillRound(g, rcHot, Gfx.ScaleF(g, p.Radius), p.Hover, Color.Transparent);
					}

					TabPage tp = m_tc.TabPages[i];
					Rectangle rcText = rc;
					if((m_tc.ImageList != null) && (tp.ImageIndex >= 0) &&
						(tp.ImageIndex < m_tc.ImageList.Images.Count))
					{
						Image img = m_tc.ImageList.Images[tp.ImageIndex];
						int yImg = rc.Top + (rc.Height - img.Height) / 2;
						g.DrawImage(img, rc.Left + 6, yImg, img.Width, img.Height);
						rcText = new Rectangle(rc.Left + 6 + img.Width, rc.Top,
							rc.Width - 6 - img.Width, rc.Height);
					}

					Color clrText = (!m_tc.Enabled ? p.DisabledText :
						((bSel || bHot) ? p.Text : p.DimText));
					TextRenderer.DrawText(g, tp.Text, m_tc.Font, rcText, clrText,
						TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
						TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

					if(bSel)
					{
						// Marker as wide as the text, centered under it
						int wText = TextRenderer.MeasureText(g, tp.Text, m_tc.Font).Width;
						int wMarker = Math.Max(Gfx.Scale(g, 16), Math.Min(rcText.Width - 4, wText - Gfx.Scale(g, 8)));
						Rectangle rcMarker = new Rectangle(rcText.X + (rcText.Width - wMarker) / 2,
							yStrip - nMarkerH, wMarker, nMarkerH);
						Gfx.FillRound(g, rcMarker, nMarkerH / 2f, p.Highlight, Color.Transparent);
					}
				}

				bg.Render(gTarget);
			}
		}
	}

	internal sealed class ListViewHeaderFixer : NativeWindow
	{
		private readonly ListView m_lv;
		private IntPtr m_hHeader = IntPtr.Zero;

		private ListViewHeaderFixer(ListView lv)
		{
			m_lv = lv;
			lv.HandleCreated += this.OnHandleCreated;
			lv.HandleDestroyed += this.OnHandleDestroyed;
			if(lv.IsHandleCreated) Hook();
		}

		public static void Attach(ListView lv)
		{
			if(lv != null) new ListViewHeaderFixer(lv);
		}

		private void Hook()
		{
			if(this.Handle != IntPtr.Zero) return;
			AssignHandle(m_lv.Handle);
			m_hHeader = ThemeNative.SendMessage(m_lv.Handle, ThemeNative.LVM_GETHEADER,
				IntPtr.Zero, IntPtr.Zero);
		}

		private void OnHandleCreated(object sender, EventArgs e) { Hook(); }

		private void OnHandleDestroyed(object sender, EventArgs e)
		{
			ReleaseHandle();
			m_hHeader = IntPtr.Zero;
		}

		public IntPtr HeaderHandle
		{
			get
			{
				if((m_hHeader == IntPtr.Zero) && m_lv.IsHandleCreated)
					m_hHeader = ThemeNative.SendMessage(m_lv.Handle, ThemeNative.LVM_GETHEADER,
						IntPtr.Zero, IntPtr.Zero);
				return m_hHeader;
			}
		}

		private bool IsDisabledAndEmpty
		{
			get
			{
				if(m_lv.Enabled || m_lv.IsDisposed) return false;
				try { return ((m_lv.VirtualMode ? m_lv.VirtualListSize : m_lv.Items.Count) == 0); }
				catch(Exception) { return false; }
			}
		}

		// KeePass sets the "Empty." markup via an internal property
		private string GetEmptyText()
		{
			try
			{
				PropertyInfo pi = m_lv.GetType().GetProperty("EmptyMarkupEx",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if((pi != null) && (pi.PropertyType == typeof(string)))
				{
					string str = (pi.GetValue(m_lv, null) as string);
					if(str != null) return str;
				}
			}
			catch(Exception) { }
			return KPRes.Empty + ".";
		}

		private void PaintDisabledEmpty(IntPtr hWnd)
		{
			ThemeNative.PAINTSTRUCT ps;
			IntPtr hdc = ThemeNative.BeginPaint(hWnd, out ps);
			try
			{
				Rectangle rc = m_lv.ClientRectangle;
				IntPtr hHeader = this.HeaderHandle;
				ThemeNative.RECT rcH;
				if((hHeader != IntPtr.Zero) && ThemeNative.IsWindowVisible(hHeader) &&
					ThemeNative.GetWindowRect(hHeader, out rcH))
				{
					int dy = Math.Max(0, m_lv.PointToClient(new Point(rcH.left, rcH.bottom)).Y);
					rc = new Rectangle(rc.X, rc.Y + dy, rc.Width, Math.Max(0, rc.Height - dy));
				}
				if((rc.Width <= 0) || (rc.Height <= 0)) return;

				ThemePalette p = ThemeManager.Palette;
				using(Graphics g = Graphics.FromHdc(hdc))
				{
					using(SolidBrush br = new SolidBrush(m_lv.BackColor.IsSystemColor ?
						p.Surface : m_lv.BackColor))
					{
						g.FillRectangle(br, rc);
					}

					string strEmpty = GetEmptyText();
					if(strEmpty.Length > 0)
					{
						using(Font f = new Font(m_lv.Font, FontStyle.Italic))
						{
							TextRenderer.DrawText(g, strEmpty, f, rc, p.DimText,
								TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
								TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
						}
					}
				}
			}
			catch(Exception) { }
			finally { ThemeNative.EndPaint(hWnd, ref ps); }
		}

		protected override void WndProc(ref Message m)
		{
			if(ThemeManager.IsActive && ((m.Msg == ThemeNative.WM_ERASEBKGND) ||
				(m.Msg == ThemeNative.WM_PAINT)) && this.IsDisabledAndEmpty)
			{
				if(m.Msg == ThemeNative.WM_PAINT) PaintDisabledEmpty(m.HWnd);
				m.Result = ((m.Msg == ThemeNative.WM_ERASEBKGND) ? (IntPtr)1 : IntPtr.Zero);
				return;
			}

			if((m.Msg == ThemeNative.WM_NOTIFY) && ThemeManager.IsActive &&
				(m.LParam != IntPtr.Zero))
			{
				ThemeNative.NMHDR hdr = (ThemeNative.NMHDR)Marshal.PtrToStructure(
					m.LParam, typeof(ThemeNative.NMHDR));
				if((hdr.code == ThemeNative.NM_CUSTOMDRAW) && (hdr.hwndFrom != IntPtr.Zero) &&
					(hdr.hwndFrom == this.HeaderHandle))
				{
					ThemeNative.NMCUSTOMDRAW cd = (ThemeNative.NMCUSTOMDRAW)Marshal.PtrToStructure(
						m.LParam, typeof(ThemeNative.NMCUSTOMDRAW));
					if(cd.dwDrawStage == ThemeNative.CDDS_PREPAINT)
					{
						m.Result = (IntPtr)ThemeNative.CDRF_NOTIFYITEMDRAW;
						return;
					}
					if(cd.dwDrawStage == ThemeNative.CDDS_ITEMPREPAINT)
					{
						ThemeNative.SetTextColor(cd.hdc, ThemeNative.ToColorRef(
							ThemeManager.Palette.Text));
						m.Result = (IntPtr)ThemeNative.CDRF_DODEFAULT;
						return;
					}
				}
			}

			base.WndProc(ref m);
		}
	}
}
