/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Flat, Windows 11 style painting for standard WinForms controls. No 3D
  borders, gradients or shadows: hairline borders, rounded buttons and
  check boxes, flat spin buttons and drop-downs.

  Painters draw over the control's own painting (Paint event) or its
  non-client border (window subclass), so KeePass' behavior, layout and
  accessibility stay untouched. When the theme is inactive they draw nothing.

  - Gfx                rounded rectangles, DPI scaling, keyboard cue state
  - BorderPainter      1 px border of text boxes, lists, trees, spin boxes
  - ButtonPainter      push buttons (primary button in the accent color)
  - CheckGlyphPainter  check box and radio button glyphs
  - GroupBoxPainter    rounded hairline frame
  - SpinPainter        up/down buttons of NumericUpDown
  - ComboPainter       drop-down lists
  - MeterPainter       KeePass' password quality bar
*/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;


namespace BetterPass
{
	internal static class Gfx
	{
		public static int Scale(Graphics g, int n)
		{
			return (int)Math.Round(n * g.DpiX / 96f);
		}

		public static float ScaleF(Graphics g, float f)
		{
			return f * g.DpiX / 96f;
		}

		public static GraphicsPath RoundRect(RectangleF rc, float r)
		{
			GraphicsPath gp = new GraphicsPath();
			float d = Math.Min(2f * r, Math.Min(rc.Width, rc.Height));
			if(d <= 0.5f)
			{
				gp.AddRectangle(rc);
				return gp;
			}
			gp.AddArc(rc.X, rc.Y, d, d, 180f, 90f);
			gp.AddArc(rc.Right - d, rc.Y, d, d, 270f, 90f);
			gp.AddArc(rc.Right - d, rc.Bottom - d, d, d, 0f, 90f);
			gp.AddArc(rc.X, rc.Bottom - d, d, d, 90f, 90f);
			gp.CloseFigure();
			return gp;
		}

		/// <summary>Fills and outlines a rounded rectangle on pixel centers.</summary>
		public static void FillRound(Graphics g, Rectangle rc, float r, Color clrFill, Color clrBorder)
		{
			SmoothingMode sm = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			RectangleF rcF = new RectangleF(rc.X + 0.5f, rc.Y + 0.5f, rc.Width - 1f, rc.Height - 1f);
			using(GraphicsPath gp = RoundRect(rcF, r))
			{
				if(clrFill.A > 0)
				{
					using(SolidBrush br = new SolidBrush(clrFill)) { g.FillPath(br, gp); }
				}
				if(clrBorder.A > 0)
				{
					using(Pen pen = new Pen(clrBorder)) { g.DrawPath(pen, gp); }
				}
			}
			g.SmoothingMode = sm;
		}

		public static Color ParentBack(Control c)
		{
			Control p = ((c != null) ? c.Parent : null);
			while((p != null) && (p.BackColor.A < 255)) p = p.Parent;
			return ((p != null) ? p.BackColor : ThemeManager.Palette.Back);
		}

		public static bool ShowFocusCues(Control c)
		{
			if((c == null) || !c.IsHandleCreated) return false;
			return ((ThemeNative.QueryUiState(c.Handle) & ThemeNative.UISF_HIDEFOCUS) == 0);
		}

		public static bool ShowKeyboardCues(Control c)
		{
			if((c == null) || !c.IsHandleCreated) return true;
			return ((ThemeNative.QueryUiState(c.Handle) & ThemeNative.UISF_HIDEACCEL) == 0);
		}

		/// <summary>Checks the type name, so KeePass' internal types need no reference.</summary>
		public static bool IsType(object o, string strTypeName)
		{
			for(Type t = ((o != null) ? o.GetType() : null); t != null; t = t.BaseType)
			{
				if(t.Name == strTypeName) return true;
			}
			return false;
		}

		public static void DrawChevron(Graphics g, Rectangle rc, bool bDown, Color clr)
		{
			float w = Math.Min(ScaleF(g, 8f), rc.Width - 2f);
			float h = w / 2f;
			float x = rc.X + (rc.Width - w) / 2f;
			float y = rc.Y + (rc.Height - h) / 2f;
			SmoothingMode sm = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using(Pen pen = new Pen(clr, Math.Max(1f, ScaleF(g, 1.25f))))
			{
				pen.StartCap = LineCap.Round;
				pen.EndCap = LineCap.Round;
				pen.LineJoin = LineJoin.Round;
				if(bDown) g.DrawLines(pen, new PointF[] { new PointF(x, y),
					new PointF(x + w / 2f, y + h), new PointF(x + w, y) });
				else g.DrawLines(pen, new PointF[] { new PointF(x, y + h),
					new PointF(x + w / 2f, y), new PointF(x + w, y + h) });
			}
			g.SmoothingMode = sm;
		}

		public static void DrawCheckMark(Graphics g, RectangleF rc, Color clr, float fWidth)
		{
			SmoothingMode sm = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			using(Pen pen = new Pen(clr, fWidth))
			{
				pen.StartCap = LineCap.Round;
				pen.EndCap = LineCap.Round;
				pen.LineJoin = LineJoin.Round;
				g.DrawLines(pen, new PointF[] {
					new PointF(rc.X + rc.Width * 0.22f, rc.Y + rc.Height * 0.53f),
					new PointF(rc.X + rc.Width * 0.42f, rc.Y + rc.Height * 0.72f),
					new PointF(rc.X + rc.Width * 0.78f, rc.Y + rc.Height * 0.31f) });
			}
			g.SmoothingMode = sm;
		}
	}

