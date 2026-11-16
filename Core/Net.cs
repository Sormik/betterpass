/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Network and file helpers. Downloads go through KeePass' IOConnection,
  so KeePass' proxy and TLS settings apply.
*/

using System;
using System.IO;
using System.Text;

using KeePass.App.Configuration;

using KeePassLib.Serialization;

namespace BetterPass
{
	internal static class Net
	{
		public static byte[] Download(string strUrl, int cbMax)
		{
			if(string.IsNullOrEmpty(strUrl)) throw new ArgumentNullException("strUrl");

			IOConnectionInfo ioc = IOConnectionInfo.FromPath(strUrl);
			using(Stream s = IOConnection.OpenRead(ioc))
			{
				if(s == null) throw new IOException("No data received.");

				using(MemoryStream ms = new MemoryStream())
				{
					byte[] pbBuf = new byte[16384];
					while(true)
					{
						int n = s.Read(pbBuf, 0, pbBuf.Length);
						if(n <= 0) break;
						if(ms.Length + n > cbMax)
							throw new InvalidDataException("Response is too large.");
						ms.Write(pbBuf, 0, n);
					}
					return ms.ToArray();
				}
			}
		}

		public static string DownloadText(string strUrl, int cbMax)
		{
			byte[] pb = Download(strUrl, cbMax);
			return DecodeUtf8(pb);
		}

		public static string DecodeUtf8(byte[] pb)
		{
			if(pb == null) return string.Empty;
			int iStart = 0;
			if((pb.Length >= 3) && (pb[0] == 0xEF) && (pb[1] == 0xBB) && (pb[2] == 0xBF))
				iStart = 3;
			return Encoding.UTF8.GetString(pb, iStart, pb.Length - iStart);
		}

		// %LOCALAPPDATA%\KeePass\BetterPass\<sub>
		public static string GetCacheDirectory(string strSub)
		{
			string strBase = AppConfigSerializer.LocalAppDataDirectory;
			if(string.IsNullOrEmpty(strBase))
				strBase = Path.Combine(Environment.GetFolderPath(
					Environment.SpecialFolder.LocalApplicationData), "KeePass");

			string str = Path.Combine(strBase, "BetterPass");
			if(!string.IsNullOrEmpty(strSub)) str = Path.Combine(str, strSub);
			if(!Directory.Exists(str)) Directory.CreateDirectory(str);
			return str;
		}

		public static string GetErrorMessage(Exception ex)
		{
			if(ex == null) return "Unbekannter Fehler";
			Exception e = ex;
			while((e.InnerException != null) && string.IsNullOrEmpty(e.Message))
				e = e.InnerException;
			return e.Message;
		}
	}
}
