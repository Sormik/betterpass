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

		public const int LVM_GETHEADER = 0x1000 + 31;

		public const int NM_CUSTOMDRAW = -12;
		public const int CDDS_PREPAINT = 0x00000001;
		public const int CDDS_ITEMPREPAINT = 0x00010001;
		public const int CDRF_DODEFAULT = 0x00000000;
		public const int CDRF_NOTIFYITEMDRAW = 0x00000020;

		private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
		private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

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

		[DllImport("Gdi32.dll")]
		public static extern int SetTextColor(IntPtr hdc, int crColor);

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
