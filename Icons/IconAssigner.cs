/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Applies theSVG icons to entries as KeePass custom icons.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using KeePass.Forms;

using KeePassLib;
using KeePassLib.Utility;

namespace BetterPass
{
	internal static class IconAssigner
	{
		private const float IconPadding = 0.04f;

		public static byte[] RenderIconPng(SvgIconInfo ii, string strVariant, int nSize)
		{
			string strSvg = TheSvgCatalog.GetSvg(ii, strVariant);
			return SvgRenderer.RenderPng(strSvg, nSize, TheSvgCatalog.GetBrandColor(ii), IconPadding);
		}

		public static Bitmap RenderPreview(SvgIconInfo ii, string strVariant, int nSize)
		{
			string strSvg = TheSvgCatalog.GetSvg(ii, strVariant);
			return SvgRenderer.Render(strSvg, nSize, TheSvgCatalog.GetBrandColor(ii), IconPadding);
		}

		/// <summary>
		/// Adds the PNG as custom icon (or reuses an identical one).
		/// </summary>
		public static PwUuid AddOrGetCustomIcon(PwDatabase pd, byte[] pbPng, string strName)
		{
			int iExisting = pd.GetCustomIconIndex(pbPng);
			if(iExisting >= 0) return pd.CustomIcons[iExisting].Uuid;

			PwCustomIcon ci = new PwCustomIcon(new PwUuid(true), pbPng);
			if(!string.IsNullOrEmpty(strName)) ci.Name = strName;
			pd.CustomIcons.Add(ci);
			pd.UINeedsIconUpdate = true;
			pd.Modified = true;
			return ci.Uuid;
		}

		public static int AssignIcon(PwDatabase pd, IList<PwEntry> lEntries, PwUuid puIcon)
		{
			int n = 0;
			foreach(PwEntry pe in lEntries)
			{
				if(pe.CustomIconUuid.Equals(puIcon)) continue;
				pe.CreateBackup(pd);
				pe.CustomIconUuid = puIcon;
				pe.Touch(true, false);
				++n;
			}
			if(n > 0) pd.Modified = true;
			return n;
		}

		private static bool CheckDatabase(MainForm mf, out PwDatabase pd)
		{
			pd = ((mf != null) ? mf.ActiveDatabase : null);
			if((pd == null) || !pd.IsOpen)
			{
				MessageService.ShowInfo("Bitte zuerst eine Datenbank \u00f6ffnen.");
				return false;
			}
			return true;
		}

		// ===================== Commands =====================

		public static void ShowPicker(MainForm mf)
		{
			PwDatabase pd;
			if(!CheckDatabase(mf, out pd)) return;

			PwEntry[] vEntries = mf.GetSelectedEntries();
			if((vEntries == null) || (vEntries.Length == 0))
			{
				MessageService.ShowInfo("Bitte zuerst einen oder mehrere Eintr\u00e4ge ausw\u00e4hlen.");
				return;
			}

			string strSuggestion = GetSuggestion(vEntries[0]);
			SvgIconInfo ii;
			string strVariant;
			using(IconPickerForm dlg = new IconPickerForm(strSuggestion, vEntries.Length))
			{
				if(dlg.ShowDialog(mf) != DialogResult.OK) return;
				ii = dlg.SelectedIcon;
				strVariant = dlg.SelectedVariant;
			}
			if(ii == null) return;

			byte[] pbPng = null;
			Exception exError;
			bool bOk = ProgressForm.Run(mf, "BetterPass \u2013 Icon", delegate(ProgressContext ctx)
			{
				ctx.Report(-1, "Lade Icon \"" + ii.Title + "\" \u2026");
				pbPng = RenderIconPng(ii, strVariant, BpConfig.IconSize);
			}, out exError);

			if(exError != null)
			{
				MessageService.ShowWarning("Das Icon konnte nicht erstellt werden:", Net.GetErrorMessage(exError));
				return;
			}
			if(!bOk || (pbPng == null)) return;

			PwUuid pu = AddOrGetCustomIcon(pd, pbPng, "theSVG: " + ii.Title);
			int n = AssignIcon(pd, vEntries, pu);
			mf.UpdateUI(false, null, false, null, true, null, true);
			mf.SetStatusEx("BetterPass: Icon \"" + ii.Title + "\" f\u00fcr " + n.ToString() +
				" Eintr\u00e4ge gesetzt.");
		}

		public static void AutoAssign(MainForm mf)
		{
			PwDatabase pd;
			if(!CheckDatabase(mf, out pd)) return;

			List<PwEntry> lEntries = new List<PwEntry>();
			PwEntry[] vSel = mf.GetSelectedEntries();
			if((vSel != null) && (vSel.Length > 1)) lEntries.AddRange(vSel);
			else
			{
				DialogResult dr = MessageBox.Show(mf, "Passende Icons f\u00fcr alle Eintr\u00e4ge " +
					"ohne eigenes Icon suchen?\r\n\r\n(Um nur bestimmte Eintr\u00e4ge zu bearbeiten, " +
					"diese vorher markieren.)", "BetterPass", MessageBoxButtons.OKCancel,
					MessageBoxIcon.Question);
				if(dr != DialogResult.OK) return;

				PwGroup pgRecycle = null;
				if(pd.RecycleBinEnabled && !pd.RecycleBinUuid.Equals(PwUuid.Zero))
					pgRecycle = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);
				foreach(PwEntry pe in pd.RootGroup.GetEntries(true))
				{
					if((pgRecycle != null) && pe.IsContainedIn(pgRecycle)) continue;
					if(!pe.CustomIconUuid.Equals(PwUuid.Zero)) continue;
					lEntries.Add(pe);
				}
			}
			if(lEntries.Count == 0)
			{
				MessageService.ShowInfo("Keine passenden Eintr\u00e4ge gefunden.");
				return;
			}