	/// <summary>Tracks hover and pressed state and repaints on changes.</summary>
	internal abstract class InteractivePainter
	{
		protected readonly Control m_c;
		protected bool m_bHover = false;
		protected bool m_bPressed = false;

		protected InteractivePainter(Control c)
		{
			m_c = c;
			c.Paint += this.OnPaint;
			c.MouseEnter += delegate(object sender, EventArgs e) { m_bHover = true; c.Invalidate(); };
			c.MouseLeave += delegate(object sender, EventArgs e)
			{
				m_bHover = false;
				m_bPressed = false;
				c.Invalidate();
			};
			c.MouseDown += delegate(object sender, MouseEventArgs e)
			{
				if(e.Button == MouseButtons.Left) { m_bPressed = true; c.Invalidate(); }
			};
			c.MouseUp += delegate(object sender, MouseEventArgs e) { m_bPressed = false; c.Invalidate(); };
			c.GotFocus += delegate(object sender, EventArgs e) { c.Invalidate(); };
			c.LostFocus += delegate(object sender, EventArgs e) { c.Invalidate(); };
			c.EnabledChanged += delegate(object sender, EventArgs e) { c.Invalidate(); };
		}

		private void OnPaint(object sender, PaintEventArgs e)
		{
			if(!ThemeManager.IsActive || m_c.IsDisposed) return;
			try { Paint(e.Graphics); }
			catch(Exception) { }
		}

		protected abstract void Paint(Graphics g);
	}

	// ===================== Borders =====================

	internal static class InputFrame
	{
		/// <summary>
		/// Frame of an input field or list: corners in the parent's color,
		/// rounded hairline border, and for inputs the focus indicator of the
		/// design (Windows 11: accent underline, aethercert: 2 px ring).
		/// The caller clips away the area the control paints itself.
		/// </summary>
		public static void Draw(Graphics g, Rectangle rc, bool bInput, bool bFocused, bool bHover,
			bool bEnabled, Color clrFill, Color clrParent)
		{
			if((rc.Width <= 4) || (rc.Height <= 4)) return;
			ThemePalette p = ThemeManager.Palette;
			float r = Gfx.ScaleF(g, 4f);

			using(SolidBrush br = new SolidBrush(clrParent)) { g.FillRectangle(br, rc); }

			Color clrStroke;
			if(!bEnabled) clrStroke = p.Border;
			else if(!bInput) clrStroke = p.Border;
			else if(bFocused && !p.FocusUnderline) clrStroke = p.Focus;
			else clrStroke = (bHover && !bFocused ? ThemePalette.Mix(p.InputStroke, p.Text, 0.18) : p.InputStroke);

			Gfx.FillRound(g, rc, r, clrFill, clrStroke);

			if(!bInput || !bEnabled) return;

			if(p.FocusUnderline)
			{
				// Underline inside the rounded shape: 1 px at rest, 2 px accent with focus
				int nLine = (bFocused ? Math.Max(2, Gfx.Scale(g, 2)) : 1);
				Color clrLine = (bFocused ? p.Accent : p.InputBottom);
				SmoothingMode sm = g.SmoothingMode;
				Region rgnOld = g.Clip;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				using(GraphicsPath gp = Gfx.RoundRect(new RectangleF(rc.X, rc.Y, rc.Width, rc.Height), r))
				{
					g.SetClip(gp, CombineMode.Intersect);
					using(SolidBrush br = new SolidBrush(clrLine))
					{
						g.FillRectangle(br, rc.X, rc.Bottom - nLine, rc.Width, nLine);
					}
				}
				g.Clip = rgnOld;
				g.SmoothingMode = sm;
			}
			else if(bFocused)
			{
				Rectangle rcIn = Rectangle.Inflate(rc, -1, -1);
				Gfx.FillRound(g, rcIn, Math.Max(1f, r - 1f), Color.Transparent, p.Focus);
			}
		}
	}

	internal sealed class BorderPainter : NativeWindow
	{
		private const int EM_SETMARGINS = 0x00D3;
		private const int EC_LEFTMARGIN = 0x1, EC_RIGHTMARGIN = 0x2;

		private readonly Control m_c;
		private bool m_bHover = false;

		// Spin boxes draw their border inside the client area themselves
		// (visual styles), so their frame is painted there, after them
		private readonly bool m_bClientFrame;

		private BorderPainter(Control c)
		{
			m_c = c;
			m_bClientFrame = (c is UpDownBase);
			c.HandleCreated += delegate(object sender, EventArgs e)
			{
				if(this.Handle == IntPtr.Zero) AssignHandle(c.Handle);
				SetTextMargins();
			};
			c.HandleDestroyed += delegate(object sender, EventArgs e) { ReleaseHandle(); };
			c.Enter += this.OnEnter;
			c.Leave += this.OnLeave;
			c.EnabledChanged += delegate(object sender, EventArgs e) { Redraw(); };
			c.MouseEnter += delegate(object sender, EventArgs e) { m_bHover = true; Redraw(); };
			c.MouseLeave += delegate(object sender, EventArgs e) { m_bHover = false; Redraw(); };
			if(c.IsHandleCreated)
			{
				AssignHandle(c.Handle);
				SetTextMargins();
			}
		}

