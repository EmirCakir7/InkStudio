using System;
using System.IO;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;
using InkStudio.Models;

namespace InkStudio.Services;

public class PdfService
{
    public static List<DocumentPage> LoadPdf(string filePath, int targetDpi = 150, IProgress<(int current, int total)>? progress = null)
    {
        var pages = new List<DocumentPage>();
        
        // DocLib instance
        using var docReader = DocLib.Instance.GetDocReader(filePath, new PageDimensions(1400, 2000));
        int pageCount = docReader.GetPageCount();

        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            int width = pageReader.GetPageWidth();
            int height = pageReader.GetPageHeight();
            byte[] rawBytes = pageReader.GetImage();

            var bitmap = BitmapSource.Create(
                width,
                height,
                96,
                96,
                PixelFormats.Bgra32,
                null,
                rawBytes,
                width * 4
            );
            bitmap.Freeze();

            pages.Add(new DocumentPage
            {
                PageIndex = i,
                Width = width,
                Height = height,
                BackgroundImage = bitmap,
                Thumbnail = bitmap // can use thumbnail or bitmap directly
            });

            progress?.Report((i + 1, pageCount));
        }

        return pages;
    }
}
