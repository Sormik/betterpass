/*
  BetterPass - KeePass plugin
  Win32 interop for the dark theme.
*/

using System;
using System.Runtime.InteropServices;

namespace BetterPass
{
	internal static class ThemeNative
	{
		public const int WM_PAINT = 0x000F;
		public const int WM_ERASEBKGND = 0x0014;
		public const int WM_NOTIFY = 0x004E;
		public const int WM_SETREDRAW = 0x000B;
		public const int WM_NCPAINT = 0x0085;
		public const int WM_SETFOCUS = 0x0007;
		public const int WM_KILLFOCUS = 0x0008;
		public const int WM_ENABLE = 0x000A;
		public const int WM_CTLCOLORSTATIC = 0x0138;
		public const int WM_QUERYUISTATE = 0x0129;

		public const int UISF_HIDEFOCUS = 0x1;
		public const int UISF_HIDEACCEL = 0x2;

		public const int LVM_GETHEADER = 0x1000 + 31;

		public const int NM_CUSTOMDRAW = -12;
		public const int CDDS_PREPAINT = 0x00000001;
		public const int CDDS_ITEMPREPAINT = 0x00010001;
		public const int CDRF_DODEFAULT = 0x00000000;
		public const int CDRF_NOTIFYITEMDRAW = 0x00000020;

		private const uint RDW_INVALIDATE = 0x0001;
		private const uint RDW_FRAME = 0x0400;

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
		private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33; // Windows 11
		private const int DWMWA_BORDER_COLOR = 34;
		private const int DWMWA_CAPTION_COLOR = 35;
		private const int DWMWA_TEXT_COLOR = 36;
		private const int DWMWCP_ROUNDSMALL = 3;
		private const int DWMWA_COLOR_DEFAULT = unchecked((int)0xFFFFFFFF);

		[StructLayout(LayoutKind.Sequential)]
		public struct NMHDR
		{
			public IntPtr hwndFrom;
			public IntPtr idFrom;
			public int code;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int left, top, right, bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct NMCUSTOMDRAW
		{
			public NMHDR hdr;
			public int dwDrawStage;
			public IntPtr hdc;
			public RECT rc;
			public IntPtr dwItemSpec;
			public uint uItemState;
			public IntPtr lItemlParam;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct PAINTSTRUCT
		{
			public IntPtr hdc;
			public int fErase;
			public RECT rcPaint;
			public int fRestore;
			public int fIncUpdate;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
			public byte[] rgbReserved;
		}

		[DllImport("UxTheme.dll", CharSet = CharSet.Unicode)]
		private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName,
			string pszSubIdList);

		[DllImport("DwmApi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute,
			ref int pvAttribute, int cbAttribute);

		[DllImport("User32.dll")]
		public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam,
			IntPtr lParam);

		[DllImport("User32.dll")]
		public static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

		[DllImport("User32.dll")]
		public static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

		[DllImport("User32.dll")]
		public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

		[DllImport("User32.dll")]
		public static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("Gdi32.dll")]
		public static extern int SetTextColor(IntPtr hdc, int crColor);

		[DllImport("Gdi32.dll")]
		public static extern int SetBkColor(IntPtr hdc, int crColor);

		[DllImport("Gdi32.dll")]
		private static extern IntPtr CreateSolidBrush(int crColor);

		[DllImport("User32.dll")]
		public static extern IntPtr GetWindowDC(IntPtr hWnd);

		[DllImport("User32.dll")]
		public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("User32.dll")]
		private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate,
			IntPtr hrgnUpdate, uint flags);

		private static readonly System.Collections.Generic.Dictionary<int, IntPtr> g_dBrushes =
			new System.Collections.Generic.Dictionary<int, IntPtr>();

		/// <summary>
		/// GDI brush for WM_CTLCOLOR* replies; cached for the lifetime of the
		/// process (only a handful of palette colors are ever requested).
		/// </summary>
		public static IntPtr GetBrush(System.Drawing.Color c)
		{
			int cr = ToColorRef(c);
			IntPtr h;
			if(!g_dBrushes.TryGetValue(cr, out h))
			{
				h = CreateSolidBrush(cr);
				g_dBrushes[cr] = h;
			}
			return h;
		}

		public static void RedrawFrame(IntPtr hWnd)
		{
			if(hWnd == IntPtr.Zero) return;
			try { RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, RDW_FRAME | RDW_INVALIDATE); }
			catch(Exception) { }
		}

		public static int QueryUiState(IntPtr hWnd)
		{
			if(hWnd == IntPtr.Zero) return 0;
			try { return SendMessage(hWnd, WM_QUERYUISTATE, IntPtr.Zero, IntPtr.Zero).ToInt32(); }
			catch(Exception) { return 0; }
		}

		/// <summary>
		/// Title bar in the palette's colors (Windows 11), or the system
		/// default when p is null. Before Windows 11 only the dark flag applies.
		/// </summary>
		public static void TrySetTitleBar(IntPtr hWnd, ThemePalette p)
		{
			if(hWnd == IntPtr.Zero) return;
			TrySetDarkTitleBar(hWnd, (p != null) && p.IsDark);
			SetDwmInt(hWnd, DWMWA_CAPTION_COLOR, ((p != null) ? ToColorRef(p.Back) : DWMWA_COLOR_DEFAULT));
			SetDwmInt(hWnd, DWMWA_BORDER_COLOR, ((p != null) ? ToColorRef(p.Border) : DWMWA_COLOR_DEFAULT));
			SetDwmInt(hWnd, DWMWA_TEXT_COLOR, ((p != null) ? ToColorRef(p.Text) : DWMWA_COLOR_DEFAULT));
		}

		/// <summary>
		/// Rounded corners and a hairline border for popup windows (menus).
		/// Returns false before Windows 11, where the caller draws its own border.
		/// </summary>
		public static bool TrySetRoundedPopup(IntPtr hWnd, System.Drawing.Color clrBorder)
		{
			if(hWnd == IntPtr.Zero) return false;
			if(!SetDwmInt(hWnd, DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUNDSMALL)) return false;
			SetDwmInt(hWnd, DWMWA_BORDER_COLOR, ToColorRef(clrBorder));
			return true;
		}

		private static bool SetDwmInt(IntPtr hWnd, int nAttr, int nValue)
		{
			try
			{
				int v = nValue;
				return (DwmSetWindowAttribute(hWnd, nAttr, ref v, 4) == 0);
			}
			catch(Exception) { return false; } // No DWM API (before Vista)
		}

		public static void TrySetWindowTheme(IntPtr hWnd, string strTheme)
		{
			if(hWnd == IntPtr.Zero) return;
			try { SetWindowTheme(hWnd, strTheme, null); }
			catch(Exception) { }
		}

		public static void TrySetDarkTitleBar(IntPtr hWnd, bool bDark)
		{
			if(hWnd == IntPtr.Zero) return;
			try
			{
				int v = (bDark ? 1 : 0);
				if(DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, 4) != 0)
					DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref v, 4);
			}
			catch(Exception) { } // Not available before Windows 10
		}

		public static int ToColorRef(System.Drawing.Color c)
		{
			return (c.R | (c.G << 8) | (c.B << 16));
		}
	}
}
