/*
  BetterPass - KeePass plugin
  Window subclasses for controls that WinForms cannot color by itself:
  - TabControlPainter: paints tab headers and the strip background
  - ListViewHeaderFixer: light text in dark list view column headers
*/

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BetterPass
{
	internal sealed class TabControlPainter : NativeWindow
	{
		private readonly TabControl m_tc;

		private TabControlPainter(TabControl tc)
		{
			m_tc = tc;
			tc.HandleCreated += this.OnHandleCreated;
			tc.HandleDestroyed += this.OnHandleDestroyed;
			if(tc.IsHandleCreated) AssignHandle(tc.Handle);
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
			if(ThemeManager.IsDarkActive)
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
				using(SolidBrush brBack = new SolidBrush(p.Back))
				{
					g.FillRectangle(brBack, rcClient);
				}

				// Frame around the page area
				Rectangle rcPage = m_tc.DisplayRectangle;
				Rectangle rcFrame = Rectangle.Inflate(rcPage, 2, 2);
				using(SolidBrush brPage = new SolidBrush(p.Back))
				{
					g.FillRectangle(brPage, rcFrame);
				}
				using(Pen pen = new Pen(p.Border))
				{
					g.DrawRectangle(pen, rcFrame.X, rcFrame.Y, rcFrame.Width - 1, rcFrame.Height - 1);
				}

				int iSel = m_tc.SelectedIndex;
				for(int i = 0; i < m_tc.TabCount; ++i)
				{
					Rectangle rc = m_tc.GetTabRect(i);
					bool bSel = (i == iSel);
					if(bSel) rc = new Rectangle(rc.X, rc.Y - 2, rc.Width, rc.Height + 3);

					using(SolidBrush br = new SolidBrush(bSel ? p.Back : p.Surface))
					{
						g.FillRectangle(br, rc);
					}
					using(Pen pen = new Pen(p.Border))
					{
						g.DrawLine(pen, rc.Left, rc.Bottom - 1, rc.Left, rc.Top);
						g.DrawLine(pen, rc.Left, rc.Top, rc.Right - 1, rc.Top);
						g.DrawLine(pen, rc.Right - 1, rc.Top, rc.Right - 1, rc.Bottom - 1);
						if(!bSel) g.DrawLine(pen, rc.Left, rc.Bottom - 1, rc.Right - 1, rc.Bottom - 1);
					}
					if(bSel)
					{
						using(Pen penAcc = new Pen(p.Accent, 2f))
						{
							g.DrawLine(penAcc, rc.Left + 1, rc.Top + 1, rc.Right - 2, rc.Top + 1);
						}
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

					TextRenderer.DrawText(g, tp.Text, m_tc.Font, rcText,
						(bSel ? p.Text : p.DimText), TextFormatFlags.HorizontalCenter |
						TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
						TextFormatFlags.EndEllipsis);
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

		protected override void WndProc(ref Message m)
		{
			if((m.Msg == ThemeNative.WM_NOTIFY) && ThemeManager.IsDarkActive &&
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
