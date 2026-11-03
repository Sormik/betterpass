/*
  BetterPass - KeePass plugin
  SVG path data ("d" attribute) to GDI+ GraphicsPath.
  Supports M L H V C S Q T A Z (absolute and relative).
*/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace BetterPass
{
	internal static class SvgPathParser
	{
		private const int MaxCommands = 200000;

		internal sealed class Tokenizer
		{
			private readonly string m_s;
			private int m_i = 0;

			public Tokenizer(string s) { m_s = (s ?? string.Empty); }

			public bool AtEnd
			{
				get { SkipSeparators(); return (m_i >= m_s.Length); }
			}

			private void SkipSeparators()
			{
				while((m_i < m_s.Length) && (char.IsWhiteSpace(m_s[m_i]) || (m_s[m_i] == ',')))
					++m_i;
			}

			public bool TryReadCommand(out char ch)
			{
				SkipSeparators();
				ch = '\0';
				if(m_i >= m_s.Length) return false;
				char c = m_s[m_i];
				if("MmLlHhVvCcSsQqTtAaZz".IndexOf(c) < 0) return false;
				ch = c;
				++m_i;
				return true;
			}

			public bool PeekIsNumber()
			{
				SkipSeparators();
				if(m_i >= m_s.Length) return false;
				char c = m_s[m_i];
				return (char.IsDigit(c) || (c == '-') || (c == '+') || (c == '.'));
			}

			// Arc flags are single characters "0" or "1", possibly not separated
			public bool TryReadFlag(out bool b)
			{
				SkipSeparators();
				b = false;
				if(m_i >= m_s.Length) return false;
				char c = m_s[m_i];
				if((c != '0') && (c != '1')) return false;
				b = (c == '1');
				++m_i;
				return true;
			}

			public bool TryReadNumber(out float f)
			{
				SkipSeparators();
				f = 0f;
				int iStart = m_i;
				int n = m_s.Length;
				if(m_i >= n) return false;

				if((m_s[m_i] == '-') || (m_s[m_i] == '+')) ++m_i;

				bool bDigits = false, bDot = false;
				while(m_i < n)
				{
					char c = m_s[m_i];
					if(char.IsDigit(c)) { bDigits = true; ++m_i; }
					else if((c == '.') && !bDot) { bDot = true; ++m_i; }
					else break;
				}
				if(!bDigits) { m_i = iStart; return false; }

				if((m_i < n) && ((m_s[m_i] == 'e') || (m_s[m_i] == 'E')))
				{
					int iExp = m_i;
					++m_i;
					if((m_i < n) && ((m_s[m_i] == '-') || (m_s[m_i] == '+'))) ++m_i;
					bool bExpDigits = false;
					while((m_i < n) && char.IsDigit(m_s[m_i])) { bExpDigits = true; ++m_i; }
					if(!bExpDigits) m_i = iExp;
				}

				string str = m_s.Substring(iStart, m_i - iStart);
				if(!float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out f))
				{
					m_i = iStart;
					return false;
				}
				if(float.IsNaN(f) || float.IsInfinity(f)) f = 0f;
				return true;
			}
		}

		public static void Parse(string strData, GraphicsPath gp)
		{
			if(string.IsNullOrEmpty(strData)) return;

			Tokenizer tk = new Tokenizer(strData);
			PointF ptCur = PointF.Empty, ptStart = PointF.Empty;
			PointF ptLastCtrl = PointF.Empty;
			char chPrev = '\0';
			char ch;
			bool bFigureOpen = false;
			int nCmds = 0;

			while(tk.TryReadCommand(out ch))
			{
				bool bRel = char.IsLower(ch);
				char chU = char.ToUpperInvariant(ch);
				bool bFirst = true;

				if(chU == 'Z')
				{
					if(bFigureOpen) gp.CloseFigure();
					bFigureOpen = false;
					ptCur = ptStart;
					chPrev = 'Z';
					continue;
				}

				// A command letter may be followed by several argument sets
				while(bFirst || tk.PeekIsNumber())
				{
					if(++nCmds > MaxCommands) return;

					float x, y, x1, y1, x2, y2;
					switch(chU)
					{
						case 'M':
							if(!tk.TryReadNumber(out x) || !tk.TryReadNumber(out y)) return;
							if(bRel) { x += ptCur.X; y += ptCur.Y; }
							if(bFirst)
							{
								gp.StartFigure();
								bFigureOpen = true;
								ptStart = new PointF(x, y);
							}
							else gp.AddLine(ptCur, new PointF(x, y)); // implicit lineto
							ptCur = new PointF(x, y);
							break;

						case 'L':
							if(!tk.TryReadNumber(out x) || !tk.TryReadNumber(out y)) return;
							if(bRel) { x += ptCur.X; y += ptCur.Y; }
							gp.AddLine(ptCur, new PointF(x, y));
							ptCur = new PointF(x, y);
							break;

						case 'H':
							if(!tk.TryReadNumber(out x)) return;
							if(bRel) x += ptCur.X;
							gp.AddLine(ptCur, new PointF(x, ptCur.Y));
							ptCur = new PointF(x, ptCur.Y);
							break;

						case 'V':
							if(!tk.TryReadNumber(out y)) return;
							if(bRel) y += ptCur.Y;
							gp.AddLine(ptCur, new PointF(ptCur.X, y));
							ptCur = new PointF(ptCur.X, y);
							break;

						case 'C':
							if(!tk.TryReadNumber(out x1) || !tk.TryReadNumber(out y1) ||
								!tk.TryReadNumber(out x2) || !tk.TryReadNumber(out y2) ||
								!tk.TryReadNumber(out x) || !tk.TryReadNumber(out y)) return;
							if(bRel)
							{
								x1 += ptCur.X; y1 += ptCur.Y; x2 += ptCur.X; y2 += ptCur.Y;
								x += ptCur.X; y += ptCur.Y;
							}
							gp.AddBezier(ptCur, new PointF(x1, y1), new PointF(x2, y2), new PointF(x, y));
							ptLastCtrl = new PointF(x2, y2);
							ptCur = new PointF(x, y);
							break;

						case 'S':
						{
							if(!tk.TryReadNumber(out x2) || !tk.TryReadNumber(out y2) ||
								!tk.TryReadNumber(out x) || !tk.TryReadNumber(out y)) return;
							if(bRel) { x2 += ptCur.X; y2 += ptCur.Y; x += ptCur.X; y += ptCur.Y; }
							PointF c1 = ptCur;
							if((chPrev == 'C') || (chPrev == 'S'))
								c1 = new PointF(2f * ptCur.X - ptLastCtrl.X, 2f * ptCur.Y - ptLastCtrl.Y);
							gp.AddBezier(ptCur, c1, new PointF(x2, y2), new PointF(x, y));
							ptLastCtrl = new PointF(x2, y2);
							ptCur = new PointF(x, y);
							break;
						}

						case 'Q':
						{
							if(!tk.TryReadNumber(out x1) || !tk.TryReadNumber(out y1) ||
								!tk.TryReadNumber(out x) || !tk.TryReadNumber(out y)) return;
							if(bRel) { x1 += ptCur.X; y1 += ptCur.Y; x += ptCur.X; y += ptCur.Y; }
							AddQuadratic(gp, ptCur, new PointF(x1, y1), new PointF(x, y));
							ptLastCtrl = new PointF(x1, y1);
							ptCur = new PointF(x, y);
							break;
						}

						case 'T':
						{
							if(!tk.TryReadNumber(out x) || !tk.TryReadNumber(out y)) return;
							if(bRel) { x += ptCur.X; y += ptCur.Y; }
							PointF q = ptCur;
							if((chPrev == 'Q') || (chPrev == 'T'))
								q = new PointF(2f * ptCur.X - ptLastCtrl.X, 2f * ptCur.Y - ptLastCtrl.Y);
							AddQuadratic(gp, ptCur, q, new PointF(x, y));
							ptLastCtrl = q;
							ptCur = new PointF(x, y);
							break;
						}

						case 'A':
						{
							float rx, ry, fRot;
							bool bLarge, bSweep;
							if(!tk.TryReadNumber(out rx) || !tk.TryReadNumber(out ry) ||
								!tk.TryReadNumber(out fRot) || !tk.TryReadFlag(out bLarge) ||
								!tk.TryReadFlag(out bSweep) || !tk.TryReadNumber(out x) ||
								!tk.TryReadNumber(out y)) return;
							if(bRel) { x += ptCur.X; y += ptCur.Y; }
							AddArc(gp, ptCur, rx, ry, fRot, bLarge, bSweep, new PointF(x, y));
							ptCur = new PointF(x, y);
							break;
						}

						default:
							return;
					}

					// After the first M pair, further pairs are linetos
					chPrev = ((chU == 'M') ? 'L' : chU);
					bFirst = false;
				}
			}
		}

		private static void AddQuadratic(GraphicsPath gp, PointF p0, PointF q, PointF p)
		{
			PointF c1 = new PointF(p0.X + 2f / 3f * (q.X - p0.X), p0.Y + 2f / 3f * (q.Y - p0.Y));
			PointF c2 = new PointF(p.X + 2f / 3f * (q.X - p.X), p.Y + 2f / 3f * (q.Y - p.Y));
			gp.AddBezier(p0, c1, c2, p);
		}

		// Endpoint parameterization -> center parameterization -> cubic Beziers
		// (SVG 1.1, appendix F.6)
		private static void AddArc(GraphicsPath gp, PointF p1, float fRx, float fRy,
			float fRotDeg, bool bLarge, bool bSweep, PointF p2)
		{
			if((p1.X == p2.X) && (p1.Y == p2.Y)) return;

			double rx = Math.Abs(fRx), ry = Math.Abs(fRy);
			if((rx < 1e-9) || (ry < 1e-9)) { gp.AddLine(p1, p2); return; }

			double phi = fRotDeg * Math.PI / 180.0;
			double cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);

			double dx2 = (p1.X - p2.X) / 2.0, dy2 = (p1.Y - p2.Y) / 2.0;
			double x1p = cosPhi * dx2 + sinPhi * dy2;
			double y1p = -sinPhi * dx2 + cosPhi * dy2;

			double lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
			if(lambda > 1.0)
			{
				double s = Math.Sqrt(lambda);
				rx *= s;
				ry *= s;
			}

			double num = rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p;
			double den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
			double coef = ((den > 0.0) ? Math.Sqrt(Math.Max(0.0, num / den)) : 0.0);
			if(bLarge == bSweep) coef = -coef;

			double cxp = coef * (rx * y1p / ry);
			double cyp = coef * -(ry * x1p / rx);

			double cx = cosPhi * cxp - sinPhi * cyp + (p1.X + p2.X) / 2.0;
			double cy = sinPhi * cxp + cosPhi * cyp + (p1.Y + p2.Y) / 2.0;

			double theta1 = VectorAngle(1.0, 0.0, (x1p - cxp) / rx, (y1p - cyp) / ry);
			double dTheta = VectorAngle((x1p - cxp) / rx, (y1p - cyp) / ry,
				(-x1p - cxp) / rx, (-y1p - cyp) / ry);

			if(!bSweep && (dTheta > 0.0)) dTheta -= 2.0 * Math.PI;
			else if(bSweep && (dTheta < 0.0)) dTheta += 2.0 * Math.PI;

			int nSegs = (int)Math.Ceiling(Math.Abs(dTheta) / (Math.PI / 2.0) - 1e-7);
			if(nSegs < 1) nSegs = 1;
			double delta = dTheta / nSegs;
			double t = 4.0 / 3.0 * Math.Tan(delta / 4.0);

			double theta = theta1;
			PointF ptPrev = p1;
			for(int i = 0; i < nSegs; ++i)
			{
				double cos1 = Math.Cos(theta), sin1 = Math.Sin(theta);
				double theta2 = theta + delta;
				double cos2 = Math.Cos(theta2), sin2 = Math.Sin(theta2);

				// Points on the unit circle, then scaled, rotated, translated
				double ex1 = cos1 - t * sin1, ey1 = sin1 + t * cos1;
				double ex2 = cos2 + t * sin2, ey2 = sin2 - t * cos2;

				PointF c1 = MapArcPoint(ex1, ey1, rx, ry, cosPhi, sinPhi, cx, cy);
				PointF c2 = MapArcPoint(ex2, ey2, rx, ry, cosPhi, sinPhi, cx, cy);
				PointF pe = ((i == nSegs - 1) ? p2 :
					MapArcPoint(cos2, sin2, rx, ry, cosPhi, sinPhi, cx, cy));

				gp.AddBezier(ptPrev, c1, c2, pe);
				ptPrev = pe;
				theta = theta2;
			}
		}

		private static PointF MapArcPoint(double ux, double uy, double rx, double ry,
			double cosPhi, double sinPhi, double cx, double cy)
		{
			double x = ux * rx, y = uy * ry;
			return new PointF((float)(cosPhi * x - sinPhi * y + cx),
				(float)(sinPhi * x + cosPhi * y + cy));
		}

		private static double VectorAngle(double ux, double uy, double vx, double vy)
		{
			double dot = ux * vx + uy * vy;
			double len = Math.Sqrt(ux * ux + uy * uy) * Math.Sqrt(vx * vx + vy * vy);
			if(len < 1e-12) return 0.0;
			double a = Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot / len)));
			if(ux * vy - uy * vx < 0.0) a = -a;
			return a;
		}
	}
}
