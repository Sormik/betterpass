/*
  BetterPass - KeePass plugin
  Modern icons for KeePass: monochrome Fluent line icons from Windows' own
  icon font (Segoe Fluent Icons on Windows 11, Segoe MDL2 Assets on
  Windows 10), drawn in the theme's text color. Nothing is bundled; without
  the font, KeePass keeps its original icons.

  Covered: main menu, toolbar, context and tray menus (by KeePass' item
  names; copies of an item are recognized by their original image), dialog
  buttons, the standard entry/group icons and the dialog banners.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace BetterPass
{
	internal static class IconSet
	{
		// ===================== Glyphs =====================

		private const char Add = '', Remove = '', Delete = '', Edit = '';
		private const char Save = '', SaveAs = '', SaveCopy = '';
		private const char Folder = '', FolderOpen = '', OpenFile = '';
		private const char Page = '', Document = '', NewFolder = '', MoveFolder = '';
		private const char Close = '', Lock = '', Unlock = '', Print = '';
		private const char Import = '', Export = '', Sync = '', Settings = '';
		private const char Key = '', Power = '', Search = '', Globe = '';
		private const char World = '', Link = '', Copy = '', Paste = '';
		private const char Contact = '', People = '', Info = '', Help = '';
		private const char Heart = '', Pin = '', List = '', Preview = '';
		private const char Puzzle = '', Bolt = '', Tag = '', Palette = '';
		private const char Up = '', Down = '', ChevronUp = '', ChevronDown = '';
		private const char Sort = '', Keyboard = '', Filter = '', Recent = '';
		private const char History = '', Attach = '', More = '', SelectAll = '';
		private const char Back = '', Switch = '', Repair = '', Calendar = '';
		private const char Eye = '', Warning = '', Mail = '', Camera = '';
		private const char Bluetooth = '', Scan = '', Star = '', Disc = '';
		private const char Monitor = '', Laptop = '', Pc = '', Server = '';
		private const char Drive = '', Console = '', Play = '', Apps = '';
		private const char Package = '', Bank = '', Clock = '', Note = '';
		private const char Memory = '', Flag = '', Check = '', Photo = '';
		private const char Book = '', Library = '', ContactInfo = '', Shield = '';
		private const char Home = '', Phone = '', Certificate = '', Marker = '';
		private const char Component = '', Rename = '', Expand = '', Collapse = '';
		private const char Wallet = '', Admin = '', Layout = '', Tiles = '';
		private const char Calculator = '';
		private const char Password = ''; // Windows 11 only, see MapGlyph

		// Standard entry/group icons, in the order of KeePassLib.PwIcon
		private static readonly char[] g_vPwIcons = new char[] {
			Key, Globe, Warning, Server, Folder, People, Component, Note, World, ContactInfo,
			Document, Camera, Bluetooth, Password, Bolt, Scan, World, Disc, Monitor, Mail,
			Settings, Paste, Page, Monitor, Bolt, Mail, Save, Drive, Help, Console,
			Console, Print, Apps, Play, Settings, Pc, Package, Bank, Drive, Clock,
			Search, Flag, Memory, Delete, Note, Close, Info, Package, Folder, FolderOpen,
			Package, Unlock, Lock, Check, Edit, Photo, Book, List, Contact, Repair,
			Home, Star, Laptop, Marker, Phone, Library, Wallet, Certificate, Phone };
		private const int PwIconWarning = 2, PwIconEnergyCareful = 24, PwIconExpired = 45;

		private static readonly Dictionary<string, char> g_dNames = BuildNameMap();

		private static Dictionary<string, char> BuildNameMap()
		{
			Dictionary<string, char> d = new Dictionary<string, char>();
			Action<char, string> m = delegate(char ch, string strNames)
			{
				foreach(string str in strNames.Split(' ')) { if(str.Length > 0) d[str] = ch; }
			};

			// File
			m(Document, "m_menuFileNew m_tbNewDatabase");
			m(FolderOpen, "m_menuFileOpen m_tbOpenDatabase");
			m(OpenFile, "m_menuFileOpenLocal m_menuFileSyncFile");
			m(Globe, "m_menuFileOpenUrl m_menuFileSaveAsUrl m_menuFileSyncUrl m_menuHelpWebsite " +
				"m_menuChangeLanguage m_menuEntryUrl m_menuEntryOpenUrl m_ctxEntryUrl m_tbOpenUrl " +
				"m_tbOpenUrlDefault");
			m(History, "m_menuFileRecent m_menuFileSyncRecent m_menuFindLastMod m_menuFindHistory");
			m(Close, "m_menuFileClose m_tbCloseTab m_ctxTrayCancel");
			m(Save, "m_menuFileSave m_tbSaveDatabase m_btnSaveKeyFile m_btnProfileAdd");
			m(SaveCopy, "m_tbSaveAll m_menuFileSaveAsCopy");
			m(SaveAs, "m_menuFileSaveAs m_menuFileSaveAsLocal");
			m(Settings, "m_menuFileDbSettings m_menuToolsOptions m_menuToolsOptionsEnf m_ctxTrayOptions " +
				"m_menuEntryOtpGenSettings m_btnCustomOpt");
			m(Key, "m_menuFileChangeMasterKey m_menuToolsRecreateKeyFile m_menuEntryCopyPassword " +
				"m_tbCopyPassword");
			m(Print, "m_menuFilePrint m_menuFilePrintDatabase m_menuFilePrintEmSheet " +
				"m_menuFilePrintKeyFile m_menuEntryPrint m_menuGroupPrint");
			m(Import, "m_menuFileImport");
			m(Export, "m_menuFileExport m_menuEntryExport m_menuGroupExport");
			m(Sync, "m_menuFileSync m_menuHelpCheckForUpdates");
			m(Search, "m_menuFileFind m_menuFileFindInFolder m_menuFindInDatabase m_tbFind m_ctxGroupFind");
			m(Lock, "m_menuFileLock m_tbLockWorkspace m_ctxTrayLock");
			m(Power, "m_menuFileExit m_ctxTrayFileExit");

			// Group and entry
			m(NewFolder, "m_menuGroupAdd");
			m(Edit, "m_menuGroupEdit m_menuEntryEdit");
			m(Rename, "m_menuEntryEditQuick m_ctxEntryEditQuick m_menuToolsDbXmlRep");
			m(Copy, "m_menuGroupDuplicate m_menuEntryDuplicate m_menuGroupClipCopy m_menuGroupClipCopyPlain " +
				"m_menuEntryClipCopy m_menuEntryClipCopyPlain m_menuFindDupPasswords");
			m(Delete, "m_menuGroupDelete m_menuGroupEmptyRB m_menuEntryDelete m_menuToolsDbDelDupEntries " +
				"m_menuToolsDbDelEmptyGroups m_menuToolsDbDelUnusedIcons m_btnProfileRemove " +
				"m_btnProfileDelete m_btnCustomDelete m_btnCDDel");
			m(Sort, "m_menuGroupRearrange m_menuEntryRearrange m_ctxEntryRearrange m_menuGroupSort " +
				"m_menuGroupSortRec m_menuViewSortBy");
			m(Up, "m_menuGroupMoveToTop m_menuEntryMoveToTop m_btnAutoTypeUp");
			m(ChevronUp, "m_menuGroupMoveOneUp m_menuEntryMoveOneUp");
			m(ChevronDown, "m_menuGroupMoveOneDown m_menuEntryMoveOneDown");
			m(Down, "m_menuGroupMoveToBottom m_menuEntryMoveToBottom m_btnAutoTypeDown");
			m(Expand, "m_menuGroupExpand");
			m(Collapse, "m_menuGroupCollapse");
			m(Switch, "m_menuGroupDX m_menuEntryDX m_menuEntryCompare m_menuEntryCompare1 " +
				"m_menuEntryCompare2 m_menuEntryCompareMark m_menuFindSimPasswordsP m_menuFindSimPasswordsC");
			m(Paste, "m_menuGroupClipPaste m_menuEntryClipPaste");
			m(Back, "m_menuGroupMoveToPreviousParent m_menuEntryMoveToPreviousParent");
			m(Add, "m_menuEntryAdd m_tbAddEntry m_tbAddEntryDefault m_btnCustomAdd");
			m(SelectAll, "m_menuEntrySelectAll");
			m(Contact, "m_menuEntryCopyUserName m_tbCopyUserName");
			m(Link, "m_menuEntryCopyUrl m_tbCopyUrl");
			m(More, "m_menuEntryOtherData m_ctxEntryOtherData m_btnCustomMore m_btnStrMore " +
				"m_btnAutoTypeMore m_btnHistoryMore m_btnMore");
			m(Attach, "m_menuEntryAttachments m_ctxEntryAttachments");
			m(Keyboard, "m_menuEntryPerformAutoType m_tbAutoType m_menuEntryAutoTypeAdv " +
				"m_ctxEntryAutoTypeAdv m_tsmiAutoTypeHotpMenu m_tsmiAutoTypeHotpCtx " +
				"m_tsmiAutoTypeTotpMenu m_tsmiAutoTypeTotpCtx");
			m(Palette, "m_menuEntryColor");
			m(Photo, "m_menuEntryIcon");
			m(Tag, "m_menuEntryTagAdd m_menuEntryTagRemove m_menuEntryTagNew m_ctxEntryTagAdd " +
				"m_ctxEntryTagRemove m_menuFindTag m_btnTags");
			m(MoveFolder, "m_menuEntryMoveToGroup m_ctxEntryMoveToGroup");
			m(Recent, "m_menuEntryExpiresNow m_menuFindExp m_menuFindExpIn m_menuFindExp1 m_menuFindExp2 " +
				"m_menuFindExp3 m_menuFindExp7 m_menuFindExp14 m_menuFindExp30 m_menuFindExp60 " +
				"m_menuFindExpInF m_tbViewsShowExpired");
			m(Calendar, "m_menuEntryExpiresNever m_btnStandardExpires");

			// Find, view, tools, help
			m(List, "m_menuFindAll m_tbViewsShowAll m_menuViewConfigColumns m_menuViewEntryListGrouping " +
				"m_menuViewTanOptions m_menuViewTanSimpleList m_menuViewTanIndices m_menuToolsGeneratePwList");
			m(Shield, "m_menuFindPwQuality m_btnProfileEnforce m_cbQualityCheck");
			m(Package, "m_menuFindLarge");
			m(Folder, "m_menuFindInGroup m_menuFindParentGroup m_menuViewShowEntriesOfSubGroups");
			m(Filter, "m_menuFindProfiles m_ctxGroupFindProfiles m_tbEntryViewsDropDown");
			m(Preview, "m_menuViewShowDetailsView");
			m(Pin, "m_menuViewAlwaysOnTop");
			m(Layout, "m_menuViewWindowLayout m_menuViewWindowsStacked");
			m(Tiles, "m_menuViewWindowsSideBySide");
			m(Password, "m_menuToolsPwGenerator m_ctxTrayGenPw m_btnGenPw");
			m(Calculator, "m_menuToolsTanWizard");
			m(Repair, "m_menuToolsDb m_menuToolsDbMaintenance m_btnTools");
			m(Admin, "m_menuToolsAdv");
			m(Bolt, "m_menuToolsTriggers");
			m(Puzzle, "m_menuToolsPlugins");
			m(Help, "m_menuHelpContents m_btnHelp m_btnHelpSource");
			m(Heart, "m_menuHelpDonate");
			m(Info, "m_menuHelpAbout");

			// Dialog buttons
			m(Eye, "m_cbHidePassword bp_btnShowTotpKey");
			m(OpenFile, "m_btnOpenKeyFile m_btnBinOpen");
			m(FolderOpen, "m_btnOpenFolder");
			return d;
		}

		// ===================== Font and rendering =====================

		private static string g_strFont = null;
		private static bool g_bFontChecked = false;

		private static string FontName
		{
			get
			{
				if(!g_bFontChecked)
				{
					g_bFontChecked = true;
					foreach(string str in new string[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
					{
						try
						{
							using(Font f = new Font(str, 12f, FontStyle.Regular, GraphicsUnit.Pixel))
							{
								if(string.Equals(f.Name, str, StringComparison.OrdinalIgnoreCase)) { g_strFont = str; break; }
							}
						}
						catch(Exception) { }
					}
				}
				return g_strFont;
			}
		}

		public static bool Available { get { return ThemeManager.IsActive && (FontName != null); } }

		private static char MapGlyph(char ch)
		{
			// Glyphs that only Segoe Fluent Icons (Windows 11) has
			if((ch == Password) && (FontName != "Segoe Fluent Icons")) return Key;
			return ch;
		}

		/// <summary>The glyph centered in a transparent bitmap of the given size.</summary>
		public static Bitmap Render(char ch, Size sz, Color clr)
		{
			int w = Math.Max(1, sz.Width), h = Math.Max(1, sz.Height);
			Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
			string strFont = FontName;
			if(strFont == null) return bmp;

			using(Graphics g = Graphics.FromImage(bmp))
			{
				g.SmoothingMode = SmoothingMode.AntiAlias;
				g.PixelOffsetMode = PixelOffsetMode.HighQuality;
				using(FontFamily ff = new FontFamily(strFont))
				{
					using(GraphicsPath gp = new GraphicsPath())
					{
						// The icon font's em square equals the icon size (16 px icons: 16 px em)
						float fEm = Math.Min(w, h);
						gp.AddString(MapGlyph(ch).ToString(), ff, (int)FontStyle.Regular, fEm,
							PointF.Empty, StringFormat.GenericTypographic);
						RectangleF rcB = gp.GetBounds();
						if((rcB.Width > 0f) && (rcB.Height > 0f))
						{
							float fScale = Math.Min(1f, Math.Min((w - 1f) / rcB.Width, (h - 1f) / rcB.Height));
							using(Matrix mx = new Matrix())
							{
								mx.Translate(w / 2f, h / 2f);
								mx.Scale(fScale, fScale);
								mx.Translate(-(rcB.X + rcB.Width / 2f), -(rcB.Y + rcB.Height / 2f));
								gp.Transform(mx);
							}
							using(SolidBrush br = new SolidBrush(clr)) { g.FillPath(br, gp); }
						}
					}
				}
			}
			return bmp;
		}

		// ===================== Menus and toolbars =====================

		private sealed class IconState
		{
			public char Glyph;
			public string Key;
			public Image Generated;
		}

		private static readonly ConditionalWeakTable<object, IconState> g_states =
			new ConditionalWeakTable<object, IconState>();
		private static readonly ConditionalWeakTable<Image, object> g_images =
			new ConditionalWeakTable<Image, object>();
		private static readonly ConditionalWeakTable<ToolStrip, object> g_hooked =
			new ConditionalWeakTable<ToolStrip, object>();
		private static readonly ConditionalWeakTable<ToolStripItem, object> g_hookedItems =
			new ConditionalWeakTable<ToolStripItem, object>();

		private static string StateKey(Size sz)
		{
			return ThemeManager.Palette.Key + "|" + sz.Width.ToString() + "x" + sz.Height.ToString();
		}

		private static void RememberImage(Image img, char ch)
		{
			if(img == null) return;
			object o;
			if(g_images.TryGetValue(img, out o)) return;
			try { g_images.Add(img, ch); }
			catch(ArgumentException) { }
		}

		private static char LookupImage(Image img)
		{
			object o;
			if((img != null) && g_images.TryGetValue(img, out o) && (o is char)) return (char)o;
			return '\0';
		}

		private static char LookupName(string strName)
		{
			char ch;
			if(!string.IsNullOrEmpty(strName) && g_dNames.TryGetValue(strName, out ch)) return ch;
			return '\0';
		}

		/// <summary>
		/// Gives an item its modern icon. Returns the new image, or null if the
		/// item keeps its own (no image, unknown item, or already up to date).
		/// </summary>
		private static Image GetReplacement(object oItem, string strName, Image imgCur, Size sz)
		{
			if(imgCur == null) return null;

			IconState st;
			g_states.TryGetValue(oItem, out st);
			string strKey = StateKey(sz);
			if((st != null) && (imgCur == st.Generated) && (st.Key == strKey)) return null;

			char ch = (((st != null) && (imgCur == st.Generated)) ? st.Glyph : LookupName(strName));
			if(ch == '\0') ch = LookupImage(imgCur);
			if(ch == '\0') return null;

			RememberImage(imgCur, ch);
			Image img = Render(ch, sz, ThemeManager.Palette.Text);
			RememberImage(img, ch);

			if(st == null)
			{
				st = new IconState();
				try { g_states.Add(oItem, st); }
				catch(ArgumentException) { }
			}
			st.Glyph = ch;
			st.Key = strKey;
			st.Generated = img;
			return img;
		}

		public static void ApplyToStrip(ToolStrip ts)
		{
			if(!Available || (ts == null)) return;

			object o;
			if(!g_hooked.TryGetValue(ts, out o))
			{
				try
				{
					g_hooked.Add(ts, true);
					// Context menus are filled/updated by KeePass before they open
					ToolStripDropDown dd = (ts as ToolStripDropDown);
					if(dd != null) dd.Opening += delegate(object sender, System.ComponentModel.CancelEventArgs e) { ApplyToItems(dd.Items); };
				}
				catch(ArgumentException) { }
			}

			ApplyToItems(ts.Items);
		}

		private static void ApplyToItems(ToolStripItemCollection tsic)
		{
			if(!Available || (tsic == null)) return;
			foreach(ToolStripItem tsi in tsic)
			{
				try
				{
					if(tsi.Image != null)
					{
						Image img = GetReplacement(tsi, tsi.Name, tsi.Image, tsi.Image.Size);
						if(img != null) tsi.Image = img;
					}

					ToolStripDropDownItem tsddi = (tsi as ToolStripDropDownItem);
					if(tsddi == null) continue;

					// Submenus filled on demand (recent files, ...): when they open
					object o;
					if(!g_hookedItems.TryGetValue(tsddi, out o))
					{
						g_hookedItems.Add(tsddi, true);
						tsddi.DropDownOpening += delegate(object sender, EventArgs e)
						{
							ApplyToItems(tsddi.DropDownItems);
						};
					}
					if(tsddi.HasDropDownItems) ApplyToItems(tsddi.DropDownItems);
				}
				catch(Exception) { }
			}
		}

		// ===================== Dialog buttons =====================

		public static void ApplyToButton(ButtonBase btn)
		{
			if(!Available || (btn == null)) return;
			string strName = btn.Name;
			bool bOurs = (!string.IsNullOrEmpty(strName) && strName.StartsWith("bp_"));
			if((btn.Image == null) && !bOurs) return; // Text buttons stay text buttons
			if(Gfx.IsType(btn, "ColorButtonEx")) return;

			// Square icon; KeePass' flat images (e.g. the 17 x 5 dots) get the
			// size that fits the button
			Size sz;
			Image imgOrg = btn.Image;
			if((imgOrg != null) && (imgOrg.Height >= 12) && (imgOrg.Width <= 2 * imgOrg.Height))
				sz = imgOrg.Size;
			else
			{
				int n = Math.Max(12, Math.Min((int)Math.Round(btn.Height * 0.6), btn.Width - 6));
				sz = new Size(n, n);
			}

			Image imgCur = (btn.Image ?? new Bitmap(1, 1));
			Image img = GetReplacement(btn, strName, imgCur, sz);
			if(img != null)
			{
				btn.Image = img;
				if(bOurs) btn.Text = string.Empty; // Placeholder text ("•••") of our own buttons
			}
		}

		// ===================== Entry and group icons =====================

		private const string ClientIconsTag = "BetterPass.Icons:";

		/// <summary>
		/// Replaces the standard icons (the first PwIcon.Count images) of
		/// KeePass' current icon list. Custom icons stay as they are.
		/// </summary>
		public static bool ApplyToClientIcons(ImageList il)
		{
			if(!Available || (il == null)) return false;
			string strTag = ClientIconsTag + StateKey(il.ImageSize);
			if(object.Equals(il.Tag, strTag)) return false;
			if((il.Tag != null) && !(il.Tag is string)) return false; // Someone else's tag

			try
			{
				ThemePalette p = ThemeManager.Palette;
				int n = Math.Min(il.Images.Count, g_vPwIcons.Length);
				for(int i = 0; i < n; ++i)
				{
					Color clr = p.Text;
					if((i == PwIconWarning) || (i == PwIconEnergyCareful)) clr = p.Warning;
					else if(i == PwIconExpired) clr = p.Error;
					il.Images[i] = Render(g_vPwIcons[i], il.ImageSize, clr);
				}
				il.Tag = strTag;
				return true;
			}
			catch(Exception) { }
			return false;
		}

		// ===================== Banners =====================

		private static readonly string[][] g_vBannerWords = new string[][] {
			new string[] { "", "master key", "masterschl", "open database", "datenbank öffnen",
				"key file", "schlüsseldatei", "unlock", "entsperr" },
			new string[] { "", "generator", "generat" },
			new string[] { "", "option", "einstellung", "settings", "database" , "datenbank" },
			new string[] { "", "search", "find", "such" },
			new string[] { "", "import" },
			new string[] { "", "export" },
			new string[] { "", "print", "druck" },
			new string[] { "", "plugin" },
			new string[] { "", "trigger" },
			new string[] { "", "auto-type", "autotype", "auto-typ" },
			new string[] { "", "icon" },
			new string[] { "", "history", "verlauf", "chronik" },
			new string[] { "", "about", "über" },
			new string[] { "", "help", "hilfe" },
			new string[] { "", "attachment", "anhang", "anhänge" },
			new string[] { "", "tag" },
			new string[] { "", "color", "farbe" },
			new string[] { "", "sync" },
			new string[] { "", "group", "gruppe" },
			new string[] { "", "string", "field", "feld" },
			new string[] { "", "entry", "eintrag" }
		};

		/// <summary>A glyph for a dialog banner, chosen by the dialog's title.</summary>
		public static char BannerGlyph(string strTitle)
		{
			string str = (strTitle ?? string.Empty).ToLowerInvariant();
			foreach(string[] v in g_vBannerWords)
			{
				for(int i = 1; i < v.Length; ++i)
				{
					if(str.IndexOf(v[i], StringComparison.Ordinal) >= 0) return MapGlyph(v[0][0]);
				}
			}
			return Key;
		}
	}
}
