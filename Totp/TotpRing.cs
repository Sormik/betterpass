/*
  BetterPass - KeePass plugin
  Small round countdown indicator (remaining seconds of the current code).
*/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BetterPass
{
	internal sealed class TotpRing : Control
	{
		private bool m_bActive = false;
		private int m_nRemaining = 0;
		private int m_nPeriod = TotpGenerator.DefaultPeriod;

		public TotpRing()
		{
			SetStyle(ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
				ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
			this.BackColor = Color.Transparent;
			this.TabStop = false;
		}

		public void SetState(bool bActive, int nRemaining, int nPeriod)
		{
			if((bActive == m_bActive) && (nRemaining == m_nRemaining) &&
				(nPeriod == m_nPeriod)) return;

			m_bActive = bActive;
			m_nRemaining = nRemaining;
			m_nPeriod = nPeriod;
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			Graphics g = e.Graphics;
			g.SmoothingMode = SmoothingMode.AntiAlias;

			int nSize = Math.Min(this.ClientSize.Width, this.ClientSize.Height);
			if(nSize < 6) return;

			float fPen = Math.Max(2f, nSize / 10f);
			float fOff = fPen / 2f + 0.5f;
			RectangleF rc = new RectangleF((this.ClientSize.Width - nSize) / 2f + fOff,
				(this.ClientSize.Height - nSize) / 2f + fOff,
				nSize - 2f * fOff, nSize - 2f * fOff);

			Color clrAccent;
			if(!m_bActive) clrAccent = SystemColors.GrayText;
			else if(m_nRemaining <= 5) clrAccent = Color.FromArgb(200, 60, 40);
			else clrAccent = Color.FromArgb(31, 95, 125);

			using(Pen pTrack = new Pen(Color.FromArgb(55, clrAccent), fPen))
			{
				g.DrawEllipse(pTrack, rc);
			}

			if(m_bActive && (m_nPeriod > 0))
			{
				float fSweep = 360f * Math.Min(m_nRemaining, m_nPeriod) / m_nPeriod;
				using(Pen pArc = new Pen(clrAccent, fPen))
				{
					g.DrawArc(pArc, rc, -90f, fSweep);
				}
			}

			string strText = (m_bActive ? m_nRemaining.ToString() : string.Empty);
			if(strText.Length > 0)
				TextRenderer.DrawText(g, strText, this.Font, this.ClientRectangle,
					this.ForeColor, TextFormatFlags.HorizontalCenter |
					TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
					TextFormatFlags.NoPadding);
		}
	}
}
