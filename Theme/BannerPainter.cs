/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Dialog banners (the strip at the top of KeePass dialogs) in the theme's
  colors: flat surface, icon, title and description, a hairline below.
  The aethercert design adds its gold accent line above the title.

  Uses KeePass' official hook BannerFactory.CustomGenerator.
*/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using KeePass.UI;

namespace BetterPass
{
	internal static class BannerPainter
	{
		private static bool g_bInstalled = false;
		private static BfBannerGenerator g_genPrev = null;

		public static void Install()
		{
			if(g_bInstalled) return;
			try
			{
				g_genPrev = BannerFactory.CustomGenerator;
				BannerFactory.CustomGenerator = Generate;
				g_bInstalled = true;
			}
			catch(Exception) { }
		}

		public static void Uninstall()
		{
			if(!g_bInstalled) return;
			try { BannerFactory.CustomGenerator = g_genPrev; }
			catch(Exception) { }
			g_genPrev = null;
			g_bInstalled = false;
		}

		private static Image Generate(BfBannerInfo bi)
		{
			if(!ThemeManager.IsActive || (bi == null) || (bi.Width <= 0) || (bi.Height <= 0))
				return ((g_genPrev != null) ? g_genPrev(bi) : null);

			ThemePalette p = ThemeManager.Palette;
			int w = bi.Width, h = bi.Height;
			Bitmap bmp = new Bitmap(w, h);
			try
			{
				using(Graphics g = Graphics.FromImage(bmp))
				{
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;
					using(SolidBrush br = new SolidBrush(p.Back)) { g.FillRectangle(br, 0, 0, w, h); }
					using(Pen pen = new Pen(p.Border)) { g.DrawLine(pen, 0, h - 1, w, h - 1); }

					float fUnit = h / 60f; // KeePass banners are 60 px high at 96 DPI
					int xPad = (int)Math.Round(16f * fUnit);
					int x = xPad;

					int nIcon = Math.Min((int)Math.Round(36f * fUnit), h - 2 * (int)Math.Round(8f * fUnit));
					if((nIcon > 0) && IconSet.Available)
					{
						// Modern icon tile: Fluent glyph matching the dialog
						Rectangle rcTile = new Rectangle(x, (h - nIcon) / 2, nIcon, nIcon);
						Color clrTile = (p.IsAethercert ? p.Control : ThemePalette.Mix(p.Back, p.Accent, (p.IsDark ? 0.18 : 0.12)));
						Color clrGlyph = (p.IsAethercert ? p.Text : p.Accent);
						Gfx.FillRound(g, rcTile, (p.Radius + 2) * fUnit, clrTile, Color.Transparent);
						int nGlyph = (int)Math.Round(nIcon * 0.5f);
						using(Bitmap bmpGlyph = IconSet.Render(IconSet.BannerGlyph(bi.TitleText),
							new Size(nGlyph, nGlyph), clrGlyph))
						{
							g.DrawImage(bmpGlyph, rcTile.X + (nIcon - nGlyph) / 2, rcTile.Y + (nIcon - nGlyph) / 2,
								nGlyph, nGlyph);
						}
						x += nIcon + (int)Math.Round(14f * fUnit);
					}
					else if((bi.Icon != null) && (nIcon > 0))
					{
						g.DrawImage(bi.Icon, new Rectangle(x, (h - nIcon) / 2, nIcon, nIcon));
						x += nIcon + (int)Math.Round(14f * fUnit);
					}

					using(Font fTitle = new Font("Segoe UI Semibold", 15f * fUnit, FontStyle.Regular, GraphicsUnit.Pixel))
					{
						using(Font fInfo = new Font("Segoe UI", 12f * fUnit, FontStyle.Regular, GraphicsUnit.Pixel))
						{
							string strTitle = (bi.TitleText ?? string.Empty);
							string strInfo = (bi.InfoText ?? string.Empty);
							int hTitle = TextRenderer.MeasureText(g, "Ag", fTitle).Height;
							int hInfo = ((strInfo.Length > 0) ? TextRenderer.MeasureText(g, "Ag", fInfo).Height : 0);
							int yTitle = (h - hTitle - hInfo) / 2;
							int wText = Math.Max(0, w - x - xPad);

							if(p.IsAethercert) // Gold eyebrow line
							{
								int wLine = (int)Math.Round(16f * fUnit), hLine = Math.Max(2, (int)Math.Round(2f * fUnit));
								using(SolidBrush br = new SolidBrush(p.Highlight))
								{
									g.FillRectangle(br, x + (int)Math.Round(2f * fUnit), yTitle - hLine, wLine, hLine);
								}
							}

							TextRenderer.DrawText(g, strTitle, fTitle, new Rectangle(x, yTitle, wText, hTitle),
								p.Text, p.Back, TextFormatFlags.Left | TextFormatFlags.SingleLine |
								TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
							if(strInfo.Length > 0)
								TextRenderer.DrawText(g, strInfo, fInfo, new Rectangle(x, yTitle + hTitle, wText, hInfo),
									p.DimText, p.Back, TextFormatFlags.Left | TextFormatFlags.SingleLine |
									TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
						}
					}
				}
			}
			catch(Exception)
			{
				bmp.Dispose();
				return ((g_genPrev != null) ? g_genPrev(bi) : null);
			}
			return bmp;
		}
	}
}
