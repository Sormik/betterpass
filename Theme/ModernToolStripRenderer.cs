/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Flat renderer for menus, toolbars, context menus and status bars:
  rounded hover highlights, hairline separators, no gradients, no grip and
  no separate image margin. On Windows 11 popups get rounded corners from
  the window manager.
*/

using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace BetterPass
{
	internal sealed class ModernColorTable : ProfessionalColorTable
	{
		public ModernColorTable()
		{
			this.UseSystemColors = false;
		}

		private static Color Back { get { return ThemeManager.Palette.Back; } }
		private static Color Menu { get { return ThemeManager.Palette.Surface; } }
		private static Color Hover { get { return ThemeManager.Palette.Hover; } }
		private static Color Pressed { get { return ThemeManager.Palette.Pressed; } }
		private static Color Border { get { return ThemeManager.Palette.Border; } }

		public override Color MenuItemSelected { get { return Hover; } }
		public override Color MenuItemSelectedGradientBegin { get { return Hover; } }
		public override Color MenuItemSelectedGradientEnd { get { return Hover; } }
		public override Color MenuItemBorder { get { return Hover; } }
		public override Color MenuBorder { get { return Border; } }
		public override Color MenuItemPressedGradientBegin { get { return Pressed; } }
		public override Color MenuItemPressedGradientMiddle { get { return Pressed; } }
		public override Color MenuItemPressedGradientEnd { get { return Pressed; } }
		public override Color ToolStripDropDownBackground { get { return Menu; } }
		public override Color ImageMarginGradientBegin { get { return Menu; } }
		public override Color ImageMarginGradientMiddle { get { return Menu; } }
		public override Color ImageMarginGradientEnd { get { return Menu; } }
		public override Color MenuStripGradientBegin { get { return Back; } }
		public override Color MenuStripGradientEnd { get { return Back; } }
		public override Color ToolStripGradientBegin { get { return Back; } }
		public override Color ToolStripGradientMiddle { get { return Back; } }
		public override Color ToolStripGradientEnd { get { return Back; } }
		public override Color ToolStripBorder { get { return Back; } }
		public override Color ToolStripContentPanelGradientBegin { get { return Back; } }
		public override Color ToolStripContentPanelGradientEnd { get { return Back; } }
		public override Color ToolStripPanelGradientBegin { get { return Back; } }
		public override Color ToolStripPanelGradientEnd { get { return Back; } }
		public override Color StatusStripGradientBegin { get { return Back; } }
		public override Color StatusStripGradientEnd { get { return Back; } }
		public override Color SeparatorDark { get { return Border; } }
		public override Color SeparatorLight { get { return Border; } }
		public override Color GripDark { get { return Back; } }
		public override Color GripLight { get { return Back; } }
		public override Color ButtonSelectedHighlight { get { return Hover; } }
		public override Color ButtonSelectedHighlightBorder { get { return Hover; } }
		public override Color ButtonSelectedGradientBegin { get { return Hover; } }
		public override Color ButtonSelectedGradientMiddle { get { return Hover; } }
		public override Color ButtonSelectedGradientEnd { get { return Hover; } }
		public override Color ButtonSelectedBorder { get { return Hover; } }
		public override Color ButtonPressedHighlight { get { return Pressed; } }
		public override Color ButtonPressedHighlightBorder { get { return Pressed; } }
		public override Color ButtonPressedGradientBegin { get { return Pressed; } }
		public override Color ButtonPressedGradientMiddle { get { return Pressed; } }
		public override Color ButtonPressedGradientEnd { get { return Pressed; } }
		public override Color ButtonPressedBorder { get { return Pressed; } }
		public override Color ButtonCheckedHighlight { get { return Pressed; } }
		public override Color ButtonCheckedHighlightBorder { get { return Pressed; } }
		public override Color ButtonCheckedGradientBegin { get { return Pressed; } }
		public override Color ButtonCheckedGradientMiddle { get { return Pressed; } }
		public override Color ButtonCheckedGradientEnd { get { return Pressed; } }
		public override Color CheckBackground { get { return Pressed; } }
		public override Color CheckSelectedBackground { get { return Pressed; } }
		public override Color CheckPressedBackground { get { return Pressed; } }
		public override Color OverflowButtonGradientBegin { get { return Back; } }
		public override Color OverflowButtonGradientMiddle { get { return Back; } }
		public override Color OverflowButtonGradientEnd { get { return Back; } }
		public override Color RaftingContainerGradientBegin { get { return Back; } }
		public override Color RaftingContainerGradientEnd { get { return Back; } }
	}

	internal sealed class ModernToolStripRenderer : ToolStripProfessionalRenderer
	{
		// Popups whose border the window manager draws (Windows 11)
		private static readonly ConditionalWeakTable<ToolStrip, object> g_rounded =
			new ConditionalWeakTable<ToolStrip, object>();

		public ModernToolStripRenderer() : base(new ModernColorTable())
		{
			this.RoundedEdges = false;
		}

		private static ThemePalette P { get { return ThemeManager.Palette; } }

		// ===================== Backgrounds =====================

		protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
		{
			ToolStripDropDown dd = (e.ToolStrip as ToolStripDropDown);
			if((dd != null) && dd.IsHandleCreated)
			{
				object o;
				if(!g_rounded.TryGetValue(dd, out o))
				{
					bool bRounded = ThemeNative.TrySetRoundedPopup(dd.Handle, P.Border);
					try { g_rounded.Add(dd, bRounded); }
					catch(ArgumentException) { }
				}
			}

			Color clr = ((dd != null) ? P.Surface : P.Back);
			using(SolidBrush br = new SolidBrush(clr))
			{
				e.Graphics.FillRectangle(br, new Rectangle(Point.Empty, e.ToolStrip.Size));
			}
		}

		protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
		{
			// One continuous menu surface, no gray icon column
		}

		protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
		{
			// No grip dots
		}

		protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
		{
			ToolStripDropDown dd = (e.ToolStrip as ToolStripDropDown);
			if(dd != null)
			{
				object o;
				if(g_rounded.TryGetValue(dd, out o) && (o is bool) && (bool)o) return;

				using(Pen pen = new Pen(P.Border))
				{
					e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
				}
				return;
			}

			if(e.ToolStrip is StatusStrip)
			{
				using(Pen pen = new Pen(P.Border))
				{
					e.Graphics.DrawLine(pen, 0, 0, e.ToolStrip.Width, 0);
				}
			}
			// No border lines for menu bars and toolbars
		}

		// ===================== Items =====================

		private static void FillItem(Graphics g, Rectangle rc, Color clr)
		{
			if((rc.Width <= 2) || (rc.Height <= 2)) return;
			Gfx.FillRound(g, rc, Gfx.ScaleF(g, P.Radius), clr, Color.Transparent);
		}

		protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
		{
			ToolStripItem tsi = e.Item;
			if(!tsi.Enabled) return; // Windows 11 does not highlight disabled items

			bool bOpen = ((tsi is ToolStripMenuItem) && ((ToolStripMenuItem)tsi).DropDown.Visible);
			if(!tsi.Selected && !tsi.Pressed && !bOpen) return;

			Rectangle rc = new Rectangle(Point.Empty, tsi.Size);
			if(tsi.IsOnDropDown)
			{
				int dx = Gfx.Scale(e.Graphics, 4);
				rc = new Rectangle(dx, 1, rc.Width - 2 * dx, rc.Height - 2);
			}
			else rc = Rectangle.Inflate(rc, -1, -Gfx.Scale(e.Graphics, 2));

			FillItem(e.Graphics, rc, ((tsi.Pressed && !tsi.IsOnDropDown) ? P.Pressed : P.Hover));
		}

		private static void RenderButton(Graphics g, ToolStripItem tsi, bool bChecked)
		{
			Rectangle rc = Rectangle.Inflate(new Rectangle(Point.Empty, tsi.Size), -1, -1);
			if(tsi.Pressed) FillItem(g, rc, P.Pressed);
			else if(tsi.Selected && tsi.Enabled) FillItem(g, rc, P.Hover);
			else if(bChecked) FillItem(g, rc, P.Pressed);
		}

		protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
		{
			ToolStripButton tsb = (e.Item as ToolStripButton);
			RenderButton(e.Graphics, e.Item, ((tsb != null) && tsb.Checked));
		}

		protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
		{
			RenderButton(e.Graphics, e.Item, false);
		}

		protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
		{
			ToolStripSplitButton tssb = (e.Item as ToolStripSplitButton);
			RenderButton(e.Graphics, e.Item, false);
			if(tssb == null) return;

			Rectangle rcArrow = tssb.DropDownButtonBounds;
			if(tssb.Selected || tssb.Pressed)
			{
				using(Pen pen = new Pen(P.Border))
				{
					e.Graphics.DrawLine(pen, rcArrow.Left, rcArrow.Top + 3, rcArrow.Left, rcArrow.Bottom - 4);
				}
			}
			Gfx.DrawChevron(e.Graphics, rcArrow, true, (tssb.Enabled ? P.DimText : P.DisabledText));
		}

		protected override void OnRenderOverflowButtonBackground(ToolStripItemRenderEventArgs e)
		{
			RenderButton(e.Graphics, e.Item, false);
			Rectangle rc = new Rectangle(Point.Empty, e.Item.Size);
			Gfx.DrawChevron(e.Graphics, rc, true, P.DimText);
		}

		protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
		{
			Rectangle rc = e.ImageRectangle;
			if(e.Item.Image != null)
			{
				// Checked item with icon: subtle rounded plate behind the icon
				FillItem(e.Graphics, Rectangle.Inflate(rc, 2, 2), P.Pressed);
				return;
			}

			Gfx.DrawCheckMark(e.Graphics, rc, (e.Item.Enabled ? P.Text : P.DisabledText),
				Math.Max(1.2f, Gfx.ScaleF(e.Graphics, 1.5f)));
		}

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			e.TextColor = (e.Item.Enabled ? P.Text : P.DisabledText);
			base.OnRenderItemText(e);
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
		{
			e.ArrowColor = (((e.Item == null) || e.Item.Enabled) ? P.DimText : P.DisabledText);
			base.OnRenderArrow(e);
		}

		protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
		{
			Rectangle rc = new Rectangle(Point.Empty, e.Item.Size);
			using(Pen pen = new Pen(P.Border))
			{
				if(e.Vertical)
				{
					int x = rc.Width / 2;
					e.Graphics.DrawLine(pen, x, rc.Top + 4, x, rc.Bottom - 4);
				}
				else
				{
					int y = rc.Height / 2;
					int dx = Gfx.Scale(e.Graphics, 4);
					e.Graphics.DrawLine(pen, rc.Left + dx, y, rc.Right - 1 - dx, y);
				}
			}
		}
	}
}