		/// <summary>
		/// Rounded flat frame instead of the 3D border; borderless controls stay
		/// borderless. Single borders become 2 px frames, which leave room for
		/// the rounded corners and the focus indicator.
		/// </summary>
		public static void Attach(Control c)
		{
			TextBoxBase tb = (c as TextBoxBase);
			ListView lv = (c as ListView);
			TreeView tv = (c as TreeView);
			UpDownBase ud = (c as UpDownBase);
			ListBox lb = (c as ListBox);

			if(tb != null) { if(tb.BorderStyle == BorderStyle.None) return; tb.BorderStyle = BorderStyle.Fixed3D; }
			else if(lv != null) { if(lv.BorderStyle == BorderStyle.None) return; lv.BorderStyle = BorderStyle.Fixed3D; }
			else if(tv != null) { if(tv.BorderStyle == BorderStyle.None) return; tv.BorderStyle = BorderStyle.Fixed3D; }
			else if(ud != null) { if(ud.BorderStyle == BorderStyle.None) return; ud.BorderStyle = BorderStyle.Fixed3D; }
			else if(lb != null) { if(lb.BorderStyle == BorderStyle.None) return; lb.BorderStyle = BorderStyle.Fixed3D; }
			else if(!(c is DateTimePicker)) return;

			new BorderPainter(c);
		}

		/// <summary>True for editable fields (focus indicator, input colors).</summary>
		public static bool IsInputControl(Control c)
		{
			TextBoxBase tb = (c as TextBoxBase);
			if(tb != null) return !tb.ReadOnly;
			return ((c is UpDownBase) || (c is DateTimePicker) || (c is ComboBox));
		}

		private bool IsInput { get { return IsInputControl(m_c); } }

		private void Redraw()
		{
			if(!m_c.IsHandleCreated) return;
			if(m_bClientFrame) m_c.Invalidate();
			else ThemeNative.RedrawFrame(m_c.Handle);
		}

		// Windows 11 fields get a slightly different fill while focused
		private void OnEnter(object sender, EventArgs e)
		{
			ThemePalette p = ThemeManager.Palette;
			if(ThemeManager.IsActive && this.IsInput && !(m_c is DateTimePicker) &&
				(m_c.BackColor == p.InputFill) && (p.InputFillFocus != p.InputFill))
				m_c.BackColor = p.InputFillFocus;
			Redraw();
		}

		private void OnLeave(object sender, EventArgs e)
		{
			ThemePalette p = ThemeManager.Palette;
			if(ThemeManager.IsActive && (m_c.BackColor == p.InputFillFocus) &&
				(p.InputFillFocus != p.InputFill))
				m_c.BackColor = p.InputFill;
			Redraw();
		}

		/// <summary>Some room between frame and text, like modern text fields.</summary>
		private void SetTextMargins()
		{
			TextBox tb = (m_c as TextBox);
			if((tb == null) || !tb.IsHandleCreated || !ThemeManager.IsActive) return;
			try
			{
				int n;
				using(Graphics g = tb.CreateGraphics()) { n = Gfx.Scale(g, 4); }
				ThemeNative.SendMessage(tb.Handle, EM_SETMARGINS, (IntPtr)(EC_LEFTMARGIN | EC_RIGHTMARGIN),
					(IntPtr)((n << 16) | n));
			}
			catch(Exception) { }
		}

		protected override void WndProc(ref Message m)
		{
			base.WndProc(ref m);

			if(!ThemeManager.IsActive) return;
			if((m.Msg == ThemeNative.WM_NCPAINT) && !m_bClientFrame) PaintBorder();
			else if((m.Msg == ThemeNative.WM_PAINT) && m_bClientFrame) PaintClientFrame();
		}

		private void PaintClientFrame()
		{
			ThemePalette p = ThemeManager.Palette;
			int nFrame = SystemInformation.Border3DSize.Width;
			Color clrFill = (m_c.BackColor.IsSystemColor ? p.InputFill : m_c.BackColor);
			try
			{
				using(Graphics g = Graphics.FromHwnd(this.Handle))
				{
					Rectangle rc = m_c.ClientRectangle;
					g.SetClip(Rectangle.Inflate(rc, -nFrame, -nFrame), CombineMode.Exclude);
					InputFrame.Draw(g, rc, true, m_c.ContainsFocus, m_bHover, m_c.Enabled,
						clrFill, Gfx.ParentBack(m_c));
				}
			}
			catch(Exception) { }
		}

