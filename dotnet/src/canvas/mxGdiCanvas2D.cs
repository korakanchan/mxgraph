// $Id: mxGdiCanvas2D.cs,v 1.18 2012/11/19 16:56:51 gaudenz Exp $
// Copyright (c) 2007-2008, Gaudenz Alder
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace com.mxgraph
{
    /// <summary>
    /// Used for exporting images.
    /// <example>To render to an image from a given XML string, graph size and
    /// and background color, the following code is used:
    /// <code>
    /// Image image = mxUtils.CreateImage(width, height, background);
    /// Graphics g = Graphics.FromImage(image);
    /// g.SmoothingMode = SmoothingMode.HighQuality;
    /// mxSaxOutputHandler handler = new mxSaxOutputHandler(new mxGdiCanvas2D(g));
    /// handler.Read(new XmlTextReader(new StringReader(xml)));
    /// </code>
    /// </example>
    /// Text rendering is available for plain text only, with optional word wrapping.
    /// </summary>
    public class mxGdiCanvas2D : mxICanvas2D
    {
	    /// <summary>
        /// Reference to the graphics instance for painting.
	    /// </summary>
	    protected Graphics graphics;

	    /// <summary>
        /// Represents the current state of the canvas.
	    /// </summary>
	    protected CanvasState state = new CanvasState();

	    /// <summary>
        /// Stack of states for save/restore.
	    /// </summary>
	    protected Stack<CanvasState> stack = new Stack<CanvasState>();

	    /// <summary>
        /// Holds the current path.
	    /// </summary>
	    protected GraphicsPath currentPath;

	    /// <summary>
	    /// Holds the last point of a moveTo or lineTo operation to determine if the
        /// current path is orthogonal.
	    /// </summary>
	    protected mxPoint lastPoint;

	    /// <summary>
	    /// Holds the current value for the shadow color. This is used to hold the
        /// input value of a shadow operation. The parsing result of this value is
        /// cached in the global scope as it should be repeating.
	    /// </summary>
	    protected string currentShadowValue;

	    /// <summary>
	    /// Holds the current parsed shadow color. This holds the result of parsing
        /// the currentShadowValue, which is an expensive operation.
	    /// </summary>
	    protected Color currentShadowColor;

	    /// <summary>
        /// Constructs a new graphics export canvas.
	    /// </summary>
	    public mxGdiCanvas2D(Graphics g)
	    {
            Graphics = g;
	    }

	    /// <summary>
        /// Sets the graphics instance.
	    /// </summary>
        Graphics Graphics
        {
            get { return graphics; }
            set { graphics = value; }
        }

	    /// <summary>
        /// Saves the current canvas state.
	    /// </summary>
	    public void Save()
	    {
            state.state = graphics.Save();
		    stack.Push(state);
		    state = (CanvasState) state.Clone();
	    }

	    /// <summary>
        /// Restores the last canvas state.
	    /// </summary>
	    public void Restore()
	    {
		    state = stack.Pop();
            graphics.Restore(state.state);
	    }

	    /// <summary>
	    /// Sets the given scale.
	    /// </summary>
	    /// <param name="value"></param>
	    public void Scale(double value)
	    {
		    // This implementation uses custom scale/translate and built-in rotation
		    state.scale = state.scale * value;
            state.strokeWidth *= value;
	    }

	    /// <summary>
	    /// Translates the canvas.
	    /// </summary>
	    /// <param name="dx"></param>
	    /// <param name="dy"></param>
	    public void Translate(double dx, double dy)
	    {
		    // This implementation uses custom scale/translate and built-in rotation
		    state.dx += dx;
		    state.dy += dy;
	    }

	    /// <summary>
	    /// Rotates the canvas.
	    /// </summary>
	    public void Rotate(double theta, bool flipH, bool flipV, double cx,
			    double cy)
	    {
            cx *= state.scale;
            cy *= state.scale;

            // This is a special case where the rotation center is scaled so dx/dy,
            // which are also scaled, must be applied after scaling the center.
            cx += state.dx;
            cy += state.dy;

		    // This implementation uses custom scale/translate and built-in rotation
		    // Rotation state is part of the AffineTransform in state.transform
		    if (flipH ^ flipV)
		    {
			    double tx = (flipH) ? cx : 0;
			    int sx = (flipH) ? -1 : 1;

			    double ty = (flipV) ? cy : 0;
			    int sy = (flipV) ? -1 : 1;

                graphics.TranslateTransform((float)tx, (float)ty, MatrixOrder.Append);
                graphics.ScaleTransform(sx, sy, MatrixOrder.Append);
                graphics.TranslateTransform((float)-tx, (float)-ty, MatrixOrder.Append);
		    }

            graphics.TranslateTransform((float)-cx, (float)-cy, MatrixOrder.Append);
            graphics.RotateTransform((float)theta, MatrixOrder.Append);
            graphics.TranslateTransform((float)cx, (float)cy, MatrixOrder.Append);
	    }

	    /// <summary>
	    /// Sets the strokewidth.
	    /// </summary>
        public double StrokeWidth
        {
            set
            {
                // Lazy and cached instantiation strategy for all stroke properties
		        if (value * state.scale != state.strokeWidth)
		        {
			        state.strokeWidth = value * state.scale;

			        // Invalidates cached pen
                    state.pen = null;
		        }
            }
        }

	    /// <summary>
        /// Caches color conversion as it is expensive.
	    /// </summary>
        public string StrokeColor
        {
            set
            {
                // Lazy and cached instantiation strategy for all stroke properties
		        if (!state.strokeColorValue.Equals(value))
		        {
			        state.strokeColorValue = value;
			        state.strokeColor = null;
                    state.pen = null;
		        }
            }
        }

	    /// <summary>
	    /// Specifies if lines are dashed.
	    /// </summary>
        public bool Dashed
        {
            set
            {
                // Lazy and cached instantiation strategy for all stroke properties
		        if (value != state.dashed)
		        {
			        state.dashed = value;

			        // Invalidates cached pen
                    state.pen = null;
		        }
            }
        }

	    /// <summary>
	    /// Sets the dashpattern.
	    /// </summary>
        public string DashPattern
        {
            set
            {
		        if (value != null && !state.dashPattern.Equals(value) && value.Length > 0)
		        {
			        String[] tokens = value.Split(' ');
                    float[] dashpattern = new float[tokens.Length];

			        for (int i = 0; i < tokens.Length; i++)
			        {
				        dashpattern[i] = (float) (float.Parse(tokens[i]));
			        }

			        state.dashPattern = dashpattern;
                    state.pen = null;
		        }
            }
        }

	    /// <summary>
	    /// Sets the linecap.
	    /// </summary>
        public string LineCap
        {
            set
            {
		        if (!state.lineCap.Equals(value))
		        {
			        state.lineCap = value;
                    state.pen = null;
		        }
            }
	    }

	    /// <summary>
	    /// Sets the linejoin.
	    /// </summary>
        public string LineJoin
        {
            set
            {
		        if (!state.lineJoin.Equals(value))
		        {
			        state.lineJoin = value;
                    state.pen = null;
		        }
            }
	    }

	    /// <summary>
	    /// Sets the miterlimit.
	    /// </summary>
        public double MiterLimit
        {
            set
            {
		        if (value != state.miterLimit)
		        {
			        state.miterLimit = value;
                    state.pen = null;
		        }
            }
	    }

	    /// <summary>
	    /// Sets the fontsize.
	    /// </summary>
        public double FontSize
        {
            set
            {
		        if (value != state.fontSize)
		        {
			        state.fontSize = value * state.scale;
			        state.font = null;
		        }
            }
	    }

	    /// <summary>
	    /// Sets the fontcolor.
	    /// </summary>
        public string FontColor
        {
            set
            {
		        if (!state.fontColorValue.Equals(value))
		        {
			        state.fontColorValue = value;
			        state.fontBrush = null;
		        }
            }
	    }

	    /// <summary>
	    /// Sets the font family.
	    /// </summary>
        public string FontFamily
        {
            set
            {
		        if (!state.fontFamily.Equals(value))
		        {
			        state.fontFamily = value;
			        state.font = null;
		        }
            }
	    }

	    /// <summary>
	    /// Sets the given fontstyle.
	    /// </summary>
        public int FontStyle
        {
            set
            {
		        if (value != state.fontStyle)
		        {
			        state.fontStyle = value;
			        state.font = null;
		        }
            }
	    }

	    /// <summary>
	    /// Sets the given alpha.
	    /// </summary>
        public double Alpha
        {
            set
            {
                state.alpha = value;
            }
	    }

	    /// <summary>
	    /// Sets the given fillcolor.
	    /// </summary>
        public string FillColor
        {
            set
            {
                state.brush = new SolidBrush(ParseColor(value));
            }
	    }

	    /// <summary>
	    /// Sets the given gradient.
	    /// </summary>
	    public void SetGradient(String color1, String color2, double x, double y,
			    double w, double h, String direction)
	    {
		    // LATER: Add lazy instantiation and check if paint already created
            x = state.dx + x * state.scale;
		    y = state.dy + y * state.scale;
		    h *= state.scale;
		    w *= state.scale;

            // FIXME: Needs to swap colors and use only horizontal and vertical
            LinearGradientMode mode = LinearGradientMode.ForwardDiagonal;

            if (direction != null && direction.Length > 0
                    && !direction.Equals(mxConstants.DIRECTION_SOUTH))
            {
                if (direction.Equals(mxConstants.DIRECTION_EAST))
                {
                    mode = LinearGradientMode.BackwardDiagonal;
                }
                else if (direction.Equals(mxConstants.DIRECTION_NORTH))
                {
                    mode = LinearGradientMode.Horizontal;
                }
                else if (direction.Equals(mxConstants.DIRECTION_WEST))
                {
                    mode = LinearGradientMode.Vertical;
                }
            }

            state.brush = new LinearGradientBrush(new RectangleF((float) x, (float) y,
                (float) w, (float) h), ParseColor(color1), ParseColor(color2), mode);
	    }

	    /// <summary>
	    /// Helper method that uses {@link mxUtils#parseColor(String)}. Subclassers
        /// can override this to implement caching for frequently used colors.
	    /// </summary>
	    protected Color ParseColor(string hex)
	    {
            Color color = ColorTranslator.FromHtml(hex);

            // Poor man's setAlpha
            color = Color.FromArgb((int) (state.alpha * 255), color.R, color.G, color.B);

            return color;
	    }

	    /// <summary>
	    /// Sets the given glass gradient.
	    /// </summary>
	    public void SetGlassGradient(double x, double y, double w, double h)
	    {
		    double size = 0.6;
		    x = state.dx + x * state.scale;
		    y = state.dy + y * state.scale;
		    h *= state.scale;
		    w *= state.scale;

            state.brush = new LinearGradientBrush(new RectangleF((float) x, (float) y,
                (float) w, (float) (size * h)), Color.FromArgb((int)(0.9 * 255), 255, 255, 255),
                Color.FromArgb((int)(0.15 * 255), 255, 255, 255), LinearGradientMode.Vertical);
	    }

	    /// <summary>
	    /// Draws a rectangle.
	    /// </summary>
	    public void Rect(double x, double y, double w, double h)
	    {
		    currentPath = new GraphicsPath();
		    currentPath.AddRectangle(new RectangleF((float) (state.dx + x * state.scale),
                    (float) (state.dy + y * state.scale), (float) (w * state.scale),
                    (float) (h * state.scale)));
	    }

	    /// <summary>
        /// Draws a rounded rectangle.
	    /// </summary>
	    public void Roundrect(double x, double y, double w, double h, double dx,
			    double dy)
	    {
		    Begin();
		    MoveTo(x + dx, y);
		    LineTo(x + w - dx, y);
		    QuadTo(x + w, y, x + w, y + dy);
		    LineTo(x + w, y + h - dy);
		    QuadTo(x + w, y + h, x + w - dx, y + h);
		    LineTo(x + dx, y + h);
		    QuadTo(x, y + h, x, y + h - dy);
		    LineTo(x, y + dy);
		    QuadTo(x, y, x + dx, y);
	    }

	    /// <summary>
	    /// Draws an ellipse.
	    /// </summary>
	    public void Ellipse(double x, double y, double w, double h)
	    {
		    currentPath = new GraphicsPath();
		    currentPath.AddEllipse((float) (state.dx + x * state.scale),
                    (float) (state.dy + y * state.scale), (float) (w * state.scale),
                    (float) (h * state.scale));
	    }

	    /// <summary>
	    /// Draws an image.
	    /// </summary>
	    public void Image(double x, double y, double w, double h, String src,
			    bool aspect, bool flipH, bool flipV)
	    {
		    if (src != null && w > 0 && h > 0)
		    {
			    Image image = LoadImage(src);

                if (image != null)
			    {
                    GraphicsState previous = graphics.Save();
                    Rectangle bounds = GetImageBounds(image, x, y, w, h, aspect);
                    ConfigureImageGraphics(bounds.X, bounds.Y, bounds.Width,
                        bounds.Height, flipH, flipV);
                    DrawImage(image, bounds);
                    graphics.Restore(previous);
			    }
		    }
	    }

        /// <summary>
        /// Implements the call to the graphics API.
        /// </summary>
        protected void DrawImage(Image image, Rectangle bounds)
        {
            graphics.DrawImage(image, bounds);
        }

	    /// <summary>
	    /// Loads the specified image.
	    /// </summary>
	    protected Image LoadImage(String src)
	    {
		    return mxUtils.LoadImage(src);
	    }

	    /// <summary>
	    /// Returns the bounds for the given image.
	    /// </summary>
	    protected Rectangle GetImageBounds(Image img, double x, double y,
			    double w, double h, bool aspect)
	    {
		    x = state.dx + x * state.scale;
		    y = state.dy + y * state.scale;
		    w *= state.scale;
		    h *= state.scale;

		    if (aspect)
		    {
                Size size = GetImageSize(img);
                double s = Math.Min(w / size.Width, h / size.Height);
                int sw = (int)Math.Round(size.Width * s);
                int sh = (int)Math.Round(size.Height * s);
                x += (w - sw) / 2;
                y += (h - sh) / 2;
                w = sw;
                h = sh;
		    }
		    else
		    {
			    w = Math.Round(w);
			    h = Math.Round(h);
		    }

		    return new Rectangle((int) x, (int) y, (int) w, (int) h);
	    }

        /// <summary>
        /// Returns the size for the given image.
        /// </summary>
        protected Size GetImageSize(Image image)
        {
            return new Size(image.Width, image.Height);
        }

	    /// <summary>
        /// Creates a graphic instance for rendering an image.
	    /// </summary>
	    protected void ConfigureImageGraphics(double x, double y,
			    double w, double h, bool flipH, bool flipV)
	    {
		    if (flipH || flipV)
		    {
			    int sx = 1;
			    int sy = 1;
			    int dx = 0;
			    int dy = 0;

			    if (flipH)
			    {
				    sx = -1;
				    dx = (int) (-w - 2 * x);
			    }

			    if (flipV)
			    {
				    sy = -1;
				    dy = (int) (-h - 2 * y);
			    }

                graphics.ScaleTransform(sx, sy, MatrixOrder.Append);
                graphics.TranslateTransform(dx, dy, MatrixOrder.Append);
            }
	    }

	    /// <summary>
        /// Draws the given text.
	    /// </summary>
	    public void Text(double x, double y, double w, double h, string str, string align,
                string valign, bool vertical, bool wrap, string format)
	    {
		    if (!state.fontColorValue.Equals(mxConstants.NONE))
		    {
                // HTML format is currently not supported so all BR are
                // replaced with linefeeds for minimal support
                if (format != null && format.Equals("html"))
                {
                    str = str.Replace("<br/>", "\n");
                    str = str.Replace("<br>", "\n");
                }

			    x = state.dx + x * state.scale;
			    y = state.dy + y * state.scale;
			    w *= state.scale;
			    h *= state.scale;

			    // Font-metrics needed below this line
                GraphicsState previous = graphics.Save();
                UpdateFont();

                if (vertical)
                {
                    graphics.TranslateTransform((float)-(x + w / 2), (float)-(y + h / 2), MatrixOrder.Append);
                    graphics.RotateTransform(-90F, MatrixOrder.Append);
                    graphics.TranslateTransform((float)(x + w / 2), (float)(y + h / 2), MatrixOrder.Append);
                }

                if (state.fontBrush == null)
                {
                    state.fontBrush = new SolidBrush(ParseColor(state.fontColorValue));
                }

			    y = GetVerticalTextPosition(x, y, w, h, align, valign, vertical, state.font, str, wrap, format);
                x = GetHorizontalTextPosition(x, y, w, h, align, valign, vertical, state.font, str, wrap, format);
                w = GetTextWidth(x, y, w, h, align, valign, vertical, state.font, str, wrap, format);

                RectangleF bounds = new RectangleF((float)x, (float)y, (float)w, (float)h);
                StringFormat fmt = CreateStringFormat(align, valign, wrap);

                // Uses built-in rendering if wrapping is enabled because we do not know what
                // additional linefeeds have been added so we can't compute the y increment
                if (wrap)
                {
                    graphics.DrawString(str, state.font, state.fontBrush, bounds, fmt);
                }
                else
                {
                    // Needed for custom linespacing
                    string[] lines = str.Split('\n');

                    for (int i = 0; i < lines.Length; i++)
                    {
                        graphics.DrawString(lines[i], state.font, state.fontBrush, bounds, fmt);
                        bounds.Y += state.font.Height + mxConstants.LINESPACING;
                    }
                }

                graphics.Restore(previous);
		    }
	    }

        /// <summary>
        /// Returns the width to be used to render the specifies text.
        /// </summary>
        protected double GetTextWidth(double x, double y, double w, double h, string align,
            string valign, bool vertical, Font font, string text, bool wrap, string format)
        {
            if (wrap)
            {
                w += 10;
            }

            return w;
        }

        /// <summary>
        /// Default alignment is top.
        /// </summary>
        protected double GetVerticalTextPosition(double x, double y, double w, double h, string align,
            string valign, bool vertical, Font font, string text, bool wrap, string format)
        {
            return y - 2;
        }

        /// <summary>
        /// Default alignment is left.
        /// </summary>
        protected double GetHorizontalTextPosition(double x, double y, double w,
                double h, string align, string valign, bool vertical,
                Font font, string text, bool wrap, string format)
        {
            // Left is default
            if (align == null)
            {
                x -= 2;
            }
            else if (align.Equals(mxConstants.ALIGN_CENTER))
            {
                x -= 6;
            }
            else if (align.Equals(mxConstants.ALIGN_RIGHT))
            {
                x -= 10;
            }

            return x;
        }

        /// <summary>
        /// Creates the specified string format.
        /// </summary>
        public static StringFormat CreateStringFormat(string align, string valign, bool wrap)
        {
            StringFormat format = new StringFormat(StringFormatFlags.NoClip);
            format.Trimming = StringTrimming.None;

            // This is not required as the rectangle for the text will take this flag into account.
            // However, we want to avoid any possible word-wrap unless explicitely specified.
            if (!wrap)
            {
                format.FormatFlags |= StringFormatFlags.NoWrap;
            }

            if (align == null || align.Equals(mxConstants.ALIGN_LEFT))
            {
                format.Alignment = StringAlignment.Near;
            }
            else if (align.Equals(mxConstants.ALIGN_CENTER))
            {
                format.Alignment = StringAlignment.Center;                
            }
            else if (align.Equals(mxConstants.ALIGN_RIGHT))
            {
                format.Alignment = StringAlignment.Far;
            }

            if (valign == null || valign.Equals(mxConstants.ALIGN_TOP))
            {
                format.LineAlignment = StringAlignment.Near;
            }
            else if (valign.Equals(mxConstants.ALIGN_MIDDLE))
            {
                format.LineAlignment = StringAlignment.Center;
            }
            else if (valign.Equals(mxConstants.ALIGN_BOTTOM))
            {
                format.LineAlignment = StringAlignment.Far;
            }

            return format;
        }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void Begin()
	    {
		    currentPath = new GraphicsPath();
		    lastPoint = null;
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void MoveTo(double x, double y)
	    {
		    if (currentPath != null)
		    {
                // StartFigure avoids connection between last figure and new figure
                currentPath.StartFigure();
                lastPoint = new mxPoint(state.dx + x * state.scale, state.dy + y * state.scale);
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void LineTo(double x, double y)
	    {
		    if (currentPath != null)
		    {
                mxPoint nextPoint = new mxPoint(state.dx + x * state.scale, state.dy + y * state.scale);

                if (lastPoint != null)
                {
			        currentPath.AddLine((float) lastPoint.X, (float) lastPoint.Y,
                            (float) nextPoint.X, (float) nextPoint.Y);
                }

                lastPoint = nextPoint;
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void QuadTo(double x1, double y1, double x2, double y2)
	    {
		    if (currentPath != null)
		    {
                mxPoint nextPoint = new mxPoint(state.dx + x2 * state.scale,
                    state.dy + y2 * state.scale);

                if (lastPoint != null)
                {
            	    double cpx0 = lastPoint.X;
				    double cpy0 = lastPoint.Y;
				    double qpx1 = state.dx + x1 * state.scale;
				    double qpy1 = state.dy + y1 * state.scale;
    				
				    double cpx1 = cpx0 + 2f/3f * (qpx1 - cpx0);
				    double cpy1 = cpy0 + 2f/3f * (qpy1 - cpy0);
    				
				    double cpx2 = nextPoint.X + 2f/3f * (qpx1 - nextPoint.X);
				    double cpy2 = nextPoint.Y + 2f/3f * (qpy1 - nextPoint.Y);

                    currentPath.AddBezier((float)cpx0, (float)cpy0,
                        (float)cpx1, (float)cpy1, (float)cpx2, (float)cpy2,
                        (float)nextPoint.X, (float)nextPoint.Y);
                }

                lastPoint = nextPoint;
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void CurveTo(double x1, double y1, double x2, double y2, double x3,
			    double y3)
	    {
		    if (currentPath != null)
		    {
                mxPoint nextPoint = new mxPoint(state.dx + x3 * state.scale, state.dy + y3 * state.scale);

                if (lastPoint != null)
                {
                    currentPath.AddBezier((float)lastPoint.X, (float)lastPoint.Y,
                        (float)(state.dx + x1 * state.scale),
                        (float)(state.dy + y1 * state.scale),
                        (float)(state.dx + x2 * state.scale),
                        (float)(state.dy + y2 * state.scale),
                        (float)nextPoint.X, (float)nextPoint.Y);
                }

                lastPoint = nextPoint;
		    }
	    }

	    /// <summary>
        /// Closes the current path.
	    /// </summary>
	    public void Close()
	    {
		    if (currentPath != null)
		    {
			    currentPath.CloseFigure();
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void Stroke()
	    {
		    if (currentPath != null
				    && !state.strokeColorValue.Equals(mxConstants.NONE))
		    {
			    if (state.strokeColor == null)
			    {
				    state.strokeColor = ParseColor(state.strokeColorValue);
			    }

			    UpdatePen();
                graphics.DrawPath(state.pen, currentPath);
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void Fill()
	    {
		    if (currentPath != null && state.brush != null)
		    {
			    graphics.FillPath(state.brush, currentPath);
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void FillAndStroke()
	    {
		    Fill();
		    Stroke();
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    /// <param name="value"></param>
	    public void Shadow(String value, bool filled)
	    {
		    if (value != null && currentPath != null)
		    {
			    if (currentShadowColor == null || currentShadowValue == null
					    || !currentShadowValue.Equals(value))
			    {
				    currentShadowColor = ParseColor(value);
				    currentShadowValue = value;
			    }

                // LATER: Cache shadowPen and shadowBrush
                if (filled)
                {
                    Brush shadowBrush = new SolidBrush(currentShadowColor);
                    graphics.FillPath(shadowBrush, currentPath);
                }

                Pen shadowPen = new Pen(currentShadowColor, (float)state.strokeWidth);
                graphics.DrawPath(shadowPen, currentPath);
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    public void Clip()
	    {
		    if (currentPath != null)
		    {
                graphics.Clip = new Region(currentPath);
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    protected void UpdateFont()
	    {
		    if (state.font == null)
		    {
                FontStyle style = ((state.fontStyle & mxConstants.FONT_BOLD) == mxConstants.FONT_BOLD) ?
                    System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular;
                style |= ((state.fontStyle & mxConstants.FONT_ITALIC) == mxConstants.FONT_ITALIC) ?
                    System.Drawing.FontStyle.Italic : System.Drawing.FontStyle.Regular;
                style |= ((state.fontStyle & mxConstants.FONT_UNDERLINE) == mxConstants.FONT_UNDERLINE) ?
                    System.Drawing.FontStyle.Underline : System.Drawing.FontStyle.Regular;

                int size = (int)Math.Floor(state.fontSize * mxConstants.FONT_SIZEFACTOR);

			    state.font = CreateFont(state.fontFamily, style, size);
		    }
	    }

	    /// <summary>
        /// Hook for subclassers to implement font caching.
	    /// </summary>
        protected Font CreateFont(String family, FontStyle style, int size)
	    {
            return new Font(GetFontName(family), size, style);
	    }

        /// <summary>
        /// Returns a font name for the given font family.
        /// </summary>
        protected String GetFontName(String family)
        {
            if (family != null)
            {
                int comma = family.IndexOf(',');

                if (comma >= 0)
                {
                    family = family.Substring(0, comma);
                }
            }

            return family;
        }

	    /// <summary>
	    /// 
	    /// </summary>
	    protected void UpdatePen()
	    {
		    if (state.pen == null)
		    {
                state.pen = new Pen((Color)state.strokeColor, (float)state.strokeWidth);

                System.Drawing.Drawing2D.LineCap cap = System.Drawing.Drawing2D.LineCap.Flat;

			    if (state.lineCap.Equals("round"))
			    {
				    cap = System.Drawing.Drawing2D.LineCap.Round;
			    }
			    else if (state.lineCap.Equals("square"))
			    {
                    cap = System.Drawing.Drawing2D.LineCap.Square;
			    }

                state.pen.StartCap = cap;
                state.pen.EndCap = cap;

                System.Drawing.Drawing2D.LineJoin join = System.Drawing.Drawing2D.LineJoin.Miter;

			    if (state.lineJoin.Equals("round"))
			    {
                    join = System.Drawing.Drawing2D.LineJoin.Round;
			    }
			    else if (state.lineJoin.Equals("bevel"))
			    {
                    join = System.Drawing.Drawing2D.LineJoin.Bevel;
			    }

                state.pen.LineJoin = join;
                state.pen.MiterLimit = (float) state.miterLimit;

                if (state.dashed)
                {
                    float[] dash = new float[state.dashPattern.Length];

                    for (int i = 0; i < dash.Length; i++)
                    {
                        dash[i] = (float)(state.dashPattern[i] * state.strokeWidth);
                    }

                    state.pen.DashPattern = dash;
                }
		    }
	    }

	    /// <summary>
	    /// 
	    /// </summary>
	    protected class CanvasState : ICloneable
	    {
		    /// <summary>
		    /// 
		    /// </summary>
		    internal double alpha = 1;

		    /// <summary>
		    /// 
		    /// </summary>
		    internal double scale = 1;

            /// <summary>
            /// 
            /// </summary>
		    internal double dx = 0;

            /// <summary>
            /// 
            /// </summary>
		    internal double dy = 0;

            /// <summary>
            /// 
            /// </summary>
		    internal double miterLimit = 10;

            /// <summary>
            /// 
            /// </summary>
		    internal int fontStyle = 0;

            /// <summary>
            /// 
            /// </summary>
		    internal double fontSize = mxConstants.DEFAULT_FONTSIZE;

            /// <summary>
            /// 
            /// </summary>
		    internal string fontFamily = mxConstants.DEFAULT_FONTFAMILIES;

            /// <summary>
            /// 
            /// </summary>
		    internal string fontColorValue = "#000000";

            /// <summary>
            /// 
            /// </summary>
            internal Brush fontBrush = new SolidBrush(Color.Black);

            /// <summary>
            /// 
            /// </summary>
		    internal string lineCap = "flat";

            /// <summary>
            /// 
            /// </summary>
		    internal string lineJoin = "miter";

            /// <summary>
            /// 
            /// </summary>
		    internal double strokeWidth = 1;

            /// <summary>
            /// 
            /// </summary>
		    internal string strokeColorValue = mxConstants.NONE;

            /// <summary>
            /// 
            /// </summary>
		    internal Color? strokeColor;

            /// <summary>
            /// 
            /// </summary>
		    internal Brush brush;

            /// <summary>
            /// 
            /// </summary>
            internal Pen pen;

            /// <summary>
            /// 
            /// </summary>
            internal Font font;

            /// <summary>
            /// 
            /// </summary>
		    internal bool dashed = false;

            /// <summary>
            /// 
            /// </summary>
		    internal float[] dashPattern = { 3, 3 };

            /// <summary>
            /// 
            /// </summary>
            internal GraphicsState state;

            /// <summary>
            /// 
            /// </summary>
		    public Object Clone()
		    {
			    return MemberwiseClone();
		    }
	    }
    }
}
