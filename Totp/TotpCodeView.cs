/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  The current TOTP code as a flat card: large code, thin countdown ring
  with the remaining seconds, copy button. No shadows, no 3D borders; a
  hairline border that turns into the focus color when focused.

  Keyboard: Space or Ctrl+C copy the code (Enter stays the dialog's OK).
  Double-clicking the code or clicking the copy button copies it as well.
*/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BetterPass
{
	internal sealed class TotpCodeView : Control
	{
		private const string GlyphCopy = "";  // Segoe Fluent Icons / MDL2 Assets
		private const string GlyphDone = "";

		private string m_strCode = string.Empty;     // Digits only, e.g. "312381"
		private string m_strMessage = string.Empty;
		private bool m_bError = false;
		private int m_nRemaining = 0;
		private int m_nPeriod = TotpGenerator.DefaultPeriod;

		private bool m_bHoverCopy = false;
		private bool m_bPressedCopy = false;
		private bool m_bCopied = false;
		private readonly Timer m_tmCopied;

		private Font m_fontCode = null;
		private Font m_fontSmall = null;
		private Font m_fontGlyph = null;
		private bool m_bGlyphFont = false;
		private float m_fFontKey = -1f;

		public event EventHandler CopyRequested;

		public TotpCodeView()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
				ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable |
				ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
			this.TabStop = true;

			m_tmCopied = new Timer();
			m_tmCopied.Interval = 1500;
			m_tmCopied.Tick += delegate(object sender, EventArgs e)
			{
				m_tmCopied.Stop();
				m_bCopied = false;
				Invalidate();
			};
		}

		protected override void Dispose(bool disposing)
		{
			if(disposing)
			{
				m_tmCopied.Stop();
				m_tmCopied.Dispose();
				DisposeFonts();
			}
			base.Dispose(disposing);
		}

		public bool HasCode { get { return (m_strCode.Length > 0); } }

		/// <summary>
		/// The code as plain digits. The card draws the digit groups with a
		/// separator line between them; that line is not part of any text.
		/// </summary>
		public void ShowCode(string strCode, int nRemaining, int nPeriod)
		{
			strCode = (strCode ?? string.Empty);
			if((strCode == m_strCode) && (nRemaining == m_nRemaining) &&
				(nPeriod == m_nPeriod) && (m_strMessage.Length == 0)) return;

			if(strCode != m_strCode) m_bCopied = false;
			m_strCode = strCode;
			m_nRemaining = nRemaining;
			m_nPeriod = nPeriod;
			m_strMessage = string.Empty;
			m_bError = false;
			Invalidate();
		}

		public void ShowMessage(string strMessage, bool bError)
		{
			strMessage = (strMessage ?? string.Empty);
			if((m_strCode.Length == 0) && (strMessage == m_strMessage) && (bError == m_bError)) return;

			m_strCode = string.Empty;
			m_strMessage = strMessage;
			m_bError = bError;
			m_bCopied = false;
			Invalidate();
		}

		/// <summary>Shows a check mark on the copy button for a moment.</summary>
		public void ConfirmCopied()
		{
			m_bCopied = true;
			m_tmCopied.Stop();
			m_tmCopied.Start();
			Invalidate();
		}

		private void RequestCopy()
		{
			if(!this.HasCode) return;
			EventHandler h = this.CopyRequested;
			if(h != null) h(this, EventArgs.Empty);
		}

		// ===================== Layout =====================

		private int S(Graphics g, int n) { return Gfx.Scale(g, n); }

		private Rectangle CardRect { get { return new Rectangle(0, 0, this.Width, this.Height); } }

		private Rectangle CopyRect(Graphics g)
		{
			int n = Math.Min(this.Height - S(g, 12), S(g, 32));
			return new Rectangle(this.Width - S(g, 8) - n, (this.Height - n) / 2, n, n);
		}

		private Rectangle RingRect(Graphics g)
		{
			Rectangle rcCopy = CopyRect(g);
			int n = Math.Min(this.Height - S(g, 14), S(g, 30));
			return new Rectangle(rcCopy.Left - S(g, 10) - n, (this.Height - n) / 2, n, n);
		}

		private void EnsureFonts(Graphics g)
		{
			float fKey = this.Height + this.Font.Size * 1000f + g.DpiX * 100000f;
			if((m_fontCode != null) && (fKey == m_fFontKey)) return;
			DisposeFonts();
			m_fFontKey = fKey;

			// Code: Segoe UI Semibold (tabular digits) for Windows 11; the
			// aethercert brand sets data in a monospaced font
			float fCodePx = Math.Max(14f, this.Height * 0.46f);
			bool bMono = (ThemeManager.IsActive && ThemeManager.Palette.IsAethercert);
			m_fontCode = (bMono ? (TryFont("Cascadia Mono", fCodePx, FontStyle.Regular) ??
				TryFont("Consolas", fCodePx, FontStyle.Regular)) : TryFont("Segoe UI Semibold", fCodePx,
				FontStyle.Regular));
			if(m_fontCode == null) m_fontCode = new Font(FontFamily.GenericSansSerif, fCodePx,
				FontStyle.Bold, GraphicsUnit.Pixel);

			m_fontSmall = new Font(this.Font.FontFamily, this.Font.Size * 0.9f, FontStyle.Regular);

			float fGlyphPx = Gfx.ScaleF(g, 14f);
			m_fontGlyph = (TryFont("Segoe Fluent Icons", fGlyphPx, FontStyle.Regular) ??
				TryFont("Segoe MDL2 Assets", fGlyphPx, FontStyle.Regular));
			m_bGlyphFont = (m_fontGlyph != null);
			if(m_fontGlyph == null) m_fontGlyph = new Font(this.Font, FontStyle.Regular);
		}

		private static Font TryFont(string strFamily, float fPx, FontStyle fs)
		{
			try
			{
				Font f = new Font(strFamily, fPx, fs, GraphicsUnit.Pixel);
				if(string.Equals(f.Name, strFamily, StringComparison.OrdinalIgnoreCase)) return f;
				f.Dispose(); // Substituted: font not installed
			}
			catch(Exception) { }
			return null;
		}

		private void DisposeFonts()
		{
			if(m_fontCode != null) { m_fontCode.Dispose(); m_fontCode = null; }
			if(m_fontSmall != null) { m_fontSmall.Dispose(); m_fontSmall = null; }
			if(m_fontGlyph != null) { m_fontGlyph.Dispose(); m_fontGlyph = null; }
		}

		// ===================== Painting =====================

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics g = e.Graphics;
			EnsureFonts(g);

			bool bThemed = ThemeManager.IsActive;
			ThemePalette p = ThemeManager.Palette;
			Color clrParent = Gfx.ParentBack(this);
			Color clrCard = (bThemed ? p.Surface : SystemColors.Window);
			Color clrBorder = (bThemed ? (this.Focused ? p.Focus : p.Border) :
				(this.Focused ? SystemColors.Highlight : SystemColors.ControlDark));
			Color clrText = (bThemed ? p.Text : SystemColors.WindowText);
			Color clrDim = (bThemed ? p.DimText : SystemColors.GrayText);
			Color clrTrack = (bThemed ? p.Border : SystemColors.ControlLight);
			float fRadius = Gfx.ScaleF(g, (bThemed ? p.Radius : 4) + 2);

			using(SolidBrush br = new SolidBrush(clrParent)) { g.FillRectangle(br, this.ClientRectangle); }
			Gfx.FillRound(g, this.CardRect, fRadius, clrCard, clrBorder);

			int xPad = S(g, 14);
			Rectangle rcRing = RingRect(g);
			Rectangle rcCopy = CopyRect(g);
			Rectangle rcText = new Rectangle(xPad, 0, Math.Max(0, rcRing.Left - S(g, 10) - xPad), this.Height);

			if(this.HasCode)
			{
				PaintCode(g, rcText, clrText, clrDim, clrCard);

				PaintRing(g, rcRing, clrTrack, clrDim, clrCard);
				PaintCopyButton(g, rcCopy, bThemed, p, clrCard, clrDim);
			}
			else
			{
				Color clrMsg = (m_bError ? ThemeManager.ErrorColor : clrDim);
				Rectangle rcMsg = new Rectangle(xPad, 0, Math.Max(0, this.Width - 2 * xPad), this.Height);
				TextRenderer.DrawText(g, m_strMessage, this.Font, rcMsg, clrMsg, clrCard,
					TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak |
					TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
			}
		}

		private void PaintCode(Graphics g, Rectangle rc, Color clrText, Color clrDim, Color clrCard)
		{
			const TextFormatFlags tff = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
				TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

			string[] vGroups = TotpGenerator.SplitCode(m_strCode);
			float fEm = m_fontCode.Size; // Pixels
			int nGap = (int)Math.Round(fEm * 0.55f);
			float yDigits = rc.Y + GetDigitCenter(rc.Height, tff);

			int x = rc.X;
			for(int i = 0; i < vGroups.Length; ++i)
			{
				if(i > 0)
				{
					// Separator: a short drawn dash, not a character
					float fLen = nGap * 0.46f;
					float fThick = Math.Max(1.5f, Gfx.ScaleF(g, 2f));
					float x0 = x + (nGap - fLen) / 2f;
					SmoothingMode sm = g.SmoothingMode;
					g.SmoothingMode = SmoothingMode.AntiAlias;
					using(Pen pen = new Pen(clrDim, fThick))
					{
						pen.StartCap = LineCap.Round;
						pen.EndCap = LineCap.Round;
						g.DrawLine(pen, x0, yDigits, x0 + fLen, yDigits);
					}
					g.SmoothingMode = sm;
					x += nGap;
				}

				int w = TextRenderer.MeasureText(g, vGroups[i], m_fontCode, Size.Empty, tff).Width;
				if(x + w > rc.Right) break;
				TextRenderer.DrawText(g, vGroups[i], m_fontCode, new Rectangle(x, rc.Y, w, rc.Height),
					clrText, clrCard, tff);
				x += w;
			}
		}

		private float m_fDigitCenter = -1f;
		private string m_strDigitCenterKey = string.Empty;

		/// <summary>
		/// Vertical center of the digits' ink, measured once per font and
		/// height (font metrics differ too much between fonts to guess it).
		/// </summary>
		private float GetDigitCenter(int h, TextFormatFlags tff)
		{
			string strKey = m_fFontKey.ToString() + "|" + h.ToString();
			if((m_fDigitCenter >= 0f) && (strKey == m_strDigitCenterKey)) return m_fDigitCenter;

			float fCenter = h / 2f;
			int w = Math.Max(8, (int)Math.Ceiling(m_fontCode.Size * 1.5f));
			using(Bitmap bmp = new Bitmap(w, Math.Max(1, h)))
			{
				using(Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.White);
					TextRenderer.DrawText(g, "08", m_fontCode, new Rectangle(0, 0, w, h), Color.Black,
						Color.White, tff);
				}
				int yTop = -1, yBottom = -1;
				for(int y = 0; y < bmp.Height; ++y)
				{
					for(int x = 0; x < bmp.Width; ++x)
					{
						if(bmp.GetPixel(x, y).R < 128)
						{
							if(yTop < 0) yTop = y;
							yBottom = y;
							break;
						}
					}
				}
				if(yTop >= 0) fCenter = (yTop + yBottom + 1) / 2f;
			}

			m_fDigitCenter = fCenter;
			m_strDigitCenterKey = strKey;
			return fCenter;
		}

		private void PaintRing(Graphics g, Rectangle rc, Color clrTrack, Color clrDim, Color clrCard)
		{
			SmoothingMode sm = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			float fPen = Math.Max(2f, Gfx.ScaleF(g, 2.5f));
			RectangleF rcF = new RectangleF(rc.X + fPen / 2f, rc.Y + fPen / 2f, rc.Width - fPen, rc.Height - fPen);

			bool bLow = (m_nRemaining <= 5);
			Color clrArc = (bLow ? ThemeManager.WarningColor : ThemeManager.AccentColor);
			if(ThemeManager.IsActive && ThemeManager.Palette.IsAethercert)
				clrArc = (bLow ? ThemeManager.WarningColor : ThemeManager.Palette.Text); // aethercert: ink

			using(Pen pTrack = new Pen(clrTrack, fPen)) { g.DrawEllipse(pTrack, rcF); }
			if(m_nPeriod > 0)
			{
				float fSweep = 360f * Math.Min(m_nRemaining, m_nPeriod) / m_nPeriod;
				using(Pen pArc = new Pen(clrArc, fPen))
				{
					pArc.StartCap = LineCap.Round;
					pArc.EndCap = LineCap.Round;
					g.DrawArc(pArc, rcF, -90f, fSweep);
				}
			}
			g.SmoothingMode = sm;

			TextRenderer.DrawText(g, m_nRemaining.ToString(), m_fontSmall, rc,
				(bLow ? ThemeManager.WarningColor : clrDim), clrCard, TextFormatFlags.HorizontalCenter |
				TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}

		private void PaintCopyButton(Graphics g, Rectangle rc, bool bThemed, ThemePalette p,
			Color clrCard, Color clrDim)
		{
			Color clrBack = clrCard;
			if(m_bPressedCopy) clrBack = (bThemed ? p.Pressed : SystemColors.ControlDark);
			else if(m_bHoverCopy) clrBack = (bThemed ? p.Hover : SystemColors.ControlLight);
			if(clrBack != clrCard)
				Gfx.FillRound(g, rc, Gfx.ScaleF(g, (bThemed ? p.Radius : 4)), clrBack, Color.Transparent);

			Color clrGlyph = (m_bCopied ? ThemeManager.SuccessColor : ((m_bHoverCopy && bThemed) ? p.Text : clrDim));
			string strGlyph = (m_bGlyphFont ? (m_bCopied ? GlyphDone : GlyphCopy) :
				(m_bCopied ? "✓" : "⎘"));
			TextRenderer.DrawText(g, strGlyph, m_fontGlyph, rc, clrGlyph, clrBack,
				TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
				TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
		}

		// ===================== Input =====================

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			bool bHover = false;
			if(this.HasCode)
			{
				using(Graphics g = CreateGraphics()) { bHover = CopyRect(g).Contains(e.Location); }
			}
			if(bHover != m_bHoverCopy) { m_bHoverCopy = bHover; Invalidate(); }
			this.Cursor = (bHover ? Cursors.Hand : Cursors.Default);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			if(m_bHoverCopy || m_bPressedCopy)
			{
				m_bHoverCopy = false;
				m_bPressedCopy = false;
				Invalidate();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			Focus();
			if((e.Button == MouseButtons.Left) && m_bHoverCopy) { m_bPressedCopy = true; Invalidate(); }
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			if(m_bPressedCopy)
			{
				m_bPressedCopy = false;
				Invalidate();
				if(m_bHoverCopy) RequestCopy();
			}
		}

		protected override void OnDoubleClick(EventArgs e)
		{
			base.OnDoubleClick(e);
			if(!m_bHoverCopy) RequestCopy();
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if((e.KeyCode == Keys.Space) || ((e.KeyCode == Keys.C) && e.Control))
			{
				RequestCopy();
				e.Handled = true;
				e.SuppressKeyPress = true;
			}
		}

		protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
		protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
	}
}
