/*
  BetterPass - KeePass plugin
  Copyright (C) 2026 Simon Müller
  SPDX-License-Identifier: GPL-3.0-or-later
  Small modal progress dialog that runs work on a background thread.
*/

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace BetterPass
{
	internal sealed class ProgressContext
	{
		private readonly ProgressForm m_form;
		private volatile bool m_bCancel = false;

		internal ProgressContext(ProgressForm f) { m_form = f; }

		public bool CancelRequested { get { return m_bCancel; } }

		internal void RequestCancel() { m_bCancel = true; }

		public void Report(int nPercent, string strText)
		{
			m_form.PostProgress(nPercent, strText);
		}
	}

	internal delegate void ProgressWork(ProgressContext ctx);

	internal sealed class ProgressForm : Form
	{
		private readonly Label m_lbl;
		private readonly ProgressBar m_pb;
		private readonly Button m_btnCancel;
		private readonly ProgressContext m_ctx;
		private readonly ProgressWork m_work;
		private Exception m_exError = null;
		private bool m_bDone = false;

		private ProgressForm(string strTitle, ProgressWork work)
		{
			m_work = work;
			m_ctx = new ProgressContext(this);

			this.Text = strTitle;
			this.FormBorderStyle = FormBorderStyle.FixedDialog;
			this.MaximizeBox = false;
			this.MinimizeBox = false;
			this.ShowInTaskbar = false;
			this.StartPosition = FormStartPosition.CenterParent;
			this.AutoScaleMode = AutoScaleMode.Font;
			this.Font = SystemFonts.MessageBoxFont;
			this.ClientSize = new Size(420, 116);

			m_lbl = new Label();
			m_lbl.AutoSize = false;
			m_lbl.AutoEllipsis = true;
			m_lbl.Bounds = new Rectangle(12, 14, 396, 20);
			m_lbl.Text = "Bitte warten \u2026";
			this.Controls.Add(m_lbl);

			m_pb = new ProgressBar();
			m_pb.Bounds = new Rectangle(12, 40, 396, 20);
			m_pb.Minimum = 0;
			m_pb.Maximum = 100;
			this.Controls.Add(m_pb);

			m_btnCancel = new Button();
			m_btnCancel.Text = "Abbrechen";
			m_btnCancel.Bounds = new Rectangle(318, 76, 90, 26);
			m_btnCancel.UseVisualStyleBackColor = true;
			m_btnCancel.Click += delegate(object sender, EventArgs e)
			{
				m_ctx.RequestCancel();
				m_btnCancel.Enabled = false;
				m_lbl.Text = "Wird abgebrochen \u2026";
			};
			this.Controls.Add(m_btnCancel);
			this.CancelButton = m_btnCancel;
		}

		/// <summary>
		/// Runs the work and shows the dialog until it has finished.
		/// Returns false if the user cancelled. Exceptions thrown by the work
		/// are returned in exError.
		/// </summary>
		public static bool Run(IWin32Window owner, string strTitle, ProgressWork work,
			out Exception exError)
		{
			using(ProgressForm f = new ProgressForm(strTitle, work))
			{
				ThemeManager.ApplyToForm(f);
				f.ShowDialog(owner);
				exError = f.m_exError;
				return !f.m_ctx.CancelRequested;
			}
		}

		protected override void OnShown(EventArgs e)
		{
			base.OnShown(e);

			Thread th = new Thread(delegate()
			{
				try { m_work(m_ctx); }
				catch(Exception ex) { m_exError = ex; }

				try
				{
					this.BeginInvoke(new MethodInvoker(delegate()
					{
						m_bDone = true;
						this.Close();
					}));
				}
				catch(Exception) { }
			});
			th.IsBackground = true;
			th.Start();
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			if(!m_bDone)
			{
				// Wait for the worker; it stops at the next cancellation check
				m_ctx.RequestCancel();
				m_btnCancel.Enabled = false;
				e.Cancel = true;
				return;
			}
			base.OnFormClosing(e);
		}

		internal void PostProgress(int nPercent, string strText)
		{
			try
			{
				if(this.IsDisposed || !this.IsHandleCreated) return;
				this.BeginInvoke(new MethodInvoker(delegate()
				{
					if(nPercent >= 0) m_pb.Value = Math.Max(0, Math.Min(100, nPercent));
					if((strText != null) && !m_ctx.CancelRequested) m_lbl.Text = strText;
				}));
			}
			catch(Exception) { }
		}
	}
}