		private void PaintBorder()
		{
			ThemePalette p = ThemeManager.Palette;

			int nFrame = 2;
			ThemeNative.RECT rcWin;
			if(ThemeNative.GetWindowRect(this.Handle, out rcWin))
			{
				Point ptClient = m_c.PointToScreen(Point.Empty);
				nFrame = Math.Max(1, Math.Min(Math.Min(ptClient.X - rcWin.left, ptClient.Y - rcWin.top), 4));
			}

			Color clrFill = (((m_c is DateTimePicker) || m_c.BackColor.IsSystemColor) ?
				(this.IsInput ? p.InputFill : p.Surface) : m_c.BackColor);

			IntPtr hdc = ThemeNative.GetWindowDC(this.Handle);
			if(hdc == IntPtr.Zero) return;
			try
			{
				using(Graphics g = Graphics.FromHdc(hdc))
				{
					// Only the frame; client area and scroll bars paint themselves
					Rectangle rcAll = new Rectangle(0, 0, m_c.Width, m_c.Height);
					g.SetClip(new Rectangle(nFrame, nFrame, m_c.Width - 2 * nFrame,
						m_c.Height - 2 * nFrame), CombineMode.Exclude);
					InputFrame.Draw(g, rcAll, this.IsInput, m_c.ContainsFocus, m_bHover, m_c.Enabled,
						clrFill, Gfx.ParentBack(m_c));
				}
			}
			catch(Exception) { }
			finally { ThemeNative.ReleaseDC(this.Handle, hdc); }
		}
	}

	// ===================== Buttons =====================

	internal sealed class ButtonPainter : InteractivePainter
	{
		private readonly ButtonBase m_btn;

		private ButtonPainter(ButtonBase btn) : base(btn)
		{
			m_btn = btn;
		}

		public static void Attach(ButtonBase btn)
		{
			// Painted by Windows otherwise, so the Paint event would not fire
			if(btn.FlatStyle == FlatStyle.System) btn.FlatStyle = FlatStyle.Standard;
			new ButtonPainter(btn);
		}

		private bool IsPrimary
		{
			get
			{
				Form f = m_btn.FindForm();
				return ((f != null) && (f.AcceptButton == (m_btn as IButtonControl)) && (m_btn is Button));
			}
		}

		protected override void Paint(Graphics g)
		{
			ThemePalette p = ThemeManager.Palette;
			Rectangle rc = m_btn.ClientRectangle;
			if((rc.Width <= 2) || (rc.Height <= 2)) return;

			bool bEnabled = m_btn.Enabled;
			bool bPrimary = this.IsPrimary;
			CheckBox cb = (m_btn as CheckBox);
			bool bChecked = ((cb != null) && cb.Checked);

			Color clrBack, clrBorder, clrText;
			if(bPrimary)
			{
				clrBack = (!bEnabled ? ThemePalette.Mix(p.Accent, p.Back, 0.6) :
					(m_bPressed ? ThemePalette.Mix(p.Accent, p.Back, 0.22) :
					(m_bHover ? p.AccentHover : p.Accent)));
				clrBorder = clrBack;
				clrText = (bEnabled ? p.AccentText : ThemePalette.Mix(p.AccentText, clrBack, 0.4));
			}
			else
			{
				clrBack = (!bEnabled ? p.Control : ((m_bPressed || bChecked) ? p.Pressed :
					(m_bHover ? p.Hover : p.Control)));
				clrBorder = p.Border;
				clrText = (bEnabled ? p.Text : p.DisabledText);
			}

			using(SolidBrush br = new SolidBrush(Gfx.ParentBack(m_btn))) { g.FillRectangle(br, rc); }

			float r = Gfx.ScaleF(g, p.Radius);
			Gfx.FillRound(g, rc, r, clrBack, clrBorder);

			if(m_btn.Focused && Gfx.ShowFocusCues(m_btn))
			{
				Rectangle rcF = Rectangle.Inflate(rc, -1, -1);
				Gfx.FillRound(g, rcF, Math.Max(1f, r - 1f), Color.Transparent, p.Focus);
			}

			Rectangle rcContent = Rectangle.Inflate(rc, -Gfx.Scale(g, 6), -Gfx.Scale(g, 2));
			bool bSplit = Gfx.IsType(m_btn, "SplitButtonEx");
			if(bSplit)
			{
				int wArrow = Gfx.Scale(g, 16);
				Rectangle rcArrow = new Rectangle(rc.Right - wArrow - Gfx.Scale(g, 2), rc.Y, wArrow, rc.Height);
				Gfx.DrawChevron(g, rcArrow, true, clrText);
				rcContent.Width -= wArrow;
			}

			DrawContent(g, rcContent, clrText);
		}

		private Image GetImage()
		{
			if(m_btn.Image != null) return m_btn.Image;
			ImageList il = m_btn.ImageList;
			if(il == null) return null;
			if((m_btn.ImageIndex >= 0) && (m_btn.ImageIndex < il.Images.Count))
				return il.Images[m_btn.ImageIndex];
			if(!string.IsNullOrEmpty(m_btn.ImageKey) && il.Images.ContainsKey(m_btn.ImageKey))
				return il.Images[m_btn.ImageKey];
			return null;
		}

