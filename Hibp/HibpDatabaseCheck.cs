/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Checks all passwords of a database against Have I Been Pwned and shows
  the affected entries in KeePass' entry list.
*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using KeePass.Forms;

using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace BetterPass
{
	internal static class HibpDatabaseCheck
	{
		private static int g_nRunning = 0;

		private sealed class Job
		{
			public PwDatabase Database;
			public readonly Dictionary<string, List<PwEntry>> ByHash =
				new Dictionary<string, List<PwEntry>>(StringComparer.Ordinal);
			public readonly Dictionary<string, int> Counts =
				new Dictionary<string, int>(StringComparer.Ordinal);
			public int Entries = 0;
			public int Skipped = 0;
			public string Error = null;
		}

		/// <summary>
		/// Interactive check with progress dialog (menu command).
		/// </summary>
		public static void RunInteractive(MainForm mf)
		{
			PwDatabase pd = ((mf != null) ? mf.ActiveDatabase : null);
			if((pd == null) || !pd.IsOpen)
			{
				MessageService.ShowInfo("Bitte zuerst eine Datenbank \u00f6ffnen.");
				return;
			}
			if(Interlocked.CompareExchange(ref g_nRunning, 1, 0) != 0)
			{
				MessageService.ShowInfo("Eine Pr\u00fcfung l\u00e4uft bereits.");
				return;
			}

			try
			{
				Job job = Prepare(pd);
				if(job.ByHash.Count == 0)
				{
					MessageService.ShowInfo("Die Datenbank enth\u00e4lt keine Passw\u00f6rter, die gepr\u00fcft werden k\u00f6nnen.");
					return;
				}

				Exception exError;
				bool bCompleted = ProgressForm.Run(mf, "BetterPass \u2013 Have I Been Pwned",
					delegate(ProgressContext ctx) { Execute(job, ctx); }, out exError);

				if(exError != null) job.Error = Net.GetErrorMessage(exError);
				if(!bCompleted && (job.Error == null)) return; // Cancelled

				ShowResults(mf, job, true);
			}
			finally { Interlocked.Exchange(ref g_nRunning, 0); }
		}

		/// <summary>
		/// Silent background check (after opening a database).
		/// Only shows something if compromised passwords were found.
		/// </summary>
		public static void RunInBackground(MainForm mf, PwDatabase pd)
		{
			if((mf == null) || (pd == null) || !pd.IsOpen) return;
			if(Interlocked.CompareExchange(ref g_nRunning, 1, 0) != 0) return;

			Job job;
			try { job = Prepare(pd); }
			catch(Exception) { Interlocked.Exchange(ref g_nRunning, 0); return; }

			if(job.ByHash.Count == 0) { Interlocked.Exchange(ref g_nRunning, 0); return; }

			ThreadPool.QueueUserWorkItem(delegate(object o)
			{
				try { Execute(job, null); }
				catch(Exception ex) { job.Error = Net.GetErrorMessage(ex); }

				try
				{
					mf.BeginInvoke(new MethodInvoker(delegate()
					{
						try
						{
							if(pd.IsOpen) ShowResults(mf, job, false);
						}
						finally { Interlocked.Exchange(ref g_nRunning, 0); }
					}));
				}
				catch(Exception) { Interlocked.Exchange(ref g_nRunning, 0); }
			});
		}

		// Runs on the UI thread: reads the passwords and hashes them
		private static Job Prepare(PwDatabase pd)
		{
			Job job = new Job();
			job.Database = pd;

			PwGroup pgRecycle = null;
			if(pd.RecycleBinEnabled && !pd.RecycleBinUuid.Equals(PwUuid.Zero))
				pgRecycle = pd.RootGroup.FindGroup(pd.RecycleBinUuid, true);

			PwObjectList<PwEntry> l = pd.RootGroup.GetEntries(true);
			foreach(PwEntry pe in l)
			{
				if((pgRecycle != null) && pe.IsContainedIn(pgRecycle)) continue;

				ProtectedString ps = pe.Strings.GetSafe(PwDefs.PasswordField);
				if(ps.IsEmpty) continue;

				++job.Entries;

				byte[] pb = ps.ReadUtf8();
				try
				{
					// Field references ({REF:...}) point to other entries
					if(StartsWithRef(pb)) { ++job.Skipped; continue; }

					string strHash = HibpClient.ComputeSha1Hex(pb);
					List<PwEntry> lEntries;
					if(!job.ByHash.TryGetValue(strHash, out lEntries))
					{
						lEntries = new List<PwEntry>();
						job.ByHash[strHash] = lEntries;
					}
					lEntries.Add(pe);
				}
				finally { Array.Clear(pb, 0, pb.Length); }
			}

			return job;
		}

		internal static bool StartsWithRef(byte[] pb)
		{
			byte[] pbRef = Encoding.ASCII.GetBytes("{REF:");
			if(pb.Length < pbRef.Length) return false;
			for(int i = 0; i < pbRef.Length; ++i)
			{
				byte b = pb[i];
				if((b >= (byte)'a') && (b <= (byte)'z')) b = (byte)(b - 32);
				if(b != pbRef[i]) return false;
			}
			return true;
		}

		// Runs on a worker thread: only network, no database access
		private static void Execute(Job job, ProgressContext ctx)
		{
			int nTotal = job.ByHash.Count, i = 0;
			foreach(string strHash in job.ByHash.Keys)
			{
				if((ctx != null) && ctx.CancelRequested) return;
				if(ctx != null)
					ctx.Report((i * 100) / nTotal, "Pr\u00fcfe Passwort " + (i + 1).ToString() +
						" von " + nTotal.ToString() + " \u2026");

				int n = HibpClient.GetBreachCount(strHash);
				lock(job.Counts) { job.Counts[strHash] = n; }
				++i;
			}
			if(ctx != null) ctx.Report(100, "Fertig");
		}

		// Runs on the UI thread
		private static void ShowResults(MainForm mf, Job job, bool bInteractive)
		{
			PwDatabase pd = job.Database;
			List<PwEntry> lPwned = new List<PwEntry>();
			Dictionary<PwEntry, int> dCount = new Dictionary<PwEntry, int>();
			List<PwEntry> lClean = new List<PwEntry>();

			foreach(KeyValuePair<string, List<PwEntry>> kvp in job.ByHash)
			{
				int n;
				if(!job.Counts.TryGetValue(kvp.Key, out n)) continue; // Not checked
				foreach(PwEntry pe in kvp.Value)
				{
					if(n > 0) { lPwned.Add(pe); dCount[pe] = n; }
					else lClean.Add(pe);
				}
			}

			bool bModified = false;
			if(BpConfig.HibpTagEntries)
			{
				foreach(PwEntry pe in lPwned)
				{
					if(pe.HasTag(BpConfig.HibpTag)) continue;
					pe.CreateBackup(pd);
					pe.AddTag(BpConfig.HibpTag);
					pe.Touch(true, false);
					bModified = true;
				}
				foreach(PwEntry pe in lClean)
				{
					if(!pe.HasTag(BpConfig.HibpTag)) continue;
					pe.CreateBackup(pd);
					pe.RemoveTag(BpConfig.HibpTag);
					pe.Touch(true, false);
					bModified = true;
				}
			}

			if(bModified) pd.Modified = true;
			bool bActive = object.ReferenceEquals(mf.ActiveDatabase, pd);
			bool bSetMod = (bModified && bActive);

			int nChecked = job.Counts.Count;

			if((job.Error != null) && (lPwned.Count == 0))
			{
				if(bInteractive)
					MessageService.ShowWarning("Die Pr\u00fcfung konnte nicht abgeschlossen werden:",
						job.Error);
				else mf.SetStatusEx("BetterPass: HIBP-Pr\u00fcfung fehlgeschlagen (" + job.Error + ")");
				if(bSetMod) mf.UpdateUI(false, null, false, null, true, null, true);
				return;
			}

			if(lPwned.Count == 0)
			{
				if(bSetMod) mf.UpdateUI(false, null, false, null, true, null, true);
				string strMsg = "Keines der gepr\u00fcften Passw\u00f6rter (" + nChecked.ToString() +
					" verschiedene) wurde in bekannten Datenlecks gefunden.";
				if(job.Skipped > 0)
					strMsg += "\r\n" + job.Skipped.ToString() + " Feldreferenzen wurden \u00fcbersprungen.";
				if(bInteractive) MessageService.ShowInfo(strMsg);
				else mf.SetStatusEx("BetterPass: Keine kompromittierten Passw\u00f6rter gefunden.");
				return;
			}

			lPwned.Sort(delegate(PwEntry a, PwEntry b) { return dCount[b].CompareTo(dCount[a]); });

			PwGroup pg = new PwGroup(true, true, "(BetterPass: Kompromittierte Passw\u00f6rter)",
				PwIcon.Warning);
			pg.IsVirtual = true;
			foreach(PwEntry pe in lPwned) pg.AddEntry(pe, false);

			if(!bInteractive && !bActive)
			{
				// User switched to another database meanwhile; don't show
				// this database's entries in the other one's list
				mf.SetStatusEx("BetterPass: " + lPwned.Count.ToString() +
					" Eintr\u00e4ge mit kompromittiertem Passwort (andere Datenbank).");
				return;
			}

			if(!bInteractive)
			{
				string strQ = lPwned.Count.ToString() + " Eintr\u00e4ge haben ein Passwort, das in " +
					"bekannten Datenlecks aufgetaucht ist (Have I Been Pwned).\r\n\r\nJetzt anzeigen?";
				DialogResult dr = MessageBox.Show(mf, strQ, "BetterPass",
					MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
				if(dr != DialogResult.Yes)
				{
					if(bSetMod) mf.UpdateUI(false, null, false, null, true, null, true);
					mf.SetStatusEx("BetterPass: " + lPwned.Count.ToString() +
						" Eintr\u00e4ge mit kompromittiertem Passwort.");
					return;
				}
			}

			mf.UpdateUI(false, null, false, null, true, pg, bSetMod);
			mf.SetStatusEx("BetterPass: " + lPwned.Count.ToString() +
				" Eintr\u00e4ge mit kompromittiertem Passwort gefunden.");

			if(bInteractive)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append(lPwned.Count.ToString());
				sb.Append(" Eintr\u00e4ge haben ein Passwort, das in bekannten Datenlecks aufgetaucht ist.");
				sb.Append("\r\nSie werden jetzt in der Eintragsliste angezeigt.");
				if(job.Error != null)
				{
					sb.Append("\r\n\r\nHinweis: Nicht alle Passw\u00f6rter konnten gepr\u00fcft werden (");
					sb.Append(job.Error);
					sb.Append(").");
				}
				if(BpConfig.HibpTagEntries)
				{
					sb.Append("\r\nDie Eintr\u00e4ge wurden mit dem Tag \"");
					sb.Append(BpConfig.HibpTag);
					sb.Append("\" markiert.");
				}
				MessageService.ShowWarning(sb.ToString());
			}
		}
	}
}