			// 1. Match (needs the catalog)
			Dictionary<PwEntry, SvgIconInfo> dMatches = new Dictionary<PwEntry, SvgIconInfo>();
			Exception exError;
			bool bOk = ProgressForm.Run(mf, "BetterPass \u2013 Icons", delegate(ProgressContext ctx)
			{
				ctx.Report(-1, "Lade Icon-Katalog von thesvg.org \u2026");
				List<SvgIconInfo> lIcons = TheSvgCatalog.Load(false);
				for(int i = 0; i < lEntries.Count; ++i)
				{
					if(ctx.CancelRequested) return;
					if((i % 20) == 0)
						ctx.Report((i * 100) / lEntries.Count, "Suche passende Icons \u2026");
					SvgIconInfo ii = TheSvgCatalog.FindBestMatch(lIcons, lEntries[i]);
					if(ii != null) lock(dMatches) { dMatches[lEntries[i]] = ii; }
				}
			}, out exError);

			if(exError != null)
			{
				MessageService.ShowWarning(Net.GetErrorMessage(exError));
				return;
			}
			if(!bOk) return;
			if(dMatches.Count == 0)
			{
				MessageService.ShowInfo("F\u00fcr keinen der " + lEntries.Count.ToString() +
					" Eintr\u00e4ge wurde ein passendes Icon gefunden.");
				return;
			}

			// 2. Confirm
			StringBuilder sb = new StringBuilder();
			sb.Append("F\u00fcr ");
			sb.Append(dMatches.Count.ToString());
			sb.Append(" von ");
			sb.Append(lEntries.Count.ToString());
			sb.Append(" Eintr\u00e4gen wurde ein passendes Icon gefunden:\r\n\r\n");
			int nShown = 0;
			foreach(KeyValuePair<PwEntry, SvgIconInfo> kvp in dMatches)
			{
				if(nShown == 12) { sb.Append("\u2026\r\n"); break; }
				string strTitle = kvp.Key.Strings.ReadSafe(PwDefs.TitleField);
				if(strTitle.Length == 0) strTitle = "(ohne Titel)";
				sb.Append("\u2022 ");
				sb.Append(strTitle);
				sb.Append("  \u2192  ");
				sb.Append(kvp.Value.Title);
				sb.Append("\r\n");
				++nShown;
			}
			sb.Append("\r\nIcons jetzt zuweisen?");
			if(MessageBox.Show(mf, sb.ToString(), "BetterPass", MessageBoxButtons.OKCancel,
				MessageBoxIcon.Question) != DialogResult.OK) return;

			// 3. Download and render (once per icon)
			string strVariant = BpConfig.IconVariant;
			int nSize = BpConfig.IconSize;
			Dictionary<string, byte[]> dPng = new Dictionary<string, byte[]>(StringComparer.Ordinal);
			List<string> lFailed = new List<string>();
			bOk = ProgressForm.Run(mf, "BetterPass \u2013 Icons", delegate(ProgressContext ctx)
			{
				List<SvgIconInfo> lUnique = new List<SvgIconInfo>();
				foreach(SvgIconInfo ii in dMatches.Values)
				{
					if(!lUnique.Contains(ii)) lUnique.Add(ii);
				}
				for(int i = 0; i < lUnique.Count; ++i)
				{
					if(ctx.CancelRequested) return;
					SvgIconInfo ii = lUnique[i];
					ctx.Report((i * 100) / lUnique.Count, "Lade Icon \"" + ii.Title + "\" \u2026");
					try
					{
						byte[] pb = RenderIconPng(ii, strVariant, nSize);
						lock(dPng) { dPng[ii.Slug] = pb; }
					}
					catch(Exception) { lock(lFailed) { lFailed.Add(ii.Title); } }
				}
			}, out exError);

			// 4. Assign whatever was rendered (also after cancel)
			int nAssigned = 0;
			Dictionary<string, PwUuid> dUuids = new Dictionary<string, PwUuid>(StringComparer.Ordinal);
			foreach(KeyValuePair<PwEntry, SvgIconInfo> kvp in dMatches)
			{
				byte[] pb;
				if(!dPng.TryGetValue(kvp.Value.Slug, out pb)) continue;

				PwUuid pu;
				if(!dUuids.TryGetValue(kvp.Value.Slug, out pu))
				{
					pu = AddOrGetCustomIcon(pd, pb, "theSVG: " + kvp.Value.Title);
					dUuids[kvp.Value.Slug] = pu;
				}
				nAssigned += AssignIcon(pd, new PwEntry[] { kvp.Key }, pu);
			}

			if(nAssigned > 0) mf.UpdateUI(false, null, false, null, true, null, true);

			string strDone = nAssigned.ToString() + " Eintr\u00e4gen wurde ein Icon zugewiesen.";
			if(lFailed.Count > 0)
				strDone += "\r\n\r\nNicht geladen werden konnten: " + string.Join(", ", lFailed.ToArray());
			if(exError != null) strDone += "\r\n\r\n" + Net.GetErrorMessage(exError);
			MessageService.ShowInfo(strDone);
		}

		private static string GetSuggestion(PwEntry pe)
		{
			string strUrl = pe.Strings.ReadSafe(PwDefs.UrlField);
			string strDomain = TheSvgCatalog.GetRegistrableDomain(strUrl);
			if(strDomain.Length > 0)
			{
				int iDot = strDomain.IndexOf('.');
				return ((iDot > 0) ? strDomain.Substring(0, iDot) : strDomain);
			}
			return pe.Strings.ReadSafe(PwDefs.TitleField);
		}
	}
}
