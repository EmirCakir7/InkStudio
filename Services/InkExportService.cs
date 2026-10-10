using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InkStudio.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

namespace InkStudio.Services;

public class InkExportService
{
    public static void ExportToPdf(
        string targetFilePath, 
        List<DocumentPage> pages, 
        string? sourcePdfFilePath = null, 
        IProgress<(int current, int total)>? progress = null)
    {
        // 1. Try modifying the original PDF directly if available (preserves original vector text, fonts, and quality 100%)
        if (!string.IsNullOrEmpty(sourcePdfFilePath) && 
            File.Exists(sourcePdfFilePath) && 
            Path.GetExtension(sourcePdfFilePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                byte[] sourceBytes = File.ReadAllBytes(sourcePdfFilePath);
                using var ms = new MemoryStream(sourceBytes);
                using var pdfDoc = PdfReader.Open(ms, PdfDocumentOpenMode.Modify);

                int pageCount = Math.Min(pdfDoc.PageCount, pages.Count);
                for (int i = 0; i < pageCount; i++)
                {
                    var page = pages[i];
                    if (page.Strokes.Count > 0)
                    {
                        var pdfPage = pdfDoc.Pages[i];
                        using var gfx = XGraphics.FromPdfPage(pdfPage, XGraphicsPdfPageOptions.Append);

                        // Align coordinate systems (WPF canvas coordinates to PDF point coordinates)
                        double scaleX = gfx.PageSize.Width / Math.Max(1.0, page.Width);
                        double scaleY = gfx.PageSize.Height / Math.Max(1.0, page.Height);
                        gfx.ScaleTransform(scaleX, scaleY);

                        DrawStrokesVector(gfx, page.Strokes);
                    }

                    progress?.Report((i + 1, pageCount));
                }

                pdfDoc.Save(targetFilePath);
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Modify original PDF failed, using clean vector export: {ex.Message}");
            }
        }

        // 2. Clean Vector PDF Generation (For new blank documents, PPTX or fallback)
        using var targetDoc = new PdfDocument();
        int totalPages = pages.Count;

        for (int i = 0; i < totalPages; i++)
        {
            var page = pages[i];
            var pdfPage = targetDoc.AddPage();

            // WPF is 96 DPI, PDF points are 72 DPI => 1 WPF pixel = 0.75 pt
            double targetWidthPt = page.Width * 0.75;
            double targetHeightPt = page.Height * 0.75;
            pdfPage.Width = XUnit.FromPoint(targetWidthPt);
            pdfPage.Height = XUnit.FromPoint(targetHeightPt);

            using var gfx = XGraphics.FromPdfPage(pdfPage);

            // Background
            if (page.BackgroundImage != null)
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(page.BackgroundImage));
                using var imgMs = new MemoryStream();
                encoder.Save(imgMs);
                imgMs.Position = 0;
                using var xImage = XImage.FromStream(imgMs);
                gfx.DrawImage(xImage, 0, 0, pdfPage.Width.Point, pdfPage.Height.Point);
            }
            else
            {
                gfx.DrawRectangle(XBrushes.White, 0, 0, pdfPage.Width.Point, pdfPage.Height.Point);
            }

            // Draw crisp vector strokes on top
            if (page.Strokes.Count > 0)
            {
                gfx.ScaleTransform(0.75, 0.75);
                DrawStrokesVector(gfx, page.Strokes);
            }

            progress?.Report((i + 1, totalPages));
        }

        targetDoc.Save(targetFilePath);
    }

    /// <summary>
    /// Renders WPF StrokeCollection into native vector graphics paths in PDFSharp.
    /// Provides infinite zoom clarity (vector), pressure-sensitivity preservation, and true transparency for highlighters.
    /// </summary>
    public static void DrawStrokesVector(XGraphics gfx, StrokeCollection strokes)
    {
        foreach (var stroke in strokes)
        {
            try
            {
                var da = stroke.DrawingAttributes;
                byte alpha = da.Color.A;
                if (da.IsHighlighter && alpha == 255)
                {
                    alpha = 120;
                }

                var color = XColor.FromArgb(alpha, da.Color.R, da.Color.G, da.Color.B);
                var brush = new XSolidBrush(color);

                var geom = stroke.GetGeometry(da);
                if (geom == null || geom.IsEmpty())
                {
                    DrawSinglePointOrLineFallback(gfx, stroke, color, brush);
                    continue;
                }

                // Flatten curved Bézier contours into smooth line segments with fine tolerance (0.1 px)
                var flattened = geom.GetFlattenedPathGeometry(0.1, ToleranceType.Absolute);
                if (flattened.Figures.Count == 0)
                {
                    DrawSinglePointOrLineFallback(gfx, stroke, color, brush);
                    continue;
                }

                var xPath = new XGraphicsPath();
                xPath.FillMode = flattened.FillRule == FillRule.EvenOdd ? XFillMode.Alternate : XFillMode.Winding;

                foreach (var fig in flattened.Figures)
                {
                    xPath.StartFigure();
                    var currentPt = new XPoint(fig.StartPoint.X, fig.StartPoint.Y);

                    foreach (var seg in fig.Segments)
                    {
                        if (seg is PolyLineSegment pls)
                        {
                            foreach (var pt in pls.Points)
                            {
                                var nextPt = new XPoint(pt.X, pt.Y);
                                xPath.AddLine(currentPt, nextPt);
                                currentPt = nextPt;
                            }
                        }
                        else if (seg is LineSegment ls)
                        {
                            var nextPt = new XPoint(ls.Point.X, ls.Point.Y);
                            xPath.AddLine(currentPt, nextPt);
                            currentPt = nextPt;
                        }
                    }

                    if (fig.IsClosed)
                    {
                        xPath.CloseFigure();
                    }
                }

                gfx.DrawPath(brush, xPath);
            }
            catch
            {
                // Fallback for unexpected geometry calculation errors
                try
                {
                    var da = stroke.DrawingAttributes;
                    var color = XColor.FromArgb(da.Color.A, da.Color.R, da.Color.G, da.Color.B);
                    var brush = new XSolidBrush(color);
                    DrawSinglePointOrLineFallback(gfx, stroke, color, brush);
                }
                catch
                {
                    // Ignore corrupted stroke
                }
            }
        }
    }

    private static void DrawSinglePointOrLineFallback(XGraphics gfx, Stroke stroke, XColor color, XSolidBrush brush)
    {
        var pts = stroke.StylusPoints;
        if (pts.Count == 1)
        {
            double r = Math.Max(1.0, stroke.DrawingAttributes.Width / 2.0);
            gfx.DrawEllipse(brush, pts[0].X - r, pts[0].Y - r, r * 2, r * 2);
        }
        else if (pts.Count > 1)
        {
            var pen = new XPen(color, stroke.DrawingAttributes.Width)
            {
                LineCap = XLineCap.Round,
                LineJoin = XLineJoin.Round
            };
            var xPoints = new XPoint[pts.Count];
            for (int i = 0; i < pts.Count; i++)
            {
                xPoints[i] = new XPoint(pts[i].X, pts[i].Y);
            }
            gfx.DrawLines(pen, xPoints);
        }
    }
}