		private void DrawContent(Graphics g, Rectangle rc, Color clrText)
		{
			Image img = GetImage();
			string strText = (m_btn.Text ?? string.Empty);

			TextFormatFlags tff = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
				TextFormatFlags.EndEllipsis;
			if(!m_btn.UseMnemonic) tff |= TextFormatFlags.NoPrefix;
			else if(!Gfx.ShowKeyboardCues(m_btn)) tff |= TextFormatFlags.HidePrefix;

			Size szText = ((strText.Length > 0) ? TextRenderer.MeasureText(g, strText,
				m_btn.Font, rc.Size, tff | TextFormatFlags.NoPadding) : Size.Empty);
			int nGap = (((img != null) && (strText.Length > 0)) ? Gfx.Scale(g, 6) : 0);

			int xImg, xText;
			bool bImageLeft = ((img != null) && (strText.Length > 0) &&
				((m_btn.ImageAlign == ContentAlignment.MiddleLeft) ||
				(m_btn.ImageAlign == ContentAlignment.TopLeft) ||
				(m_btn.ImageAlign == ContentAlignment.BottomLeft)));
			if(bImageLeft)
			{
				xImg = rc.X;
				xText = xImg + img.Width + nGap;
				int wRest = rc.Right - xText;
				if(szText.Width < wRest) xText += (wRest - szText.Width) / 2;
			}
			else
			{
				int wAll = ((img != null) ? img.Width : 0) + nGap + szText.Width;
				xImg = rc.X + Math.Max(0, (rc.Width - wAll) / 2);
				xText = xImg + ((img != null) ? img.Width : 0) + nGap;
			}

			if(img != null)
			{
				int yImg = rc.Y + (rc.Height - img.Height) / 2;
				if(m_btn.Enabled) g.DrawImage(img, xImg, yImg, img.Width, img.Height);
				else
				{
					using(Image imgD = ToolStripRenderer.CreateDisabledImage(img))
					{
						g.DrawImage(imgD, xImg, yImg, img.Width, img.Height);
					}
				}
			}

			if(strText.Length > 0)
			{
				Rectangle rcText = new Rectangle(xText, rc.Y, Math.Max(0, rc.Right - xText), rc.Height);
				TextRenderer.DrawText(g, strText, m_btn.Font, rcText, clrText, tff |
					TextFormatFlags.NoPadding);
			}
		}
	}

	// ===================== Check boxes and radio buttons =====================

	internal sealed class CheckGlyphPainter : InteractivePainter
	{
		private readonly ButtonBase m_btn;

		private CheckGlyphPainter(ButtonBase btn) : base(btn)
		{
			m_btn = btn;
		}

		public static void Attach(ButtonBase btn)
		{
			if(btn.FlatStyle == FlatStyle.System) btn.FlatStyle = FlatStyle.Standard;
			new CheckGlyphPainter(btn);
		}

		protected override void Paint(Graphics g)
		{
			CheckBox cb = (m_btn as CheckBox);
			RadioButton rb = (m_btn as RadioButton);
			ContentAlignment ca = ((cb != null) ? cb.CheckAlign : ((rb != null) ?
				rb.CheckAlign : ContentAlignment.MiddleLeft));
			if((ca != ContentAlignment.MiddleLeft) && (ca != ContentAlignment.TopLeft)) return;

			// Same box as Windows' glyph, so the text layout stays as it is
			Size szGlyph = ((cb != null) ? CheckBoxRenderer.GetGlyphSize(g, System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal) :
				RadioButtonRenderer.GetGlyphSize(g, System.Windows.Forms.VisualStyles.RadioButtonState.UncheckedNormal));
			int x = m_btn.Padding.Left;
			int y = ((ca == ContentAlignment.MiddleLeft) ? (m_btn.Height - szGlyph.Height) / 2 :
				m_btn.Padding.Top + Math.Max(0, (m_btn.Font.Height - szGlyph.Height) / 2));
			Rectangle rcGlyph = new Rectangle(x, y, szGlyph.Width, szGlyph.Height);

			using(SolidBrush br = new SolidBrush(Gfx.ParentBack(m_btn)))
			{
				g.FillRectangle(br, Rectangle.Inflate(rcGlyph, 1, 1));
			}

			ThemePalette p = ThemeManager.Palette;
			bool bEnabled = m_btn.Enabled;
			CheckState cs = ((cb != null) ? cb.CheckState : ((rb != null) && rb.Checked ?
				CheckState.Checked : CheckState.Unchecked));
			bool bOn = (cs != CheckState.Unchecked);

			Color clrFill, clrBorder, clrMark;
			if(bOn)
			{
				clrFill = (!bEnabled ? ThemePalette.Mix(p.Accent, p.Back, 0.6) :
					(m_bPressed ? ThemePalette.Mix(p.Accent, p.Back, 0.22) :
					(m_bHover ? p.AccentHover : p.Accent)));
				clrBorder = clrFill;
				clrMark = p.AccentText;
			}
			else
			{
				clrFill = (m_bPressed ? p.Pressed : (m_bHover ? p.Hover : p.Surface));
				clrBorder = (bEnabled ? p.InputBorder : p.Border);
				clrMark = Color.Transparent;
			}

			if(rb != null)
			{
				SmoothingMode sm = g.SmoothingMode;
				g.SmoothingMode = SmoothingMode.AntiAlias;
				RectangleF rcF = new RectangleF(rcGlyph.X + 0.5f, rcGlyph.Y + 0.5f,
					rcGlyph.Width - 1f, rcGlyph.Height - 1f);
				using(SolidBrush br = new SolidBrush(clrFill)) { g.FillEllipse(br, rcF); }
				using(Pen pen = new Pen(clrBorder)) { g.DrawEllipse(pen, rcF); }
				if(bOn)
				{
					float d = rcF.Width * 0.42f;
					using(SolidBrush br = new SolidBrush(clrMark))
					{
						g.FillEllipse(br, rcF.X + (rcF.Width - d) / 2f, rcF.Y + (rcF.Height - d) / 2f, d, d);
					}
				}
				g.SmoothingMode = sm;
				return;
			}

			Gfx.FillRound(g, rcGlyph, Gfx.ScaleF(g, 3f), clrFill, clrBorder);
			if(cs == CheckState.Checked)
				Gfx.DrawCheckMark(g, rcGlyph, clrMark, Math.Max(1.2f, Gfx.ScaleF(g, 1.5f)));
			else if(cs == CheckState.Indeterminate)
			{
				float w = rcGlyph.Width * 0.5f;
				using(Pen pen = new Pen(clrMark, Math.Max(1.2f, Gfx.ScaleF(g, 1.5f))))
				{
					pen.StartCap = LineCap.Round;
					pen.EndCap = LineCap.Round;
					float yM = rcGlyph.Y + rcGlyph.Height / 2f;
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.DrawLine(pen, rcGlyph.X + (rcGlyph.Width - w) / 2f, yM,
						rcGlyph.X + (rcGlyph.Width + w) / 2f, yM);
				}
			}
		}
	}

