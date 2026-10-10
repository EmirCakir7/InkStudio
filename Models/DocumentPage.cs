using System.IO;
using System.Windows.Ink;
using System.Windows.Media.Imaging;

namespace InkStudio.Models;

public class DocumentPage
{
    public int PageIndex { get; set; }
    public int PageNumber => PageIndex + 1;
    public string PageDisplay => $"{Services.LocalizationService.Get("FooterLblPage")} {PageNumber}";
    public double Width { get; set; } = 800;
    public double Height { get; set; } = 1100;
    public BitmapSource? BackgroundImage { get; set; }
    public StrokeCollection Strokes { get; set; } = new();
    public BitmapSource? Thumbnail { get; set; }

    // Backup for undo/redo
    public Stack<StrokeCollection> UndoStack { get; } = new();
    public Stack<StrokeCollection> RedoStack { get; } = new();

    public void SaveState()
    {
        using var ms = new MemoryStream();
        Strokes.Save(ms);
        ms.Position = 0;
        var copy = new StrokeCollection(ms);
        UndoStack.Push(copy);
        RedoStack.Clear();
    }
}
