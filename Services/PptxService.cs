using System;
using System.IO;
using System.Collections.Generic;
using InkStudio.Models;

namespace InkStudio.Services;

public class PptxService
{
    public static List<DocumentPage> LoadPptx(string filePath, IProgress<(int current, int total)>? progress = null)
    {
        // Check if PowerPoint COM is available
        Type? pptType = Type.GetTypeFromProgID("PowerPoint.Application");
        if (pptType != null)
        {
            dynamic? pptApp = null;
            try
            {
                pptApp = Activator.CreateInstance(pptType);
                if (pptApp != null)
                {
                    try { pptApp.WindowState = 2; } catch { } // Minimized
                    
                    // Open read-only and without window
                    dynamic presentation = pptApp.Presentations.Open(
                        filePath, 
                        ReadOnly: -1 /* msoTrue */, 
                        Untitled: 0 /* msoFalse */, 
                        WithWindow: 0 /* msoFalse */
                    );

                    string tempPdf = Path.Combine(Path.GetTempPath(), $"InkStudio_pptx_{Guid.NewGuid():N}.pdf");
                    
                    // 32 = ppSaveAsPDF
                    presentation.SaveAs(tempPdf, 32);
                    presentation.Close();

                    var pages = PdfService.LoadPdf(tempPdf, 150, progress);

                    // Clean up temp file after reading
                    try { File.Delete(tempPdf); } catch { }

                    return pages;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PowerPoint COM conversion failed: {ex.Message}");
            }
            finally
            {
                if (pptApp != null)
                {
                    try { pptApp.Quit(); } catch { }
                }
            }
        }

        // Fallback or error
        throw new InvalidOperationException("PowerPoint dosyası dönüştürülemedi. Lütfen sistemde PowerPoint yüklü olduğundan emin olun.");
    }
}