	// ===================== Group boxes =====================

	internal static class GroupBoxPainter
	{
		public static void Attach(GroupBox gb)
		{
			gb.Paint += OnPaint;
		}

		private static void OnPaint(object sender, PaintEventArgs e)
		{
			GroupBox gb = (sender as GroupBox);
			if((gb == null) || !ThemeManager.IsActive) return;

			try
			{
				ThemePalette p = ThemeManager.Palette;
				Graphics g = e.Graphics;
				Rectangle rc = gb.ClientRectangle;
				using(SolidBrush br = new SolidBrush(gb.BackColor)) { g.FillRectangle(br, rc); }

				string strText = (gb.Text ?? string.Empty);
				Size szText = TextRenderer.MeasureText(g, strText, gb.Font, Size.Empty,
					TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
				int yFrame = gb.Font.Height / 2;

				Rectangle rcFrame = new Rectangle(0, yFrame, rc.Width, rc.Height - yFrame);
				Gfx.FillRound(g, rcFrame, Gfx.ScaleF(g, p.Radius + 2), Color.Transparent, p.Border);

				if(strText.Length > 0)
				{
					int xText = Gfx.Scale(g, 8);
					Rectangle rcText = new Rectangle(xText, 0, szText.Width + Gfx.Scale(g, 6), gb.Font.Height);
					using(SolidBrush br = new SolidBrush(gb.BackColor)) { g.FillRectangle(br, rcText); }
					TextRenderer.DrawText(g, strText, gb.Font, new Point(xText + Gfx.Scale(g, 3), 0),
						(gb.Enabled ? p.Text : p.DisabledText), TextFormatFlags.SingleLine |
						TextFormatFlags.NoPadding);
				}
			}
			catch(Exception) { }
		}
	}

	/// <summary>
	/// Replaces a control's painting completely (WM_PAINT, double buffered),
	/// for controls whose OnPaint does not raise the Paint event.
	/// </summary>
	internal abstract class PaintReplacer : NativeWindow
	{
		protected readonly Control m_c;

		protected PaintReplacer(Control c)
		{
			m_c = c;
			c.HandleCreated += delegate(object sender, EventArgs e) { if(this.Handle == IntPtr.Zero) AssignHandle(c.Handle); };
			c.HandleDestroyed += delegate(object sender, EventArgs e) { ReleaseHandle(); };
			if(c.IsHandleCreated) AssignHandle(c.Handle);
		}

