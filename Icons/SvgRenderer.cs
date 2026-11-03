/*
  BetterPass - KeePass plugin
  Compact SVG renderer (GDI+) for brand icons.

  Supported: svg, g, a, switch, path, rect, circle, ellipse, line, polyline,
  polygon, use/symbol, linearGradient, radialGradient, clipPath, <style> with
  simple class/id/tag selectors, style attributes, transforms, opacity,
  fill-rule, strokes. Ignored: masks, filters, text, images, patterns.

  Input comes from the network: DTDs are not processed, external resources
  are never loaded, and size/element/recursion limits apply.
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace BetterPass
{
	internal static class SvgRenderer
	{
		private const int MaxSvgChars = 2 * 1024 * 1024;
		private const int MaxElements = 20000;
		private const int MaxDepth = 32;

		private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

		// ===================== Public API =====================

		public static Bitmap Render(string strSvg, int nSize, Color clrCurrent, float fPadding)
		{
			if(string.IsNullOrEmpty(strSvg)) throw new ArgumentNullException("strSvg");
			if(strSvg.Length > MaxSvgChars) throw new InvalidDataException("SVG is too large.");
			if(nSize < 8) nSize = 8;

			XmlDocument doc = LoadXml(strSvg);
			XmlElement xeRoot = doc.DocumentElement;
			if((xeRoot == null) || (xeRoot.LocalName != "svg"))
				throw new InvalidDataException("No SVG document.");

			RenderContext ctx = new RenderContext();
			ctx.CurrentColor = clrCurrent;
			IndexDocument(ctx, xeRoot);

			RectangleF rcView = GetViewBox(xeRoot);
			Bitmap bmp = new Bitmap(nSize, nSize, PixelFormat.Format32bppArgb);
			try
			{
				using(Graphics g = Graphics.FromImage(bmp))
				{
					g.Clear(Color.Transparent);
					g.SmoothingMode = SmoothingMode.AntiAlias;
					g.PixelOffsetMode = PixelOffsetMode.HighQuality;
					g.CompositingQuality = CompositingQuality.HighQuality;
					g.InterpolationMode = InterpolationMode.HighQualityBicubic;

					float fPad = nSize * Math.Max(0f, Math.Min(0.3f, fPadding));
					float fAvail = nSize - 2f * fPad;
					float fScale = Math.Min(fAvail / rcView.Width, fAvail / rcView.Height);
					float dx = fPad + (fAvail - rcView.Width * fScale) / 2f - rcView.X * fScale;
					float dy = fPad + (fAvail - rcView.Height * fScale) / 2f - rcView.Y * fScale;

					using(Matrix m = new Matrix(fScale, 0f, 0f, fScale, dx, dy))
					{
						g.Transform = m;
					}

					Style st = Style.CreateRoot();
					RenderChildren(ctx, g, xeRoot, st, 0);
				}
			}
			catch(Exception)
			{
				bmp.Dispose();
				throw;
			}
			return bmp;
		}

		public static byte[] RenderPng(string strSvg, int nSize, Color clrCurrent, float fPadding)
		{
			using(Bitmap bmp = Render(strSvg, nSize, clrCurrent, fPadding))
			{
				using(MemoryStream ms = new MemoryStream())
				{
					bmp.Save(ms, ImageFormat.Png);
					return ms.ToArray();
				}
			}
		}

		// ===================== XML =====================

		private static XmlDocument LoadXml(string strSvg)
		{
			XmlReaderSettings xrs = new XmlReaderSettings();
			xrs.DtdProcessing = DtdProcessing.Ignore;
			xrs.XmlResolver = null;
			xrs.MaxCharactersInDocument = MaxSvgChars;
			xrs.IgnoreComments = true;
			xrs.IgnoreProcessingInstructions = true;

			XmlDocument doc = new XmlDocument();
			doc.XmlResolver = null;
			using(StringReader sr = new StringReader(strSvg))
			{
				using(XmlReader xr = XmlReader.Create(sr, xrs))
				{
					doc.Load(xr);
				}
			}
			return doc;
		}

		private sealed class CssRule
		{
			public string Selector;
			public Dictionary<string, string> Decls;
		}

		private sealed class RenderContext
		{
			public Color CurrentColor = Color.Black;
			public readonly Dictionary<string, XmlElement> Ids =
				new Dictionary<string, XmlElement>(StringComparer.Ordinal);
			public readonly List<CssRule> Css = new List<CssRule>();
			public int Elements = 0;
		}

		private static void IndexDocument(RenderContext ctx, XmlElement xeRoot)
		{
			int n = 0;
			foreach(XmlNode xn in xeRoot.GetElementsByTagName("*"))
			{
				XmlElement xe = (xn as XmlElement);
				if(xe == null) continue;
				if(++n > MaxElements) throw new InvalidDataException("SVG has too many elements.");

				string strId = xe.GetAttribute("id");
				if((strId.Length > 0) && !ctx.Ids.ContainsKey(strId)) ctx.Ids[strId] = xe;

				if(xe.LocalName == "style") ParseCss(ctx, xe.InnerText);
			}
		}

		private static void ParseCss(RenderContext ctx, string strCss)
		{
			if(string.IsNullOrEmpty(strCss)) return;

			// Remove comments
			StringBuilder sb = new StringBuilder(strCss.Length);
			int i = 0;
			while(i < strCss.Length)
			{
				if((i + 1 < strCss.Length) && (strCss[i] == '/') && (strCss[i + 1] == '*'))
				{
					int iEnd = strCss.IndexOf("*/", i + 2, StringComparison.Ordinal);
					if(iEnd < 0) break;
					i = iEnd + 2;
					continue;
				}
				sb.Append(strCss[i]);
				++i;
			}
			string s = sb.ToString();

			int iPos = 0;
			while(iPos < s.Length)
			{
				int iOpen = s.IndexOf('{', iPos);
				if(iOpen < 0) break;
				int iClose = s.IndexOf('}', iOpen + 1);
				if(iClose < 0) break;

				string strSelectors = s.Substring(iPos, iOpen - iPos).Trim();
				string strBody = s.Substring(iOpen + 1, iClose - iOpen - 1);
				iPos = iClose + 1;

				if(strSelectors.StartsWith("@", StringComparison.Ordinal)) continue;

				Dictionary<string, string> d = ParseDeclarations(strBody);
				foreach(string strSel in strSelectors.Split(','))
				{
					string strS = strSel.Trim();
					if(strS.Length == 0) continue;
					CssRule r = new CssRule();
					r.Selector = strS;
					r.Decls = d;
					ctx.Css.Add(r);
				}
			}
		}

		private static Dictionary<string, string> ParseDeclarations(string strBody)
		{
			Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);
			if(string.IsNullOrEmpty(strBody)) return d;

			foreach(string strDecl in strBody.Split(';'))
			{
				int iColon = strDecl.IndexOf(':');
				if(iColon <= 0) continue;
				string strName = strDecl.Substring(0, iColon).Trim().ToLowerInvariant();
				string strValue = strDecl.Substring(iColon + 1).Trim();
				if(strValue.EndsWith("!important", StringComparison.OrdinalIgnoreCase))
					strValue = strValue.Substring(0, strValue.Length - 10).Trim();
				if((strName.Length > 0) && (strValue.Length > 0)) d[strName] = strValue;
			}
			return d;
		}

		private static RectangleF GetViewBox(XmlElement xe)
		{
			float[] v = ParseNumberList(xe.GetAttribute("viewBox"));
			if((v.Length == 4) && (v[2] > 0f) && (v[3] > 0f))
				return new RectangleF(v[0], v[1], v[2], v[3]);

			float w = ParseLength(xe.GetAttribute("width"), 0f);
			float h = ParseLength(xe.GetAttribute("height"), 0f);
			if((w > 0f) && (h > 0f)) return new RectangleF(0f, 0f, w, h);
			return new RectangleF(0f, 0f, 24f, 24f);
		}

		// ===================== Style =====================

		private sealed class Style
		{
			// Inherited
			public string Fill = "#000";
			public float FillOpacity = 1f;
			public string Stroke = "none";
			public float StrokeOpacity = 1f;
			public float StrokeWidth = 1f;
			public string FillRule = "nonzero";
			public string ClipRule = "nonzero";
			public string LineCap = "butt";
			public string LineJoin = "miter";
			public string Color = null;
			public bool Visible = true;

			// Not inherited, accumulated into the alpha of fills and strokes
			public float GroupOpacity = 1f;

			public static Style CreateRoot() { return new Style(); }

			public Style CloneInherited()
			{
				Style s = (Style)MemberwiseClone();
				return s;
			}
		}

		private static Style ComputeStyle(RenderContext ctx, XmlElement xe, Style stParent,
			out bool bDisplayNone, out string strClipPath)
		{
			Style st = stParent.CloneInherited();
			bDisplayNone = false;
			strClipPath = null;

			Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.Ordinal);

			// 1. Presentation attributes
			foreach(XmlAttribute xa in xe.Attributes)
			{
				string strName = xa.LocalName;
				if(IsStyleProperty(strName)) d[strName] = xa.Value.Trim();
			}

			// 2. CSS rules (in document order)
			if(ctx.Css.Count > 0)
			{
				string strClass = xe.GetAttribute("class");
				string strId = xe.GetAttribute("id");
				string[] vClasses = (strClass.Length > 0) ? strClass.Split(new char[] { ' ', '\t' },
					StringSplitOptions.RemoveEmptyEntries) : new string[0];

				foreach(CssRule r in ctx.Css)
				{
					if(SelectorMatches(r.Selector, xe.LocalName, strId, vClasses))
					{
						foreach(KeyValuePair<string, string> kvp in r.Decls) d[kvp.Key] = kvp.Value;
					}
				}
			}

			// 3. style attribute
			string strStyle = xe.GetAttribute("style");
			if(strStyle.Length > 0)
			{
				foreach(KeyValuePair<string, string> kvp in ParseDeclarations(strStyle))
					d[kvp.Key] = kvp.Value;
			}

			string v;
			if(d.TryGetValue("color", out v)) st.Color = v;
			if(d.TryGetValue("fill", out v)) st.Fill = v;
			if(d.TryGetValue("stroke", out v)) st.Stroke = v;
			if(d.TryGetValue("fill-opacity", out v)) st.FillOpacity = ParseOpacity(v, st.FillOpacity);
			if(d.TryGetValue("stroke-opacity", out v)) st.StrokeOpacity = ParseOpacity(v, st.StrokeOpacity);
			if(d.TryGetValue("stroke-width", out v)) st.StrokeWidth = ParseLength(v, st.StrokeWidth);
			if(d.TryGetValue("fill-rule", out v)) st.FillRule = v.Trim();
			if(d.TryGetValue("clip-rule", out v)) st.ClipRule = v.Trim();
			if(d.TryGetValue("stroke-linecap", out v)) st.LineCap = v.Trim();
			if(d.TryGetValue("stroke-linejoin", out v)) st.LineJoin = v.Trim();
			if(d.TryGetValue("visibility", out v)) st.Visible = !(v.Trim() == "hidden" || v.Trim() == "collapse");
			if(d.TryGetValue("display", out v) && (v.Trim() == "none")) bDisplayNone = true;
			if(d.TryGetValue("opacity", out v)) st.GroupOpacity *= ParseOpacity(v, 1f);
			if(d.TryGetValue("clip-path", out v)) strClipPath = v;

			return st;
		}

		private static bool IsStyleProperty(string strName)
		{
			switch(strName)
			{
				case "fill": case "fill-opacity": case "fill-rule":
				case "stroke": case "stroke-opacity": case "stroke-width":
				case "stroke-linecap": case "stroke-linejoin":
				case "opacity": case "display": case "visibility": case "color":
				case "clip-path": case "clip-rule":
					return true;
				default:
					return false;
			}
		}

		private static bool SelectorMatches(string strSel, string strTag, string strId,
			string[] vClasses)
		{
			// Supports "tag", ".class", "#id", "tag.class" and ".a.b"; the last
			// compound of a descendant selector is matched (approximation)
			string s = strSel;
			int iSpace = s.LastIndexOfAny(new char[] { ' ', '>' });
			if(iSpace >= 0) s = s.Substring(iSpace + 1);
			if((s.Length == 0) || (s.IndexOf(':') >= 0) || (s.IndexOf('[') >= 0)) return false;
			if(s == "*") return true;

			int i = 0;
			string strTagPart = string.Empty;
			while((i < s.Length) && (s[i] != '.') && (s[i] != '#')) ++i;
			strTagPart = s.Substring(0, i);
			if((strTagPart.Length > 0) && (strTagPart != strTag)) return false;

			while(i < s.Length)
			{
				char chType = s[i];
				int j = i + 1;
				while((j < s.Length) && (s[j] != '.') && (s[j] != '#')) ++j;
				string strName = s.Substring(i + 1, j - i - 1);
				if(chType == '.')
				{
					if(Array.IndexOf(vClasses, strName) < 0) return false;
				}
				else if(strName != strId) return false;
				i = j;
			}
			return true;
		}

		// ===================== Rendering =====================

		private static void RenderChildren(RenderContext ctx, Graphics g, XmlElement xeParent,
			Style st, int nDepth)
		{
			foreach(XmlNode xn in xeParent.ChildNodes)
			{
				XmlElement xe = (xn as XmlElement);
				if(xe != null) RenderElement(ctx, g, xe, st, nDepth);
			}
		}

		private static void RenderElement(RenderContext ctx, Graphics g, XmlElement xe,
			Style stParent, int nDepth)
		{
			if(nDepth > MaxDepth) return;
			if(++ctx.Elements > MaxElements) return;

			string strTag = xe.LocalName;
			switch(strTag)
			{
				case "defs": case "clipPath": case "mask": case "linearGradient":
				case "radialGradient": case "title": case "desc": case "style":
				case "metadata": case "filter": case "pattern": case "symbol":
				case "text": case "image": case "marker": case "script":
				case "foreignObject":
					return;
			}

			bool bNone;
			string strClip;
			Style st = ComputeStyle(ctx, xe, stParent, out bNone, out strClip);
			if(bNone) return;

			GraphicsState gs = g.Save();
			try
			{
				Matrix mElem = ParseTransform(xe.GetAttribute("transform"));
				if(mElem != null)
				{
					g.MultiplyTransform(mElem, MatrixOrder.Prepend);
					mElem.Dispose();
				}

				if(!string.IsNullOrEmpty(strClip)) ApplyClip(ctx, g, strClip, nDepth);

				switch(strTag)
				{
					case "svg":
					case "g":
					case "a":
					case "switch":
						RenderChildren(ctx, g, xe, st, nDepth + 1);
						break;

					case "use":
						RenderUse(ctx, g, xe, st, nDepth);
						break;

					default:
						using(GraphicsPath gp = BuildShape(xe))
						{
							if(gp != null) PaintShape(ctx, g, gp, st);
						}
						break;
				}
			}
			finally { g.Restore(gs); }
		}

		private static void RenderUse(RenderContext ctx, Graphics g, XmlElement xe, Style st,
			int nDepth)
		{
			XmlElement xeRef = ResolveHref(ctx, xe);
			if(xeRef == null) return;

			float x = ParseLength(xe.GetAttribute("x"), 0f);
			float y = ParseLength(xe.GetAttribute("y"), 0f);
			if((x != 0f) || (y != 0f)) g.TranslateTransform(x, y, MatrixOrder.Prepend);

			if(xeRef.LocalName == "symbol")
			{
				bool bNone;
				string strClip;
				Style stSym = ComputeStyle(ctx, xeRef, st, out bNone, out strClip);
				if(!bNone) RenderChildren(ctx, g, xeRef, stSym, nDepth + 1);
			}
			else RenderElement(ctx, g, xeRef, st, nDepth + 1);
		}

		private static XmlElement ResolveHref(RenderContext ctx, XmlElement xe)
		{
			string strHref = xe.GetAttribute("href");
			if(strHref.Length == 0)
				strHref = xe.GetAttribute("href", "http://www.w3.org/1999/xlink");
			if(!strHref.StartsWith("#", StringComparison.Ordinal)) return null;

			XmlElement xeRef;
			if(ctx.Ids.TryGetValue(strHref.Substring(1), out xeRef)) return xeRef;
			return null;
		}

		private static string GetUrlId(string strValue)
		{
			if(string.IsNullOrEmpty(strValue)) return null;
			string s = strValue.Trim();
			if(!s.StartsWith("url(", StringComparison.OrdinalIgnoreCase)) return null;
			int iEnd = s.IndexOf(')');
			if(iEnd < 0) return null;
			string strInner = s.Substring(4, iEnd - 4).Trim().Trim('\'', '"').Trim();
			if(!strInner.StartsWith("#", StringComparison.Ordinal)) return null;
			return strInner.Substring(1);
		}

		private static void ApplyClip(RenderContext ctx, Graphics g, string strClip, int nDepth)
		{
			string strId = GetUrlId(strClip);
			XmlElement xeClip;
			if((strId == null) || !ctx.Ids.TryGetValue(strId, out xeClip)) return;
			if(xeClip.LocalName != "clipPath") return;

			using(GraphicsPath gpClip = new GraphicsPath(FillMode.Winding))
			{
				Matrix mClip = ParseTransform(xeClip.GetAttribute("transform"));
				CollectClipShapes(ctx, xeClip, gpClip, mClip, nDepth);
				if(mClip != null) mClip.Dispose();

				if(gpClip.PointCount > 0) g.SetClip(gpClip, CombineMode.Intersect);
			}
		}

		private static void CollectClipShapes(RenderContext ctx, XmlElement xeParent,
			GraphicsPath gpTarget, Matrix mParent, int nDepth)
		{
			if(nDepth > MaxDepth) return;

			foreach(XmlNode xn in xeParent.ChildNodes)
			{
				XmlElement xe = (xn as XmlElement);
				if(xe == null) continue;

				Matrix m = (mParent != null) ? mParent.Clone() : new Matrix();
				Matrix mOwn = ParseTransform(xe.GetAttribute("transform"));
				if(mOwn != null) { m.Multiply(mOwn, MatrixOrder.Prepend); mOwn.Dispose(); }

				XmlElement xeShape = xe;
				if(xe.LocalName == "use")
				{
					xeShape = ResolveHref(ctx, xe);
					float x = ParseLength(xe.GetAttribute("x"), 0f);
					float y = ParseLength(xe.GetAttribute("y"), 0f);
					if((x != 0f) || (y != 0f)) m.Translate(x, y, MatrixOrder.Prepend);
				}

				if(xeShape != null)
				{
					if(xeShape.LocalName == "g")
						CollectClipShapes(ctx, xeShape, gpTarget, m, nDepth + 1);
					else
					{
						using(GraphicsPath gp = BuildShape(xeShape))
						{
							if((gp != null) && (gp.PointCount > 0))
							{
								gp.Transform(m);
								gpTarget.AddPath(gp, false);
							}
						}
					}
				}
				m.Dispose();
			}
		}

		private static void PaintShape(RenderContext ctx, Graphics g, GraphicsPath gp, Style st)
		{
			if(!st.Visible || (gp.PointCount == 0)) return;

			gp.FillMode = ((st.FillRule == "evenodd") ? FillMode.Alternate : FillMode.Winding);

			// Fill
			string strFill = st.Fill;
			if(!string.IsNullOrEmpty(strFill) && (strFill != "none"))
			{
				float fAlpha = st.FillOpacity * st.GroupOpacity;
				string strGradId = GetUrlId(strFill);
				if(strGradId != null)
				{
					XmlElement xeGrad;
					if(ctx.Ids.TryGetValue(strGradId, out xeGrad))
						FillWithGradient(ctx, g, gp, xeGrad, fAlpha);
					else
					{
						// Fallback after url(): "url(#x) #fff"
						string strFb = strFill.Substring(strFill.IndexOf(')') + 1).Trim();
						Color cFb;
						if(TryParseColor(ctx, st, strFb, out cFb))
							FillSolid(g, gp, ApplyAlpha(cFb, fAlpha));
					}
				}
				else
				{
					Color c;
					if(TryParseColor(ctx, st, strFill, out c))
						FillSolid(g, gp, ApplyAlpha(c, fAlpha));
				}
			}

			// Stroke (solid colors; gradients use their middle color)
			string strStroke = st.Stroke;
			if(!string.IsNullOrEmpty(strStroke) && (strStroke != "none") && (st.StrokeWidth > 0f))
			{
				Color c;
				bool bOk;
				string strGradId = GetUrlId(strStroke);
				if(strGradId != null)
				{
					XmlElement xeGrad;
					bOk = ctx.Ids.TryGetValue(strGradId, out xeGrad);
					c = (bOk ? GetGradientAverage(ctx, xeGrad) : Color.Empty);
				}
				else bOk = TryParseColor(ctx, st, strStroke, out c);

				if(bOk)
				{
					using(Pen p = new Pen(ApplyAlpha(c, st.StrokeOpacity * st.GroupOpacity),
						st.StrokeWidth))
					{
						LineCap lc = LineCap.Flat;
						if(st.LineCap == "round") lc = LineCap.Round;
						else if(st.LineCap == "square") lc = LineCap.Square;
						p.StartCap = lc;
						p.EndCap = lc;
						if(st.LineJoin == "round") p.LineJoin = LineJoin.Round;
						else if(st.LineJoin == "bevel") p.LineJoin = LineJoin.Bevel;
						else p.LineJoin = LineJoin.MiterClipped;
						g.DrawPath(p, gp);
					}
				}
			}
		}

		private static void FillSolid(Graphics g, GraphicsPath gp, Color c)
		{
			if(c.A == 0) return;
			using(SolidBrush br = new SolidBrush(c)) { g.FillPath(br, gp); }
		}

		private static Color ApplyAlpha(Color c, float fAlpha)
		{
			int a = (int)Math.Round(c.A * Math.Max(0f, Math.Min(1f, fAlpha)));
			return Color.FromArgb(a, c.R, c.G, c.B);
		}

		// ===================== Gradients =====================

		private sealed class GradStop
		{
			public float Offset;
			public Color Color;
		}

		private static string GetGradAttr(RenderContext ctx, XmlElement xe, string strName)
		{
			XmlElement xeCur = xe;
			for(int i = 0; (i < 8) && (xeCur != null); ++i)
			{
				if(xeCur.HasAttribute(strName)) return xeCur.GetAttribute(strName);
				xeCur = ResolveHref(ctx, xeCur);
			}
			return null;
		}

		private static List<GradStop> GetStops(RenderContext ctx, XmlElement xe)
		{
			XmlElement xeCur = xe;
			for(int i = 0; (i < 8) && (xeCur != null); ++i)
			{
				List<GradStop> l = new List<GradStop>();
				foreach(XmlNode xn in xeCur.ChildNodes)
				{
					XmlElement xeStop = (xn as XmlElement);
					if((xeStop == null) || (xeStop.LocalName != "stop")) continue;

					Dictionary<string, string> d = ParseDeclarations(xeStop.GetAttribute("style"));
					string strColor = xeStop.GetAttribute("stop-color");
					string strOpacity = xeStop.GetAttribute("stop-opacity");
					string v;
					if(d.TryGetValue("stop-color", out v)) strColor = v;
					if(d.TryGetValue("stop-opacity", out v)) strOpacity = v;

					Color c;
					if(!TryParseColor(ctx, null, (strColor.Length > 0) ? strColor : "#000", out c))
						c = Color.Black;
					c = ApplyAlpha(c, ParseOpacity(strOpacity, 1f));

					GradStop gs = new GradStop();
					gs.Offset = Math.Max(0f, Math.Min(1f, ParseOffset(xeStop.GetAttribute("offset"))));
					if((l.Count > 0) && (gs.Offset < l[l.Count - 1].Offset))
						gs.Offset = l[l.Count - 1].Offset;
					gs.Color = c;
					l.Add(gs);
				}
				if(l.Count > 0) return l;
				xeCur = ResolveHref(ctx, xeCur);
			}
			return new List<GradStop>();
		}

		private static Color GetGradientAverage(RenderContext ctx, XmlElement xeGrad)
		{
			List<GradStop> l = GetStops(ctx, xeGrad);
			if(l.Count == 0) return Color.Black;
			return l[l.Count / 2].Color;
		}

		private static void FillWithGradient(RenderContext ctx, Graphics g, GraphicsPath gp,
			XmlElement xeGrad, float fAlpha)
		{
			List<GradStop> lStops = GetStops(ctx, xeGrad);
			if(lStops.Count == 0) return;
			if(fAlpha < 1f)
			{
				foreach(GradStop s in lStops) s.Color = ApplyAlpha(s.Color, fAlpha);
			}
			if(lStops.Count == 1) { FillSolid(g, gp, lStops[0].Color); return; }

			bool bUserSpace = (GetGradAttr(ctx, xeGrad, "gradientUnits") == "userSpaceOnUse");
			RectangleF rcB = gp.GetBounds();
			if((rcB.Width <= 0f) || (rcB.Height <= 0f)) return;

			Matrix mUnits = (bUserSpace ? new Matrix() :
				new Matrix(rcB.Width, 0f, 0f, rcB.Height, rcB.X, rcB.Y));
			Matrix mGrad = ParseTransform(GetGradAttr(ctx, xeGrad, "gradientTransform"));
			if(mGrad != null) { mUnits.Multiply(mGrad, MatrixOrder.Prepend); mGrad.Dispose(); }

			try
			{
				if(xeGrad.LocalName == "radialGradient")
					FillRadial(ctx, g, gp, xeGrad, lStops, mUnits, bUserSpace, rcB);
				else FillLinear(ctx, g, gp, xeGrad, lStops, mUnits, bUserSpace, rcB);
			}
			catch(Exception)
			{
				FillSolid(g, gp, lStops[lStops.Count / 2].Color);
			}
			finally { mUnits.Dispose(); }
		}

		private static float GradCoord(RenderContext ctx, XmlElement xe, string strName,
			float fDefault, bool bUserSpace, float fRef)
		{
			string s = GetGradAttr(ctx, xe, strName);
			if(string.IsNullOrEmpty(s)) return fDefault;
			s = s.Trim();
			if(s.EndsWith("%", StringComparison.Ordinal))
			{
				float f = ParseFloat(s.Substring(0, s.Length - 1), 0f) / 100f;
				return (bUserSpace ? f * fRef : f);
			}
			return ParseLength(s, fDefault);
		}

		private static void FillLinear(RenderContext ctx, Graphics g, GraphicsPath gp,
			XmlElement xe, List<GradStop> lStops, Matrix mUnits, bool bUserSpace, RectangleF rcB)
		{
			float x1 = GradCoord(ctx, xe, "x1", 0f, bUserSpace, rcB.Width);
			float y1 = GradCoord(ctx, xe, "y1", 0f, bUserSpace, rcB.Height);
			float x2 = GradCoord(ctx, xe, "x2", 1f, bUserSpace, rcB.Width);
			float y2 = GradCoord(ctx, xe, "y2", 0f, bUserSpace, rcB.Height);

			PointF[] vPts = new PointF[] { new PointF(x1, y1), new PointF(x2, y2) };
			mUnits.TransformPoints(vPts);
			PointF p1 = vPts[0], p2 = vPts[1];

			float vx = p2.X - p1.X, vy = p2.Y - p1.Y;
			float fLen2 = vx * vx + vy * vy;
			if(fLen2 < 1e-9f) { FillSolid(g, gp, lStops[lStops.Count - 1].Color); return; }

			// Extend the gradient line so that it covers the whole shape ("pad")
			PointF[] vCorners = new PointF[] { new PointF(rcB.Left, rcB.Top),
				new PointF(rcB.Right, rcB.Top), new PointF(rcB.Left, rcB.Bottom),
				new PointF(rcB.Right, rcB.Bottom) };
			float tMin = 0f, tMax = 1f;
			foreach(PointF pt in vCorners)
			{
				float t = ((pt.X - p1.X) * vx + (pt.Y - p1.Y) * vy) / fLen2;
				tMin = Math.Min(tMin, t);
				tMax = Math.Max(tMax, t);
			}
			tMin -= 0.01f;
			tMax += 0.01f;

			PointF pA = new PointF(p1.X + vx * tMin, p1.Y + vy * tMin);
			PointF pB = new PointF(p1.X + vx * tMax, p1.Y + vy * tMax);
			float fSpan = tMax - tMin;

			List<Color> lColors = new List<Color>();
			List<float> lPos = new List<float>();
			lColors.Add(lStops[0].Color);
			lPos.Add(0f);
			foreach(GradStop s in lStops)
			{
				float f = (s.Offset - tMin) / fSpan;
				f = Math.Max(lPos[lPos.Count - 1], Math.Min(1f, f));
				lColors.Add(s.Color);
				lPos.Add(f);
			}
			lColors.Add(lStops[lStops.Count - 1].Color);
			lPos.Add(1f);

			using(LinearGradientBrush br = new LinearGradientBrush(pA, pB,
				lColors[0], lColors[lColors.Count - 1]))
			{
				ColorBlend cb = new ColorBlend(lColors.Count);
				cb.Colors = lColors.ToArray();
				cb.Positions = lPos.ToArray();
				br.InterpolationColors = cb;
				br.WrapMode = WrapMode.TileFlipXY;
				g.FillPath(br, gp);
			}
		}

		private static void FillRadial(RenderContext ctx, Graphics g, GraphicsPath gp,
			XmlElement xe, List<GradStop> lStops, Matrix mUnits, bool bUserSpace, RectangleF rcB)
		{
			float fRefR = (float)Math.Sqrt((rcB.Width * rcB.Width + rcB.Height * rcB.Height) / 2.0);
			float cx = GradCoord(ctx, xe, "cx", 0.5f, bUserSpace, rcB.Width);
			float cy = GradCoord(ctx, xe, "cy", 0.5f, bUserSpace, rcB.Height);
			float r = GradCoord(ctx, xe, "r", 0.5f, bUserSpace, fRefR);
			float fx = GradCoord(ctx, xe, "fx", cx, bUserSpace, rcB.Width);
			float fy = GradCoord(ctx, xe, "fy", cy, bUserSpace, rcB.Height);
			if(r <= 0f) { FillSolid(g, gp, lStops[lStops.Count - 1].Color); return; }

			// Outside the circle: last stop color ("pad")
			FillSolid(g, gp, lStops[lStops.Count - 1].Color);

			using(GraphicsPath gpCircle = new GraphicsPath())
			{
				gpCircle.AddEllipse(cx - r, cy - r, 2f * r, 2f * r);
				gpCircle.Transform(mUnits);

				PointF[] vFocus = new PointF[] { new PointF(fx, fy) };
				mUnits.TransformPoints(vFocus);

				using(PathGradientBrush br = new PathGradientBrush(gpCircle))
				{
					br.CenterPoint = vFocus[0];

					// GDI+: position 0 = edge, 1 = center (reverse of SVG)
					int n = lStops.Count;
					List<Color> lColors = new List<Color>();
					List<float> lPos = new List<float>();
					lColors.Add(lStops[n - 1].Color);
					lPos.Add(0f);
					for(int i = n - 1; i >= 0; --i)
					{
						float f = 1f - lStops[i].Offset;
						f = Math.Max(lPos[lPos.Count - 1], Math.Min(1f, f));
						lColors.Add(lStops[i].Color);
						lPos.Add(f);
					}
					lColors.Add(lStops[0].Color);
					lPos.Add(1f);

					ColorBlend cb = new ColorBlend(lColors.Count);
					cb.Colors = lColors.ToArray();
					cb.Positions = lPos.ToArray();
					br.InterpolationColors = cb;
					g.FillPath(br, gp);
				}
			}
		}

		// ===================== Shapes =====================

		private static GraphicsPath BuildShape(XmlElement xe)
		{
			GraphicsPath gp = new GraphicsPath();
			try
			{
				switch(xe.LocalName)
				{
					case "path":
						SvgPathParser.Parse(xe.GetAttribute("d"), gp);
						break;

					case "rect":
					{
						float x = ParseLength(xe.GetAttribute("x"), 0f);
						float y = ParseLength(xe.GetAttribute("y"), 0f);
						float w = ParseLength(xe.GetAttribute("width"), 0f);
						float h = ParseLength(xe.GetAttribute("height"), 0f);
						if((w <= 0f) || (h <= 0f)) { gp.Dispose(); return null; }

						bool bRx = xe.HasAttribute("rx"), bRy = xe.HasAttribute("ry");
						float rx = ParseLength(xe.GetAttribute("rx"), 0f);
						float ry = ParseLength(xe.GetAttribute("ry"), 0f);
						if(bRx && !bRy) ry = rx;
						else if(bRy && !bRx) rx = ry;
						rx = Math.Min(rx, w / 2f);
						ry = Math.Min(ry, h / 2f);

						if((rx <= 0f) || (ry <= 0f)) gp.AddRectangle(new RectangleF(x, y, w, h));
						else
						{
							gp.AddArc(x, y, 2f * rx, 2f * ry, 180f, 90f);
							gp.AddArc(x + w - 2f * rx, y, 2f * rx, 2f * ry, 270f, 90f);
							gp.AddArc(x + w - 2f * rx, y + h - 2f * ry, 2f * rx, 2f * ry, 0f, 90f);
							gp.AddArc(x, y + h - 2f * ry, 2f * rx, 2f * ry, 90f, 90f);
							gp.CloseFigure();
						}
						break;
					}

					case "circle":
					{
						float cx = ParseLength(xe.GetAttribute("cx"), 0f);
						float cy = ParseLength(xe.GetAttribute("cy"), 0f);
						float r = ParseLength(xe.GetAttribute("r"), 0f);
						if(r <= 0f) { gp.Dispose(); return null; }
						gp.AddEllipse(cx - r, cy - r, 2f * r, 2f * r);
						break;
					}

					case "ellipse":
					{
						float cx = ParseLength(xe.GetAttribute("cx"), 0f);
						float cy = ParseLength(xe.GetAttribute("cy"), 0f);
						float rx = ParseLength(xe.GetAttribute("rx"), 0f);
						float ry = ParseLength(xe.GetAttribute("ry"), 0f);
						if((rx <= 0f) || (ry <= 0f)) { gp.Dispose(); return null; }
						gp.AddEllipse(cx - rx, cy - ry, 2f * rx, 2f * ry);
						break;
					}

					case "line":
					{
						gp.AddLine(ParseLength(xe.GetAttribute("x1"), 0f),
							ParseLength(xe.GetAttribute("y1"), 0f),
							ParseLength(xe.GetAttribute("x2"), 0f),
							ParseLength(xe.GetAttribute("y2"), 0f));
						break;
					}

					case "polyline":
					case "polygon":
					{
						float[] v = ParseNumberList(xe.GetAttribute("points"));
						int nPts = v.Length / 2;
						if(nPts < 2) { gp.Dispose(); return null; }
						PointF[] vPts = new PointF[nPts];
						for(int i = 0; i < nPts; ++i) vPts[i] = new PointF(v[2 * i], v[2 * i + 1]);
						if(xe.LocalName == "polygon") gp.AddPolygon(vPts);
						else gp.AddLines(vPts);
						break;
					}

					default:
						gp.Dispose();
						return null;
				}
			}
			catch(Exception)
			{
				gp.Dispose();
				return null;
			}

			return gp;
		}

		// ===================== Parsing helpers =====================

		internal static float ParseFloat(string s, float fDefault)
		{
			if(string.IsNullOrEmpty(s)) return fDefault;
			float f;
			if(float.TryParse(s.Trim(), NumberStyles.Float, Inv, out f) &&
				!float.IsNaN(f) && !float.IsInfinity(f)) return f;
			return fDefault;
		}

		private static float ParseLength(string s, float fDefault)
		{
			if(string.IsNullOrEmpty(s)) return fDefault;
			string t = s.Trim().ToLowerInvariant();
			string[] vUnits = new string[] { "px", "pt", "em", "mm", "cm", "in", "%" };
			foreach(string strUnit in vUnits)
			{
				if(t.EndsWith(strUnit, StringComparison.Ordinal))
				{
					t = t.Substring(0, t.Length - strUnit.Length);
					break;
				}
			}
			return ParseFloat(t, fDefault);
		}

		private static float ParseOpacity(string s, float fDefault)
		{
			if(string.IsNullOrEmpty(s)) return fDefault;
			string t = s.Trim();
			float f;
			if(t.EndsWith("%", StringComparison.Ordinal))
				f = ParseFloat(t.Substring(0, t.Length - 1), fDefault * 100f) / 100f;
			else f = ParseFloat(t, fDefault);
			return Math.Max(0f, Math.Min(1f, f));
		}

		private static float ParseOffset(string s)
		{
			if(string.IsNullOrEmpty(s)) return 0f;
			string t = s.Trim();
			if(t.EndsWith("%", StringComparison.Ordinal))
				return ParseFloat(t.Substring(0, t.Length - 1), 0f) / 100f;
			return ParseFloat(t, 0f);
		}

		private static float[] ParseNumberList(string s)
		{
			List<float> l = new List<float>();
			if(string.IsNullOrEmpty(s)) return l.ToArray();
			SvgPathParser.Tokenizer tk = new SvgPathParser.Tokenizer(s);
			float f;
			while(tk.TryReadNumber(out f)) l.Add(f);
			return l.ToArray();
		}

		private static Matrix ParseTransform(string s)
		{
			if(string.IsNullOrEmpty(s)) return null;

			Matrix m = new Matrix();
			int i = 0;
			while(i < s.Length)
			{
				while((i < s.Length) && (char.IsWhiteSpace(s[i]) || (s[i] == ','))) ++i;
				int iOpen = s.IndexOf('(', i);
				if(iOpen < 0) break;
				int iClose = s.IndexOf(')', iOpen);
				if(iClose < 0) break;

				string strName = s.Substring(i, iOpen - i).Trim();
				float[] v = ParseNumberList(s.Substring(iOpen + 1, iClose - iOpen - 1));
				i = iClose + 1;

				Matrix t = null;
				if((strName == "matrix") && (v.Length == 6))
					t = new Matrix(v[0], v[1], v[2], v[3], v[4], v[5]);
				else if((strName == "translate") && (v.Length >= 1))
					t = new Matrix(1f, 0f, 0f, 1f, v[0], (v.Length >= 2) ? v[1] : 0f);
				else if((strName == "scale") && (v.Length >= 1))
					t = new Matrix(v[0], 0f, 0f, (v.Length >= 2) ? v[1] : v[0], 0f, 0f);
				else if((strName == "rotate") && (v.Length >= 1))
				{
					t = new Matrix();
					if(v.Length >= 3) t.RotateAt(v[0], new PointF(v[1], v[2]));
					else t.Rotate(v[0]);
				}
				else if((strName == "skewX") && (v.Length >= 1))
					t = new Matrix(1f, 0f, (float)Math.Tan(v[0] * Math.PI / 180.0), 1f, 0f, 0f);
				else if((strName == "skewY") && (v.Length >= 1))
					t = new Matrix(1f, (float)Math.Tan(v[0] * Math.PI / 180.0), 0f, 1f, 0f, 0f);

				if(t != null)
				{
					// "A B" means: apply B first, then A
					m.Multiply(t, MatrixOrder.Prepend);
					t.Dispose();
				}
			}
			return m;
		}

		// ===================== Colors =====================

		private static bool TryParseColor(RenderContext ctx, Style st, string s, out Color c)
		{
			c = Color.Black;
			if(string.IsNullOrEmpty(s)) return false;
			string t = s.Trim();
			string tl = t.ToLowerInvariant();

			if((tl == "none") || (tl == "transparent")) { c = Color.Transparent; return tl != "none"; }

			if(tl == "currentcolor")
			{
				if((st != null) && !string.IsNullOrEmpty(st.Color) &&
					(st.Color.ToLowerInvariant() != "currentcolor"))
					return TryParseColor(ctx, null, st.Color, out c);
				c = ctx.CurrentColor;
				return true;
			}

			if(tl.StartsWith("#", StringComparison.Ordinal))
			{
				string h = tl.Substring(1);
				if((h.Length == 3) || (h.Length == 4))
				{
					StringBuilder sb = new StringBuilder();
					foreach(char ch in h) { sb.Append(ch); sb.Append(ch); }
					h = sb.ToString();
				}
				int v;
				if((h.Length == 6) && int.TryParse(h, NumberStyles.HexNumber, Inv, out v))
				{
					c = Color.FromArgb(255, (v >> 16) & 0xFF, (v >> 8) & 0xFF, v & 0xFF);
					return true;
				}
				if(h.Length == 8)
				{
					uint u;
					if(uint.TryParse(h, NumberStyles.HexNumber, Inv, out u))
					{
						c = Color.FromArgb((int)(u & 0xFF), (int)((u >> 24) & 0xFF),
							(int)((u >> 16) & 0xFF), (int)((u >> 8) & 0xFF));
						return true;
					}
				}
				return false;
			}

			if(tl.StartsWith("rgb", StringComparison.Ordinal))
			{
				int iOpen = tl.IndexOf('('), iClose = tl.IndexOf(')');
				if((iOpen < 0) || (iClose < iOpen)) return false;
				string[] v = tl.Substring(iOpen + 1, iClose - iOpen - 1).Split(
					new char[] { ',', ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
				if(v.Length < 3) return false;

				int[] rgb = new int[3];
				for(int i = 0; i < 3; ++i)
				{
					string p = v[i].Trim();
					float f;
					if(p.EndsWith("%", StringComparison.Ordinal))
						f = ParseFloat(p.Substring(0, p.Length - 1), 0f) * 2.55f;
					else f = ParseFloat(p, 0f);
					rgb[i] = Math.Max(0, Math.Min(255, (int)Math.Round(f)));
				}
				int a = 255;
				if(v.Length >= 4) a = (int)Math.Round(ParseOpacity(v[3], 1f) * 255f);
				c = Color.FromArgb(a, rgb[0], rgb[1], rgb[2]);
				return true;
			}

			// Named colors (CSS names match .NET's KnownColor names for the
			// common cases; system color names are rejected)
			foreach(char ch in tl) { if(!char.IsLetter(ch)) return false; }
			Color cNamed = Color.FromName(t);
			if(cNamed.IsKnownColor && !cNamed.IsSystemColor)
			{
				c = Color.FromArgb(cNamed.A, cNamed.R, cNamed.G, cNamed.B);
				return true;
			}
			return false;
		}
	}
}
