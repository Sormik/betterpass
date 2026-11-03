/*
  BetterPass - KeePass plugin
  Dark renderer for menus, toolbars, context menus and status bars.
*/

using System;
using System.Drawing;
using System.Windows.Forms;

namespace BetterPass
{
	internal sealed class DarkColorTable : ProfessionalColorTable
	{
		public DarkColorTable()
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
		public override Color MenuItemPressedGradientBegin { get { return Menu; } }
		public override Color MenuItemPressedGradientMiddle { get { return Menu; } }
		public override Color MenuItemPressedGradientEnd { get { return Menu; } }
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
		public override Color GripDark { get { return Border; } }
		public override Color GripLight { get { return Back; } }
		public override Color ButtonSelectedHighlight { get { return Hover; } }
		public override Color ButtonSelectedHighlightBorder { get { return Border; } }
		public override Color ButtonSelectedGradientBegin { get { return Hover; } }
		public override Color ButtonSelectedGradientMiddle { get { return Hover; } }
		public override Color ButtonSelectedGradientEnd { get { return Hover; } }
		public override Color ButtonSelectedBorder { get { return Border; } }
		public override Color ButtonPressedHighlight { get { return Pressed; } }
		public override Color ButtonPressedHighlightBorder { get { return Border; } }
		public override Color ButtonPressedGradientBegin { get { return Pressed; } }
		public override Color ButtonPressedGradientMiddle { get { return Pressed; } }
		public override Color ButtonPressedGradientEnd { get { return Pressed; } }
		public override Color ButtonPressedBorder { get { return Border; } }
		public override Color ButtonCheckedHighlight { get { return Pressed; } }
		public override Color ButtonCheckedHighlightBorder { get { return Border; } }
		public override Color ButtonCheckedGradientBegin { get { return Pressed; } }
		public override Color ButtonCheckedGradientMiddle { get { return Pressed; } }
		public override Color ButtonCheckedGradientEnd { get { return Pressed; } }
		public override Color CheckBackground { get { return Pressed; } }
		public override Color CheckSelectedBackground { get { return Hover; } }
		public override Color CheckPressedBackground { get { return Pressed; } }
		public override Color OverflowButtonGradientBegin { get { return Back; } }
		public override Color OverflowButtonGradientMiddle { get { return Back; } }
		public override Color OverflowButtonGradientEnd { get { return Back; } }
		public override Color RaftingContainerGradientBegin { get { return Back; } }
		public override Color RaftingContainerGradientEnd { get { return Back; } }
	}

	internal sealed class DarkToolStripRenderer : ToolStripProfessionalRenderer
	{
		public DarkToolStripRenderer() : base(new DarkColorTable())
		{
			this.RoundedEdges = false;
		}

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			e.TextColor = (e.Item.Enabled ? ThemeManager.Palette.Text :
				ThemeManager.Palette.DimText);
			base.OnRenderItemText(e);
		}

		protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
		{
			e.ArrowColor = ((e.Item == null) || e.Item.Enabled) ?
				ThemeManager.Palette.Text : ThemeManager.Palette.DimText;
			base.OnRenderArrow(e);
		}

		protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
		{
			if(e.ToolStrip is ToolStripDropDown)
			{
				using(Pen p = new Pen(ThemeManager.Palette.Border))
				{
					Rectangle rc = new Rectangle(Point.Empty, e.ToolStrip.Size);
					rc.Width -= 1;
					rc.Height -= 1;
					e.Graphics.DrawRectangle(p, rc);
				}
			}
			// No border lines for menu bars, toolbars and status bars
		}

		protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
		{
			Rectangle rc = new Rectangle(Point.Empty, e.Item.Size);
			using(Pen p = new Pen(ThemeManager.Palette.Border))
			{
				if(e.Vertical)
				{
					int x = rc.Width / 2;
					e.Graphics.DrawLine(p, x, rc.Top + 4, x, rc.Bottom - 4);
				}
				else
				{
					int y = rc.Height / 2;
					int xStart = ((e.ToolStrip is ToolStripDropDownMenu) ? 28 : 2);
					e.Graphics.DrawLine(p, xStart, y, rc.Right - 2, y);
				}
			}
		}
	}
}
