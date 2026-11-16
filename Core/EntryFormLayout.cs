/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Inserts rows into the "General" tab of KeePass' entry dialog.

  All geometry is derived from KeePass' own controls, so DPI scaling and
  fonts match. If a future KeePass version changes the dialog so that the
  expected controls are missing, TryCreate returns null and nothing is changed.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

using KeePass.Forms;

namespace BetterPass
{
	internal sealed class EntryFormLayout
	{
		private readonly PwEntryForm m_form;
		private TabControl m_tabMain;
		private TabPage m_tabEntry;
		private Control m_tbTitle;
		private Control m_tbUserName;
		private Control m_tbUrl;
		private Control m_lblUrl;
		private Control m_rtNotes;
		private Control m_btnRef;

		public PwEntryForm Form { get { return m_form; } }
		public TabControl TabMain { get { return m_tabMain; } }
		public TabPage TabEntry { get { return m_tabEntry; } }
		public Control UrlBox { get { return m_tbUrl; } }

		public int Pitch { get; private set; }
		public int TextBoxHeight { get; private set; }
		public int LabelX { get; private set; }
		public int LabelOffset { get; private set; }
		public int FieldX { get; private set; }
		public int RightX { get; private set; }
		public int ButtonWidth { get; private set; }
		public int ButtonHeight { get; private set; }
		public int ButtonX { get; private set; }
		public int Gap { get; private set; }

		private EntryFormLayout(PwEntryForm f)
		{
			m_form = f;
		}

		public static T FindControl<T>(Control cParent, string strName) where T : Control
		{
			if(cParent == null) return null;
			Control[] v = cParent.Controls.Find(strName, true);
			if((v == null) || (v.Length == 0)) return null;
			return (v[0] as T);
		}

		public static EntryFormLayout TryCreate(PwEntryForm f)
		{
			if(f == null) return null;

			EntryFormLayout l = new EntryFormLayout(f);
			l.m_tabMain = FindControl<TabControl>(f, "m_tabMain");
			l.m_tabEntry = FindControl<TabPage>(f, "m_tabEntry");
			l.m_tbTitle = FindControl<Control>(f, "m_tbTitle");
			l.m_tbUserName = FindControl<Control>(f, "m_tbUserName");
			l.m_tbUrl = FindControl<Control>(f, "m_tbUrl");
			l.m_lblUrl = FindControl<Control>(f, "m_lblUrl");
			l.m_rtNotes = FindControl<Control>(f, "m_rtNotes");
			l.m_btnRef = FindControl<Control>(f, "m_btnGenPw");

			if((l.m_tabMain == null) || (l.m_tabEntry == null) || (l.m_tbTitle == null) ||
				(l.m_tbUserName == null) || (l.m_tbUrl == null) || (l.m_lblUrl == null) ||
				(l.m_rtNotes == null)) return null;
			if((l.m_tbUrl.Parent != l.m_tabEntry) || (l.m_rtNotes.Parent != l.m_tabEntry))
				return null;

			l.Measure();
			return l;
		}

		private void Measure()
		{
			int nTbH = m_tbUrl.Height;
			int nPitch = m_tbUserName.Top - m_tbTitle.Top;
			if(nPitch <= nTbH) nPitch = nTbH + 7;

			this.Pitch = nPitch;
			this.TextBoxHeight = nTbH;
			this.LabelX = m_lblUrl.Left;
			this.LabelOffset = m_lblUrl.Top - m_tbUrl.Top;
			this.FieldX = m_tbUrl.Left;
			this.RightX = m_tbUrl.Right;
			this.ButtonWidth = ((m_btnRef != null) ? m_btnRef.Width : nTbH + 12);
			this.ButtonHeight = ((m_btnRef != null) ? m_btnRef.Height : nTbH + 3);
			this.ButtonX = this.RightX + 1 - this.ButtonWidth;
			this.Gap = Math.Max(4, nTbH / 3);
		}

		/// <summary>
		/// Inserts nRows rows starting at y0 (the top of the text box of
		/// the first new row). Everything at or below y0 moves down.
		/// Returns the first free TabIndex for the new controls.
		/// </summary>
		public int InsertRows(int y0, int nRows)
		{
			int nInsert = nRows * this.Pitch;
			int yThreshold = y0 - Math.Max(3, this.LabelOffset + 1);

			List<Control> lBelow = new List<Control>();
			int nTabAfter = -1;
			foreach(Control c in m_tabEntry.Controls)
			{
				if(c.Top >= yThreshold) lBelow.Add(c);
				else nTabAfter = Math.Max(nTabAfter, c.TabIndex);
			}

			// Grow the dialog, or shrink the notes box if the screen is too small
			Rectangle rcWork = Screen.FromControl(m_form).WorkingArea;
			int nOverflow = (m_form.Height + nInsert) - rcWork.Height;
			int nShrink = 0;
			if(nOverflow > 0)
				nShrink = Math.Max(0, Math.Min(nOverflow, m_rtNotes.Height - 3 * this.Pitch));
			int nGrow = nInsert - nShrink;
			int yNotesBottom = m_rtNotes.Bottom;

			m_form.SuspendLayout();
			m_tabEntry.SuspendLayout();
			try
			{
				if(nGrow > 0)
				{
					int yTabBottom = m_tabMain.Bottom;
					List<Control> lFormBelow = new List<Control>();
					foreach(Control c in m_form.Controls)
					{
						if(c == m_tabMain) continue;
						if(c.Dock != DockStyle.None) continue;
						if(c.Top >= yTabBottom - 1) lFormBelow.Add(c);
					}

					m_form.ClientSize = new Size(m_form.ClientSize.Width,
						m_form.ClientSize.Height + nGrow);
					m_tabMain.Height += nGrow;
					foreach(Control c in lFormBelow) c.Top += nGrow;

					int yNew = m_form.Top - nGrow / 2;
					if(yNew + m_form.Height > rcWork.Bottom) yNew = rcWork.Bottom - m_form.Height;
					if(yNew < rcWork.Top) yNew = rcWork.Top;
					m_form.Top = yNew;
				}

				foreach(Control c in lBelow)
				{
					if(c == m_rtNotes)
					{
						c.Top += nInsert;
						c.Height -= nShrink;
					}
					else if(c.Top >= yNotesBottom) c.Top += nGrow;
					else c.Top += nInsert;
				}

				foreach(Control c in lBelow)
				{
					if(c.TabIndex > nTabAfter) c.TabIndex += 20;
				}
			}
			finally
			{
				m_tabEntry.ResumeLayout(true);
				m_form.ResumeLayout(true);
			}

			return nTabAfter + 1;
		}

		public Label AddLabel(string strText, int x, int yRow)
		{
			Label lbl = new Label();
			lbl.AutoSize = true;
			lbl.Text = strText;
			lbl.Location = new Point(x, yRow + this.LabelOffset);
			m_tabEntry.Controls.Add(lbl);
			return lbl;
		}

		public void Add(Control c)
		{
			m_tabEntry.Controls.Add(c);
		}
	}
}