		protected override void WndProc(ref Message m)
		{
			if(ThemeManager.IsActive && !m_c.IsDisposed)
			{
				if(m.Msg == ThemeNative.WM_ERASEBKGND) { m.Result = (IntPtr)1; return; }
				if(m.Msg == ThemeNative.WM_PAINT)
				{
					ThemeNative.PAINTSTRUCT ps;
					IntPtr hdc = ThemeNative.BeginPaint(m.HWnd, out ps);
					try
					{
						Rectangle rc = m_c.ClientRectangle;
						if((rc.Width > 0) && (rc.Height > 0))
						{
							using(Graphics gTarget = Graphics.FromHdc(hdc))
							{
								using(BufferedGraphics bg = BufferedGraphicsManager.Current.Allocate(gTarget, rc))
								{
									Paint(bg.Graphics, rc);
									bg.Render(gTarget);
								}
							}
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

		protected abstract void Paint(Graphics g, Rectangle rc);
	}

	// ===================== Spin buttons =====================

	internal sealed class SpinPainter : PaintReplacer
	{
		private readonly UpDownBase m_ud;
		private int m_iHot = -1; // 0 = up, 1 = down
		private bool m_bPressed = false;

		private SpinPainter(Control cButtons, UpDownBase ud) : base(cButtons)
		{
			m_ud = ud;
			cButtons.MouseMove += delegate(object sender, MouseEventArgs e)
			{
				int i = ((e.Y < cButtons.Height / 2) ? 0 : 1);
				if(i != m_iHot) { m_iHot = i; cButtons.Invalidate(); }
			};
			cButtons.MouseLeave += delegate(object sender, EventArgs e)
			{
				m_iHot = -1;
				m_bPressed = false;
				cButtons.Invalidate();
			};
			cButtons.MouseDown += delegate(object sender, MouseEventArgs e) { m_bPressed = true; cButtons.Invalidate(); };
			cButtons.MouseUp += delegate(object sender, MouseEventArgs e) { m_bPressed = false; cButtons.Invalidate(); };
			ud.EnabledChanged += delegate(object sender, EventArgs e) { cButtons.Invalidate(); };
		}

		public static void Attach(UpDownBase ud)
		{
			foreach(Control c in ud.Controls)
			{
				if(Gfx.IsType(c, "UpDownButtons")) { new SpinPainter(c, ud); return; }
			}
		}

		protected override void Paint(Graphics g, Rectangle rc)
		{
			ThemePalette p = ThemeManager.Palette;
			int hHalf = rc.Height / 2;
			Rectangle rcUp = new Rectangle(rc.X, rc.Y, rc.Width, hHalf);
			Rectangle rcDown = new Rectangle(rc.X, rc.Y + hHalf, rc.Width, rc.Height - hHalf);

			Color clrFill = (m_ud.BackColor.IsSystemColor ? p.InputFill : m_ud.BackColor);
			using(SolidBrush br = new SolidBrush(clrFill)) { g.FillRectangle(br, rc); }

			bool bEnabled = m_ud.Enabled;
			for(int i = 0; i < 2; ++i)
			{
				Rectangle rcPart = ((i == 0) ? rcUp : rcDown);
				if(bEnabled && (m_iHot == i))
				{
					Rectangle rcHi = Rectangle.Inflate(rcPart, -1, -1);
					Gfx.FillRound(g, rcHi, Gfx.ScaleF(g, 2f), (m_bPressed ? p.Pressed : p.Hover),
						Color.Transparent);
				}
				Gfx.DrawChevron(g, rcPart, (i == 1), (bEnabled ? p.DimText : p.DisabledText));
			}
		}
	}

	// ===================== Drop-down lists =====================

	internal sealed class ComboPainter : NativeWindow
	{
		private readonly ComboBox m_cmb;

		private ComboPainter(ComboBox cmb)
		{
			m_cmb = cmb;
			cmb.HandleCreated += delegate(object sender, EventArgs e) { if(this.Handle == IntPtr.Zero) AssignHandle(cmb.Handle); };
			cmb.HandleDestroyed += delegate(object sender, EventArgs e) { ReleaseHandle(); };
			cmb.Enter += delegate(object sender, EventArgs e) { cmb.Invalidate(); };
			cmb.Leave += delegate(object sender, EventArgs e) { cmb.Invalidate(); };
			cmb.EnabledChanged += delegate(object sender, EventArgs e) { cmb.Invalidate(); };
			cmb.SelectedIndexChanged += delegate(object sender, EventArgs e) { cmb.Invalidate(); };
			cmb.MouseEnter += delegate(object sender, EventArgs e) { m_bHover = true; cmb.Invalidate(); };
			cmb.MouseLeave += delegate(object sender, EventArgs e) { m_bHover = false; cmb.Invalidate(); };
			if(cmb.IsHandleCreated) AssignHandle(cmb.Handle);
		}

		/// <summary>Only for combo boxes that Windows draws (no owner draw).</summary>
		public static void Attach(ComboBox cmb)
		{
			if(cmb.DrawMode != DrawMode.Normal) return;
			cmb.FlatStyle = FlatStyle.Flat;
			new ComboPainter(cmb);
		}

		protected override void WndProc(ref Message m)
		{
			if(ThemeManager.IsActive)
			{
				// Disabled edit field of editable combo boxes (e.g. the search box)
				if((m.Msg == ThemeNative.WM_CTLCOLORSTATIC) && !m_cmb.Enabled)
				{
					ThemePalette p = ThemeManager.Palette;
					ThemeNative.SetTextColor(m.WParam, ThemeNative.ToColorRef(p.DisabledText));
					ThemeNative.SetBkColor(m.WParam, ThemeNative.ToColorRef(this.FillColor));
					m.Result = ThemeNative.GetBrush(this.FillColor);
					return;
				}

				if((m.Msg == ThemeNative.WM_PAINT) && (m_cmb.DropDownStyle == ComboBoxStyle.DropDownList))
				{
					ThemeNative.PAINTSTRUCT ps;
					IntPtr hdc = ThemeNative.BeginPaint(m.HWnd, out ps);
					try
					{
						using(Graphics g = Graphics.FromHdc(hdc)) { PaintFull(g); }
					}
					catch(Exception) { }
					finally { ThemeNative.EndPaint(m.HWnd, ref ps); }
					m.Result = IntPtr.Zero;
					return;
				}
			}

			base.WndProc(ref m);

			if((m.Msg == ThemeNative.WM_PAINT) && ThemeManager.IsActive)
			{
				try
				{
					using(Graphics g = Graphics.FromHwnd(m.HWnd)) { PaintFrame(g); }
				}
				catch(Exception) { }
			}
		}

		private bool m_bHover = false;

		private Rectangle ArrowRect(Graphics g)
		{
			int w = Gfx.Scale(g, 22);
			Rectangle rc = m_cmb.ClientRectangle;
			return new Rectangle(rc.Right - w - 2, rc.Y + 2, w, Math.Max(0, rc.Height - 4));
		}

		private Color FillColor
		{
			get
			{
				Color c = m_cmb.BackColor;
				return (c.IsSystemColor ? ThemeManager.Palette.InputFill : c);
			}
		}

		private void DrawFrame(Graphics g)
		{
			InputFrame.Draw(g, m_cmb.ClientRectangle, true, m_cmb.ContainsFocus, m_bHover,
				m_cmb.Enabled, this.FillColor, Gfx.ParentBack(m_cmb));
		}

		/// <summary>Editable combo box: the edit field draws itself, we draw frame and arrow.</summary>
		private void PaintFrame(Graphics g)
		{
			ThemePalette p = ThemeManager.Palette;
			Rectangle rc = m_cmb.ClientRectangle;
			Rectangle rcArrow = ArrowRect(g);

			// Frame ring and arrow area only, never over the edit field
			int nRing = Math.Max(2, Gfx.Scale(g, 2));
			using(Region rgn = new Region(rc))
			{
				rgn.Exclude(Rectangle.Inflate(rc, -nRing, -nRing));
				rgn.Union(rcArrow);
				g.Clip = rgn;
				DrawFrame(g);
				Gfx.DrawChevron(g, rcArrow, true, (m_cmb.Enabled ? p.DimText : p.DisabledText));
				g.ResetClip();
			}
		}

		/// <summary>Drop-down list: everything, without the blue selection highlight.</summary>
		private void PaintFull(Graphics g)
		{
			ThemePalette p = ThemeManager.Palette;
			Rectangle rc = m_cmb.ClientRectangle;
			DrawFrame(g);

			Rectangle rcArrow = ArrowRect(g);
			Gfx.DrawChevron(g, rcArrow, true, (m_cmb.Enabled ? p.DimText : p.DisabledText));

			int xText = rc.X + Gfx.Scale(g, 8);
			Rectangle rcText = new Rectangle(xText, rc.Y, Math.Max(0, rcArrow.Left - xText), rc.Height);
			TextRenderer.DrawText(g, m_cmb.Text, m_cmb.Font, rcText,
				(m_cmb.Enabled ? p.Text : p.DisabledText), TextFormatFlags.VerticalCenter |
				TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
				TextFormatFlags.NoPadding);
		}
	}

	// ===================== Password quality =====================

	/// <summary>
	/// KeePass' quality bar (rainbow gradient) as a flat meter: a tinted fill
	/// in the status color, so the text stays readable in every theme.
	/// </summary>
	internal sealed class MeterPainter : PaintReplacer
	{
		private MeterPainter(Control c) : base(c) { }

		public static void Attach(Control c)
		{
			new MeterPainter(c);
		}

		private static int GetInt(object o, string strProp)
		{
			System.Reflection.PropertyInfo pi = o.GetType().GetProperty(strProp);
			if((pi == null) || (pi.PropertyType != typeof(int))) return 0;
			return (int)pi.GetValue(o, null);
		}

		protected override void Paint(Graphics g, Rectangle rc)
		{
			Control c = m_c;
			try
			{
				ThemePalette p = ThemeManager.Palette;
				if((rc.Width <= 2) || (rc.Height <= 2)) return;

				int nMin = GetInt(c, "Minimum"), nMax = GetInt(c, "Maximum"), nValue = GetInt(c, "Value");
				float f = ((nMax > nMin) ? Math.Max(0f, Math.Min(1f, (float)(nValue - nMin) / (nMax - nMin))) : 0f);

				using(SolidBrush br = new SolidBrush(Gfx.ParentBack(c))) { g.FillRectangle(br, rc); }

				float r = Gfx.ScaleF(g, p.Radius);
				Gfx.FillRound(g, rc, r, p.Surface, Color.Transparent);

				if(f > 0f)
				{
					Color clrState = ((f < 0.33f) ? p.Error : ((f < 0.66f) ? p.Warning : p.Success));
					Color clrFill = ThemePalette.Mix(p.Surface, clrState, (p.IsDark ? 0.42 : 0.32));
					int w = Math.Max((int)Math.Ceiling(2 * r), (int)Math.Round(rc.Width * f));
					Region rgnOld = g.Clip;
					g.SetClip(new Rectangle(rc.X, rc.Y, w, rc.Height), CombineMode.Intersect);
					Gfx.FillRound(g, rc, r, clrFill, Color.Transparent);
					g.Clip = rgnOld;
				}

				Gfx.FillRound(g, rc, r, Color.Transparent, p.Border);

				System.Reflection.PropertyInfo pi = c.GetType().GetProperty("ProgressText");
				string strText = ((pi != null) ? (pi.GetValue(c, null) as string) : null);
				if(!string.IsNullOrEmpty(strText))
					TextRenderer.DrawText(g, strText, c.Font, rc, (c.Enabled ? p.Text : p.DisabledText),
						TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
						TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
			}
			catch(Exception) { }
		}
	}
}
