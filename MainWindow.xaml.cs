using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using InkStudio.Controls;
using InkStudio.Models;
using InkStudio.Services;
using ScreenRecorderLib;

namespace InkStudio;

public partial class MainWindow : Window
{
    private List<DocumentPage> _pages = new();
    private int _currentPageIndex = 0;
    private string? _currentFilePath;
    private double _zoomFactor = 1.0;

    // Undo / Redo stacks for the current active page
    private readonly Stack<StrokeCollection> _undoStack = new();
    private readonly Stack<StrokeCollection> _redoStack = new();
    private bool _isPerformingUndoRedo = false;

    // Lecture Recording Service & Audio Meter
    private LectureRecorderService? _recorderService;
    private AudioMeterService? _audioMeterService;

    public MainWindow()
    {
        InitializeComponent();

        // Suppress any window-level bouncing/displacement on touch edge
        ManipulationBoundaryFeedback += (s, e) => e.Handled = true;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        InkLayer.PenTelemetryChanged += InkLayer_PenTelemetryChanged;
        InkLayer.Strokes.StrokesChanged += Strokes_StrokesChanged;

        InitializeRecorder();

        MainScrollViewer.ScrollChanged += (s, e) => {
            UpdateLivePreviewDocumentCrop();
            NotifyRecordingCropAreaChanged();
        };
        SizeChanged += (s, e) => {
            UpdateLivePreviewDocumentCrop();
            UpdateCameraPlacementFromUI();
            NotifyRecordingCropAreaChanged();
        };
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Default to English as requested
        ApplyLanguage();

        // Don't open a blank document directly on startup; show the clean Welcome Screen
        SetDocumentActive(false);
        PopulateCameras();
        UpdateCameraPlacementFromUI();
        UpdateLivePreviewDocumentCrop();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _audioMeterService?.Dispose();
        _recorderService?.Dispose();
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string langCode)
        {
            LocalizationService.SetLanguageByCode(langCode);
            ApplyLanguage();
            if (PopupToolbarLang != null) PopupToolbarLang.IsOpen = false;
        }
    }

    private void BtnToolbarLang_Click(object sender, RoutedEventArgs e)
    {
        if (PopupToolbarLang != null)
        {
            PopupToolbarLang.IsOpen = !PopupToolbarLang.IsOpen;
        }
    }

    private void UpdateLangPillStyles()
    {
        var current = LocalizationService.CurrentLanguage;
        SetLangPillActive(BtnLangEn, TxtLangEn, current == AppLanguage.English);
        SetLangPillActive(BtnLangTr, TxtLangTr, current == AppLanguage.Turkish);
        SetLangPillActive(BtnLangDe, TxtLangDe, current == AppLanguage.German);
        SetLangPillActive(BtnLangRu, TxtLangRu, current == AppLanguage.Russian);
        SetLangPillActive(BtnLangZh, TxtLangZh, current == AppLanguage.Chinese);

        if (TxtToolbarLangFlag != null) TxtToolbarLangFlag.Text = LocalizationService.GetLanguageFlag();
        if (TxtToolbarLangCode != null) TxtToolbarLangCode.Text = LocalizationService.GetLanguageCode().ToUpperInvariant();
    }

    private void SetLangPillActive(Button? btn, TextBlock? txt, bool isActive)
    {
        if (btn == null || txt == null) return;
        if (isActive)
        {
            btn.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)); // #2563EB
            txt.Foreground = Brushes.White;
            txt.FontWeight = FontWeights.Bold;
        }
        else
        {
            btn.Background = Brushes.Transparent;
            txt.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)); // #475569
            txt.FontWeight = FontWeights.Medium;
        }
    }

    private string _activeToolTag = "Pen";

    private void ApplyLanguage()
    {
        // 0. Window Title
        if (_pages.Count == 0 || WelcomeScreen.Visibility == Visibility.Visible)
        {
            Title = $"InkStudio - {LocalizationService.Get("WelcomeSubtitle")}";
        }

        // 1. Welcome Screen
        if (TxtWelcomeSubtitle != null) TxtWelcomeSubtitle.Text = LocalizationService.Get("WelcomeSubtitle");
        if (TxtCardNewTitle != null) TxtCardNewTitle.Text = LocalizationService.Get("CardNewTitle");
        if (TxtCardNewDesc != null) TxtCardNewDesc.Text = LocalizationService.Get("CardNewDesc");
        if (TxtCardOpenTitle != null) TxtCardOpenTitle.Text = LocalizationService.Get("CardOpenTitle");
        if (TxtCardOpenDesc != null) TxtCardOpenDesc.Text = LocalizationService.Get("CardOpenDesc");
        if (TxtCardRecordTitle != null) TxtCardRecordTitle.Text = LocalizationService.Get("CardRecordTitle");
        if (TxtCardRecordDesc != null) TxtCardRecordDesc.Text = LocalizationService.Get("CardRecordDesc");
        if (TxtBadgePressure != null) TxtBadgePressure.Text = LocalizationService.Get("BadgePressure");
        if (TxtBadgeTilt != null) TxtBadgeTilt.Text = LocalizationService.Get("BadgeTilt");
        if (TxtBadgePalm != null) TxtBadgePalm.Text = LocalizationService.Get("BadgePalm");

        // 2. Toolbar File & Edit Buttons
        if (TxtBtnOpen != null) TxtBtnOpen.Text = LocalizationService.Get("BtnOpen");
        if (TxtBtnSave != null) TxtBtnSave.Text = LocalizationService.Get("BtnSave");
        if (TxtBtnNewPage != null) TxtBtnNewPage.Text = LocalizationService.Get("BtnNew");
        if (TxtBtnToggleSidebar != null) TxtBtnToggleSidebar.Text = $"📑 {LocalizationService.Get("BtnPages")}";
        if (BtnOpen != null) BtnOpen.ToolTip = LocalizationService.Get("BtnOpen");
        if (BtnSave != null) BtnSave.ToolTip = LocalizationService.Get("BtnSave");
        if (BtnNewPage != null) BtnNewPage.ToolTip = LocalizationService.Get("BtnNew");
        if (BtnUndo != null) BtnUndo.ToolTip = $"{LocalizationService.Get("BtnUndo")} (Ctrl+Z)";
        if (BtnRedo != null) BtnRedo.ToolTip = $"{LocalizationService.Get("BtnRedo")} (Ctrl+Y)";
        if (BtnClear != null) BtnClear.ToolTip = LocalizationService.Get("BtnClear");
        if (BtnCloseDoc != null) BtnCloseDoc.ToolTip = LocalizationService.Get("TipCloseDoc");
        if (BtnToggleSidebar != null) BtnToggleSidebar.ToolTip = LocalizationService.Get("TipToggleSidebar");

        // 2b. Tools & Colors & Thickness
        if (BtnToolPicker != null) BtnToolPicker.ToolTip = LocalizationService.Get("ToolPickerTip");
        if (TxtHeaderToolPicker != null) TxtHeaderToolPicker.Text = LocalizationService.Get("ToolPickerHeader");
        if (TxtToolPenTitle != null) TxtToolPenTitle.Text = LocalizationService.Get("ToolPenTitle");
        if (TxtToolPenDesc != null) TxtToolPenDesc.Text = LocalizationService.Get("ToolPenDesc");
        if (TxtToolPencilTitle != null) TxtToolPencilTitle.Text = LocalizationService.Get("ToolPencilTitle");
        if (TxtToolPencilDesc != null) TxtToolPencilDesc.Text = LocalizationService.Get("ToolPencilDesc");
        if (TxtToolHighlighterTitle != null) TxtToolHighlighterTitle.Text = LocalizationService.Get("ToolHighlighterTitle");
        if (TxtToolHighlighterDesc != null) TxtToolHighlighterDesc.Text = LocalizationService.Get("ToolHighlighterDesc");
        if (TxtToolEraserTitle != null) TxtToolEraserTitle.Text = LocalizationService.Get("ToolEraserTitle");
        if (TxtToolEraserDesc != null) TxtToolEraserDesc.Text = LocalizationService.Get("ToolEraserDesc");
        if (TxtToolLassoTitle != null) TxtToolLassoTitle.Text = LocalizationService.Get("ToolLassoTitle");
        if (TxtToolLassoDesc != null) TxtToolLassoDesc.Text = LocalizationService.Get("ToolLassoDesc");

        if (TxtBtnColorPicker != null) TxtBtnColorPicker.Text = LocalizationService.Get("BtnColor");
        if (BtnColorPicker != null) BtnColorPicker.ToolTip = LocalizationService.Get("TipColorPicker");
        if (TxtHeaderColorPicker != null) TxtHeaderColorPicker.Text = LocalizationService.Get("HeaderColorPicker");

        if (TxtLblThickness != null) TxtLblThickness.Text = LocalizationService.Get("LblThickness");
        if (BtnDecreaseWidth != null) BtnDecreaseWidth.ToolTip = LocalizationService.Get("TipDecreaseWidth");
        if (SliderWidth != null) SliderWidth.ToolTip = LocalizationService.Get("TipStrokeWidth");
        if (BtnIncreaseWidth != null) BtnIncreaseWidth.ToolTip = LocalizationService.Get("TipIncreaseWidth");

        if (CheckTouchInking != null)
        {
            CheckTouchInking.Content = LocalizationService.Get("CheckTouchInking");
            CheckTouchInking.ToolTip = LocalizationService.Get("TipTouchInking");
        }

        // 3. Sidebar Header
        if (TxtSidebarPagesTitle != null) TxtSidebarPagesTitle.Text = LocalizationService.Get("SidebarPagesTitle");
        if (TxtHeaderLectureRecording != null) TxtHeaderLectureRecording.Text = LocalizationService.Get("LectureStudioHeader");
        if (TxtBtnPreviewModeLive != null) TxtBtnPreviewModeLive.Text = LocalizationService.Get("BtnLivePreview");
        if (TxtBtnPreviewModeCam != null) TxtBtnPreviewModeCam.Text = LocalizationService.Get("BtnCameraOnly");
        if (TxtMirrorIcon != null) TxtMirrorIcon.Text = LocalizationService.Get("BtnMirror");
        if (TxtBtnToggleLayoutPreview != null) TxtBtnToggleLayoutPreview.Text = LocalizationService.Get("BtnLayout");
        if (TxtPreviewPlaceholder != null) TxtPreviewPlaceholder.Text = LocalizationService.Get("CameraPlaceholder");

        // 4. Camera Layout Settings
        if (TxtCameraLayoutTitle != null) TxtCameraLayoutTitle.Text = LocalizationService.Get("CameraLayoutTitle");
        if (TxtLblPosition != null) TxtLblPosition.Text = LocalizationService.Get("LblPosition");
        if (TxtLblCameraAspect != null) TxtLblCameraAspect.Text = LocalizationService.Get("LblCameraAspect");
        if (TxtLblCameraSize != null) TxtLblCameraSize.Text = LocalizationService.Get("CameraSize");
        if (TxtLblCustomAspect != null) TxtLblCustomAspect.Text = LocalizationService.Get("CamAspectCustom");

        // 5. Lecture Recording Settings & Buttons
        if (TxtLblAspectRatio != null) TxtLblAspectRatio.Text = LocalizationService.Get("LblAspect");
        if (TxtLblCamera != null) TxtLblCamera.Text = LocalizationService.Get("LblCamera");
        if (TxtLblMicrophone != null) TxtLblMicrophone.Text = LocalizationService.Get("LblMicrophone");
        if (TxtLblMicLevel != null) TxtLblMicLevel.Text = LocalizationService.Get("LblAudioLevel");
        if (TxtLblMicVolume != null) TxtLblMicVolume.Text = LocalizationService.Get("LblMicVol");
        if (SliderMicVolume != null) SliderMicVolume.ToolTip = LocalizationService.Get("TipMicVolume");

        if (TxtBtnRecordStart != null) TxtBtnRecordStart.Text = LocalizationService.Get("BtnStartRecord").Replace("🔴 ", "");
        if (BtnRecordStart != null) BtnRecordStart.ToolTip = LocalizationService.Get("BtnStartRecord");
        if (BtnRecordPause != null) BtnRecordPause.ToolTip = LocalizationService.Get("TipRecordPause");
        if (BtnRecordStop != null) BtnRecordStop.ToolTip = LocalizationService.Get("TipRecordStop");
        if (BtnShowRecordings != null) BtnShowRecordings.ToolTip = LocalizationService.Get("TipOpenRecordings");

        // 6. Update ComboBox items with localized texts
        if (CmbAspectRatio != null)
        {
            int savedAspect = CmbAspectRatio.SelectedIndex;
            CmbAspectRatio.SelectionChanged -= CmbAspectRatio_SelectionChanged;
            CmbAspectRatio.Items.Clear();
            CmbAspectRatio.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("AspectFitDocument") });
            CmbAspectRatio.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Aspect16x9Fhd") });
            CmbAspectRatio.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Aspect16x9Hd") });
            CmbAspectRatio.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Aspect4x3") });
            CmbAspectRatio.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Aspect9x16") });
            CmbAspectRatio.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("Aspect1x1") });
            CmbAspectRatio.SelectedIndex = savedAspect >= 0 ? savedAspect : 0;
            CmbAspectRatio.SelectionChanged += CmbAspectRatio_SelectionChanged;
        }

        if (CmbCameraPosition != null)
        {
            int savedPos = CmbCameraPosition.SelectedIndex;
            CmbCameraPosition.SelectionChanged -= CmbCameraPosition_SelectionChanged;
            CmbCameraPosition.Items.Clear();
            CmbCameraPosition.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("PosBottomRight") });
            CmbCameraPosition.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("PosBottomLeft") });
            CmbCameraPosition.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("PosTopRight") });
            CmbCameraPosition.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("PosTopLeft") });
            CmbCameraPosition.SelectedIndex = savedPos >= 0 ? savedPos : 0;
            CmbCameraPosition.SelectionChanged += CmbCameraPosition_SelectionChanged;
        }

        if (CmbCameraAspect != null)
        {
            int savedCamAspect = CmbCameraAspect.SelectedIndex;
            CmbCameraAspect.SelectionChanged -= CmbCameraAspect_SelectionChanged;
            CmbCameraAspect.Items.Clear();
            CmbCameraAspect.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("CamAspect16x9") });
            CmbCameraAspect.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("CamAspect4x3") });
            CmbCameraAspect.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("CamAspect1x1") });
            CmbCameraAspect.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("CamAspect9x16") });
            CmbCameraAspect.Items.Add(new ComboBoxItem { Content = LocalizationService.Get("CamAspectCustom") });
            CmbCameraAspect.SelectedIndex = savedCamAspect >= 0 ? savedCamAspect : 0;
            CmbCameraAspect.SelectionChanged += CmbCameraAspect_SelectionChanged;
        }

        // 7. Footer & Navigation
        if (TxtBtnPrevPage != null) TxtBtnPrevPage.Text = LocalizationService.Get("FooterPrev");
        if (TxtLblPage != null) TxtLblPage.Text = LocalizationService.Get("FooterLblPage");
        if (TxtJumpPage != null) TxtJumpPage.ToolTip = LocalizationService.Get("FooterTipJumpPage");
        if (TxtBtnJumpPage != null) TxtBtnJumpPage.Text = LocalizationService.Get("FooterBtnGo");
        if (BtnJumpPage != null) BtnJumpPage.ToolTip = LocalizationService.Get("FooterTipGo");
        if (TxtBtnNextPage != null) TxtBtnNextPage.Text = LocalizationService.Get("FooterNext");
        if (TxtTelemetryMode != null) TxtTelemetryMode.Text = LocalizationService.Get("TelemetryModeDefault");
        if (TxtTelemetryDevice != null && TxtTelemetryDevice.Text.Contains(":")) 
        {
            TxtTelemetryDevice.Text = LocalizationService.Get("TelemetryPenNone");
        }

        // 8. Lecture Choice Modal
        if (TxtModalLectureTitle != null) TxtModalLectureTitle.Text = LocalizationService.Get("ModalLectureTitle");
        if (TxtModalLectureSubtitle != null) TxtModalLectureSubtitle.Text = LocalizationService.Get("ModalLectureSubtitle");
        if (TxtModalChoice1Title != null) TxtModalChoice1Title.Text = LocalizationService.Get("ModalChoice1Title");
        if (TxtModalChoice1Desc != null) TxtModalChoice1Desc.Text = LocalizationService.Get("ModalChoice1Desc");
        if (TxtModalChoice2Title != null) TxtModalChoice2Title.Text = LocalizationService.Get("ModalChoice2Title");
        if (TxtModalChoice2Desc != null) TxtModalChoice2Desc.Text = LocalizationService.Get("ModalChoice2Desc");
        if (BtnLectureChoiceCancel != null) BtnLectureChoiceCancel.Content = LocalizationService.Get("ModalBtnCancel");

        // 9. Loading Overlay
        if (TxtLoadingTitle != null) TxtLoadingTitle.Text = LocalizationService.Get("LoadingDocTitle");
        if (TxtLoadingSubtitle != null) TxtLoadingSubtitle.Text = LocalizationService.Get("LoadingDocSubtitle");

        // 10. Update Status Badge
        if (TxtRecordingStatus != null && _recorderService != null)
        {
            TxtRecordingStatus.Text = _recorderService.State switch
            {
                RecordingState.Recording => LocalizationService.Get("StatusRecording"),
                RecordingState.Paused => LocalizationService.Get("StatusPaused"),
                _ => LocalizationService.Get("StatusReady")
            };
        }

        // 11. Refresh current tool label on button
        SelectTool(_activeToolTag);

        // 12. Zoom and corner tooltips
        if (BtnZoomOut != null) BtnZoomOut.ToolTip = LocalizationService.Get("TipZoomOut");
        if (BtnZoomIn != null) BtnZoomIn.ToolTip = LocalizationService.Get("TipZoomIn");
        if (BtnCornerTL != null) BtnCornerTL.ToolTip = LocalizationService.Get("PosTopLeft");
        if (BtnCornerTR != null) BtnCornerTR.ToolTip = LocalizationService.Get("PosTopRight");
        if (BtnCornerBL != null) BtnCornerBL.ToolTip = LocalizationService.Get("PosBottomLeft");
        if (BtnCornerBR != null) BtnCornerBR.ToolTip = LocalizationService.Get("PosBottomRight");
        if (TxtMiniCameraBadge != null) TxtMiniCameraBadge.Text = LocalizationService.Get("CameraBadge");

        // 13. Refresh sidebar thumbnails page labels
        try { ListThumbnails?.Items.Refresh(); } catch { }

        UpdateLangPillStyles();
        UpdateCameraPlacementFromUI();
    }

    private void SetDocumentActive(bool isActive)
    {
        TopToolbar.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        WelcomeScreen.Visibility = isActive ? Visibility.Collapsed : Visibility.Visible;
        WorkspaceGrid.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
        FooterBar.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;

        BtnSave.IsEnabled = isActive;
        BtnToolPicker.IsEnabled = isActive;
        BtnColorPicker.IsEnabled = isActive;
        BtnDecreaseWidth.IsEnabled = isActive;
        SliderWidth.IsEnabled = isActive;
        BtnIncreaseWidth.IsEnabled = isActive;
        BtnUndo.IsEnabled = isActive;
        BtnRedo.IsEnabled = isActive;
        BtnClear.IsEnabled = isActive;
        CheckTouchInking.IsEnabled = isActive;
        BtnToggleSidebar.IsEnabled = isActive;

        if (!isActive)
        {
            Title = $"InkStudio - {LocalizationService.Get("WelcomeSubtitle")}";
            SetLectureRecordingMode(false);
        }
    }

    private void BtnCloseDoc_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(LocalizationService.Get("DialogCloseConfirm"), 
                                     LocalizationService.Get("DialogCloseTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _pages.Clear();
            _currentFilePath = null;
            SetDocumentActive(false);
            SetLectureRecordingMode(false);
        }
    }

    private void BtnWelcomeNew_Click(object sender, RoutedEventArgs e)
    {
        CreateBlankDocument(1000, 1414, LocalizationService.Get("DefaultNoteTitle"), isLectureMode: false);
    }

    private void BtnWelcomeOpen_Click(object sender, RoutedEventArgs e)
    {
        BtnOpen_Click(sender, e);
    }

    private void CreateBlankDocument(int width = 1000, int height = 1414, string? title = null, bool isLectureMode = false)
    {
        if (string.IsNullOrEmpty(title)) title = LocalizationService.Get("DefaultNoteTitle");
        SetDocumentActive(true);
        SetLectureRecordingMode(isLectureMode);
        SaveCurrentPageStrokes();

        _pages.Clear();
        _pages.Add(new DocumentPage
        {
            PageIndex = 0,
            Width = width,
            Height = height,
            BackgroundImage = null,
            Thumbnail = CreateBlankThumbnail(width, height)
        });

        _currentPageIndex = 0;
        _currentFilePath = null;
        Title = title;

        RefreshThumbnails();
        DisplayCurrentPage();
        FitPageToView();
    }

    private BitmapSource CreateBlankThumbnail(int width = 1000, int height = 1414)
    {
        int thumbW = 150;
        int thumbH = (int)(thumbW * ((double)height / width));
        var rtb = new RenderTargetBitmap(thumbW, thumbH, 96, 96, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, new Pen(Brushes.LightGray, 1), new Rect(0, 0, thumbW, thumbH));
        }
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }

    private void DisplayCurrentPage()
    {
        if (_pages.Count == 0 || _currentPageIndex < 0 || _currentPageIndex >= _pages.Count)
            return;

        var page = _pages[_currentPageIndex];

        // Unsubscribe while swapping strokes to avoid trigger spam
        InkLayer.Strokes.StrokesChanged -= Strokes_StrokesChanged;

        // Set dimensions
        InkLayer.Width = page.Width;
        InkLayer.Height = page.Height;

        // Set background
        PageBackground.Source = page.BackgroundImage;
        PageBackground.Width = page.Width;
        PageBackground.Height = page.Height;

        // Set strokes
        InkLayer.Strokes = page.Strokes;
        InkLayer.Strokes.StrokesChanged += Strokes_StrokesChanged;

        // Reset undo/redo for this page session
        _undoStack.Clear();
        _redoStack.Clear();

        // Update navigation UI
        TxtJumpPage.Text = (_currentPageIndex + 1).ToString();
        TxtTotalPages.Text = $"/ {_pages.Count}";
        BtnPrevPage.IsEnabled = _currentPageIndex > 0;
        BtnNextPage.IsEnabled = _currentPageIndex < _pages.Count - 1;

        if (ListThumbnails.SelectedIndex != _currentPageIndex)
        {
            ListThumbnails.SelectedIndex = _currentPageIndex;
        }

        // When recording is active, ensure new slide/page immediately fits the recording frame
        if (_recorderService != null && _recorderService.State == RecordingState.Recording)
        {
            FitPageToView();
        }

        UpdateCameraPlacementFromUI();
        UpdateLivePreviewDocumentCrop();
    }

    private void SaveCurrentPageStrokes()
    {
        if (_pages.Count > 0 && _currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
        {
            _pages[_currentPageIndex].Strokes = InkLayer.Strokes.Clone();
        }
    }

    private void RefreshThumbnails()
    {
        ListThumbnails.ItemsSource = null;
        ListThumbnails.ItemsSource = _pages;
        ListThumbnails.SelectedIndex = _currentPageIndex;
    }

    // ==========================================
    // FILE OPEN / SAVE
    // ==========================================

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = LocalizationService.Get("CardOpenTitle"),
            Filter = LocalizationService.Get("DialogFilterAllSupported")
        };

        if (dlg.ShowDialog() == true)
        {
            LoadDocument(dlg.FileName, isLectureMode: false);
        }
    }

    private async void LoadDocument(string filePath, bool isLectureMode = false)
    {
        try
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            LoadingOverlay.Visibility = Visibility.Visible;
            ProgressBarFill.Width = 0;
            TxtLoadingTitle.Text = Path.GetFileName(filePath);
            TxtLoadingPercent.Text = "%0";
            BtnOpen.IsEnabled = false;
            BtnSave.IsEnabled = false;

            if (ext == ".pptx" || ext == ".ppt")
            {
                TxtLoadingSubtitle.Text = LocalizationService.Get("LoadingPptSubtitle");
            }
            else
            {
                TxtLoadingSubtitle.Text = LocalizationService.Get("LoadingDocSubtitle");
            }

            var progress = new Progress<(int current, int total)>(tuple =>
            {
                int current = tuple.current;
                int total = tuple.total;
                double percent = total > 0 ? (double)current / total * 100.0 : 0;
                
                double totalTrackWidth = ProgressBarTrack.ActualWidth > 0 ? ProgressBarTrack.ActualWidth : 358.0;
                ProgressBarFill.Width = Math.Max(0, Math.Min(totalTrackWidth, totalTrackWidth * (percent / 100.0)));
                TxtLoadingPercent.Text = $"%{(int)percent} ({current} / {total})";
            });

            List<DocumentPage> loadedPages = await Task.Run(() =>
            {
                if (ext == ".pptx" || ext == ".ppt")
                {
                    return PptxService.LoadPptx(filePath, progress);
                }
                else
                {
                    return PdfService.LoadPdf(filePath, 150, progress);
                }
            });

            if (loadedPages.Count == 0)
            {
                MessageBox.Show(LocalizationService.Get("DialogDocReadError"), LocalizationService.Get("DialogWarningTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetDocumentActive(true);
            SetLectureRecordingMode(isLectureMode);
            SaveCurrentPageStrokes();
            _pages = loadedPages;
            _currentPageIndex = 0;
            _currentFilePath = filePath;

            Title = $"InkStudio - {Path.GetFileName(filePath)} ({_pages.Count}) [MPP 2.0]";

            RefreshThumbnails();
            DisplayCurrentPage();
            FitPageToView();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{ex.Message}", LocalizationService.Get("DialogErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            BtnOpen.IsEnabled = true;
            BtnSave.IsEnabled = _pages.Count > 0;
        }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentPageStrokes();

        var dlg = new SaveFileDialog
        {
            Title = LocalizationService.Get("BtnSave"),
            Filter = LocalizationService.Get("DialogFilterPdfOnly"),
            FileName = _currentFilePath != null 
                ? $"{Path.GetFileNameWithoutExtension(_currentFilePath)}_Edited.pdf" 
                : "Note_InkStudio.pdf"
        };

        if (dlg.ShowDialog() == true)
        {
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                ProgressBarFill.Width = 0;
                TxtLoadingTitle.Text = LocalizationService.Get("LoadingSavingPdfTitle");
                TxtLoadingSubtitle.Text = LocalizationService.Get("LoadingSavingPdfSubtitle");
                TxtLoadingPercent.Text = "%0";
                BtnOpen.IsEnabled = false;
                BtnSave.IsEnabled = false;

                string targetPath = dlg.FileName;
                string? sourcePath = _currentFilePath;

                var progress = new Progress<(int current, int total)>(p =>
                {
                    double percent = (double)p.current / Math.Max(1, p.total);
                    ProgressBarFill.Width = Math.Min(358, 358 * percent);
                    TxtLoadingPercent.Text = $"%{Math.Round(percent * 100):0}";
                    TxtLoadingSubtitle.Text = LocalizationService.Format("LoadingPageProcessing", p.current, p.total);
                });

                await Task.Run(() =>
                {
                    InkExportService.ExportToPdf(targetPath, _pages, sourcePath, progress);
                });

                MessageBox.Show(LocalizationService.Format("DialogSaveSuccessMsg", targetPath), 
                                LocalizationService.Get("DialogSaveSuccessTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{ex.Message}", LocalizationService.Get("DialogErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                BtnOpen.IsEnabled = true;
                BtnSave.IsEnabled = true;
            }
        }
    }

    private void BtnNewPage_Click(object sender, RoutedEventArgs e)
    {
        if (_pages.Count > 0)
        {
            var result = MessageBox.Show(LocalizationService.Get("DialogNewConfirm"), 
                                         LocalizationService.Get("DialogNewTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;
        }

        CreateBlankDocument();
    }

    // ==========================================
    // PAGE NAVIGATION
    // ==========================================

    private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPageIndex > 0)
        {
            SaveCurrentPageStrokes();
            _currentPageIndex--;
            DisplayCurrentPage();
        }
    }

    private void BtnNextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPageIndex < _pages.Count - 1)
        {
            SaveCurrentPageStrokes();
            _currentPageIndex++;
            DisplayCurrentPage();
        }
    }

    private void JumpToSpecifiedPage()
    {
        if (_pages.Count == 0) return;

        if (int.TryParse(TxtJumpPage.Text.Trim(), out int targetPage))
        {
            // Clamp to valid 1-based page range
            int targetIndex = Math.Clamp(targetPage, 1, _pages.Count) - 1;
            if (targetIndex != _currentPageIndex)
            {
                SaveCurrentPageStrokes();
                _currentPageIndex = targetIndex;
                DisplayCurrentPage();
            }
            else
            {
                TxtJumpPage.Text = (_currentPageIndex + 1).ToString();
            }
        }
        else
        {
            TxtJumpPage.Text = (_currentPageIndex + 1).ToString();
        }
    }

    private void BtnJumpPage_Click(object sender, RoutedEventArgs e)
    {
        JumpToSpecifiedPage();
    }

    private void TxtJumpPage_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            JumpToSpecifiedPage();
            InkLayer.Focus();
        }
    }

    private void TxtJumpPage_LostFocus(object sender, RoutedEventArgs e)
    {
        JumpToSpecifiedPage();
    }

    private void ListThumbnails_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListThumbnails.SelectedIndex >= 0 && ListThumbnails.SelectedIndex != _currentPageIndex)
        {
            SaveCurrentPageStrokes();
            _currentPageIndex = ListThumbnails.SelectedIndex;
            DisplayCurrentPage();
        }
    }

    private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        if (SidebarColumn.Width.Value > 0)
        {
            SidebarColumn.Width = new GridLength(0);
        }
        else
        {
            SidebarColumn.Width = new GridLength(220);
        }
    }

    // ==========================================
    // TOOLS & COLORS
    // ==========================================

    private void BtnToolPicker_Click(object sender, RoutedEventArgs e)
    {
        PopupToolPicker.IsOpen = !PopupToolPicker.IsOpen;
    }

    private void MenuTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string toolTag)
        {
            SelectTool(toolTag);
            PopupToolPicker.IsOpen = false;
        }
    }

    private void SelectTool(string toolTag)
    {
        _activeToolTag = toolTag;
        switch (toolTag)
        {
            case "Pen":
                InkLayer?.SetTool(ActiveTool.BallpointPen);
                if (TxtCurrentToolIcon != null) TxtCurrentToolIcon.Text = "🖊️";
                if (TxtCurrentToolName != null) TxtCurrentToolName.Text = LocalizationService.Get("ToolPen");
                break;
            case "Pencil":
                InkLayer?.SetTool(ActiveTool.Pencil);
                if (TxtCurrentToolIcon != null) TxtCurrentToolIcon.Text = "✏️";
                if (TxtCurrentToolName != null) TxtCurrentToolName.Text = LocalizationService.Get("ToolPencil");
                break;
            case "Highlighter":
                InkLayer?.SetTool(ActiveTool.Highlighter);
                if (TxtCurrentToolIcon != null) TxtCurrentToolIcon.Text = "🖍️";
                if (TxtCurrentToolName != null) TxtCurrentToolName.Text = LocalizationService.Get("ToolHighlighter");
                break;
            case "Eraser":
                InkLayer?.SetTool(ActiveTool.Eraser);
                if (TxtCurrentToolIcon != null) TxtCurrentToolIcon.Text = "🧹";
                if (TxtCurrentToolName != null) TxtCurrentToolName.Text = LocalizationService.Get("ToolEraser");
                break;
            case "Lasso":
                InkLayer?.SetTool(ActiveTool.LassoSelect);
                if (TxtCurrentToolIcon != null) TxtCurrentToolIcon.Text = "⭕";
                if (TxtCurrentToolName != null) TxtCurrentToolName.Text = LocalizationService.Get("ToolLasso");
                break;
        }
    }

    private void BtnColorPicker_Click(object sender, RoutedEventArgs e)
    {
        PopupColorPicker.IsOpen = !PopupColorPicker.IsOpen;
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            InkLayer.SetPenColor(color);
            if (CurrentColorIndicator != null)
            {
                CurrentColorIndicator.Background = new SolidColorBrush(color);
            }
            if (PopupColorPicker != null)
            {
                PopupColorPicker.IsOpen = false;
            }
        }
    }

    private void BtnDecreaseWidth_Click(object sender, RoutedEventArgs e)
    {
        double currentVal = Math.Round(SliderWidth.Value);
        double newVal = Math.Max(SliderWidth.Minimum, currentVal - 2);
        SliderWidth.Value = newVal;
    }

    private void BtnIncreaseWidth_Click(object sender, RoutedEventArgs e)
    {
        double currentVal = Math.Round(SliderWidth.Value);
        double newVal = Math.Min(SliderWidth.Maximum, currentVal + 2);
        SliderWidth.Value = newVal;
    }

    private void SliderWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtWidthValue != null)
        {
            TxtWidthValue.Text = $"{Math.Round(e.NewValue):0} pt";
        }
        InkLayer?.SetPenWidth(e.NewValue);
    }

    // ==========================================
    // UNDO / REDO / CLEAR
    // ==========================================

    private void Strokes_StrokesChanged(object? sender, StrokeCollectionChangedEventArgs e)
    {
        if (_isPerformingUndoRedo) return;

        // Push previous state for undo
        var snapshot = InkLayer.Strokes.Clone();
        _undoStack.Push(snapshot);
        _redoStack.Clear();
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 1)
        {
            _isPerformingUndoRedo = true;
            var current = _undoStack.Pop();
            _redoStack.Push(current);

            var previous = _undoStack.Peek();
            InkLayer.Strokes = previous.Clone();
            _isPerformingUndoRedo = false;
        }
        else if (_undoStack.Count == 1)
        {
            _isPerformingUndoRedo = true;
            _redoStack.Push(_undoStack.Pop());
            InkLayer.Strokes.Clear();
            _isPerformingUndoRedo = false;
        }
    }

    private void BtnRedo_Click(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count > 0)
        {
            _isPerformingUndoRedo = true;
            var next = _redoStack.Pop();
            _undoStack.Push(next.Clone());
            InkLayer.Strokes = next.Clone();
            _isPerformingUndoRedo = false;
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        if (InkLayer.Strokes.Count > 0)
        {
            var result = MessageBox.Show(LocalizationService.Get("DialogClearConfirm"), 
                                         LocalizationService.Get("DialogClearTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _undoStack.Push(InkLayer.Strokes.Clone());
                InkLayer.Strokes.Clear();
            }
        }
    }

    // ==========================================
    // ZOOM CONTROLS
    // ==========================================

    private void BtnZoomIn_Click(object sender, RoutedEventArgs e)
    {
        ApplyZoom(_zoomFactor + 0.15);
    }

    private void BtnZoomOut_Click(object sender, RoutedEventArgs e)
    {
        ApplyZoom(_zoomFactor - 0.15);
    }

    private void BtnFitPage_Click(object sender, RoutedEventArgs e)
    {
        FitPageToView();
    }

    private void ApplyZoom(double factor)
    {
        _zoomFactor = Math.Clamp(factor, 0.3, 3.5);
        CanvasScale.ScaleX = _zoomFactor;
        CanvasScale.ScaleY = _zoomFactor;
        TxtZoomLevel.Text = $"%{(int)(_zoomFactor * 100)}";
        CanvasContainer.UpdateLayout();
        UpdateLivePreviewDocumentCrop();
        NotifyRecordingCropAreaChanged();
    }

    private void FitPageToView()
    {
        if (_pages.Count == 0 || _currentPageIndex >= _pages.Count) return;

        double viewportWidth = MainScrollViewer.ActualWidth - 80;
        double viewportHeight = MainScrollViewer.ActualHeight - 80;

        if (viewportWidth > 100 && viewportHeight > 100)
        {
            var page = _pages[_currentPageIndex];
            double scaleX = viewportWidth / page.Width;
            double scaleY = viewportHeight / page.Height;
            double optimalScale = Math.Min(scaleX, scaleY);
            ApplyZoom(optimalScale);
        }
    }

    // ==========================================
    // MPP 2.0 HARDWARE TELEMETRY HUD
    // ==========================================

    private void InkLayer_PenTelemetryChanged(object? sender, PenTelemetryEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (e.IsPenPresent)
            {
                TxtTelemetryDevice.Text = $"{LocalizationService.Get("TelemetryPenReady")}: {e.DeviceName}";
                TxtTelemetryPressure.Text = LocalizationService.Format("TelemetryPressure", (int)(e.Pressure * 100));
                TxtTelemetryTilt.Text = LocalizationService.Format("TelemetryTilt", $"{(int)e.TiltX}° / {(int)e.TiltY}");

                if (e.IsInverted)
                {
                    TxtTelemetryMode.Text = LocalizationService.Get("TelemetryModeEraser");
                    TxtTelemetryMode.Foreground = Brushes.Crimson;
                }
                else if (e.IsBarrelDown)
                {
                    TxtTelemetryMode.Text = LocalizationService.Get("TelemetryModeLasso");
                    TxtTelemetryMode.Foreground = Brushes.Purple;
                }
                else
                {
                    TxtTelemetryMode.Text = LocalizationService.Get("TelemetryModeDefault");
                    TxtTelemetryMode.Foreground = (Brush)FindResource("AccentBrush");
                }
            }
        });
    }

    // ==========================================
    // TOUCH PAN & PINCH-TO-ZOOM MANIPULATION
    // ==========================================

    private void CheckTouchInking_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = CheckTouchInking.IsChecked == true;
        InkLayer.TouchInkingEnabled = isChecked;

        if (isChecked)
        {
            TxtTelemetryMode.Text = LocalizationService.Get("TelemetryModeTouch");
            TxtTelemetryMode.Foreground = Brushes.DarkOrange;
        }
        else
        {
            TxtTelemetryMode.Text = LocalizationService.Get("TelemetryModeDefault");
            TxtTelemetryMode.Foreground = (Brush)FindResource("AccentBrush");
        }
    }

    private void Canvas_ManipulationStarting(object? sender, System.Windows.Input.ManipulationStartingEventArgs e)
    {
        // If stylus is in contact or active, cancel palm-induced manipulation
        if (InkLayer.IsStylusEngaged())
        {
            e.Cancel();
            e.Handled = true;
            return;
        }

        e.ManipulationContainer = MainScrollViewer;
        e.Mode = System.Windows.Input.ManipulationModes.All;
        e.Handled = true;
    }

    private void Canvas_ManipulationDelta(object? sender, System.Windows.Input.ManipulationDeltaEventArgs e)
    {
        if (InkLayer.IsStylusEngaged())
        {
            e.Handled = true;
            return;
        }

        // Check if two-finger pinch-to-zoom is being performed
        double scaleDelta = (e.DeltaManipulation.Scale.X + e.DeltaManipulation.Scale.Y) / 2.0;
        bool isPinchZoom = Math.Abs(scaleDelta - 1.0) > 0.003;
        bool isMultiTouch = e.Manipulators.Count() >= 2 || isPinchZoom;

        // When touch inking is enabled, single finger is reserved for drawing strokes (not panning)
        if (InkLayer.TouchInkingEnabled && !isMultiTouch)
        {
            return;
        }

        Point origin = e.ManipulationOrigin;

        if (isPinchZoom)
        {
            // Point on the unscaled document canvas directly under the user's fingers:
            Point docPoint = MainScrollViewer.TranslatePoint(origin, InkLayer);

            double oldZoom = _zoomFactor;
            double targetZoom = Math.Clamp(_zoomFactor * scaleDelta, 0.25, 4.5);

            if (Math.Abs(targetZoom - oldZoom) > 0.001)
            {
                _zoomFactor = targetZoom;
                CanvasScale.ScaleX = _zoomFactor;
                CanvasScale.ScaleY = _zoomFactor;
                TxtZoomLevel.Text = $"%{(int)(_zoomFactor * 100)}";

                // Update layout bounds so ScrollViewer reflects the new scale
                CanvasContainer.UpdateLayout();

                // Find where the document point is now located in viewport coordinates:
                Point newPos = InkLayer.TranslatePoint(docPoint, MainScrollViewer);

                // Adjust scroll offsets so that the point stays pinned directly under the fingers:
                double shiftX = newPos.X - origin.X;
                double shiftY = newPos.Y - origin.Y;

                MainScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset + shiftX - e.DeltaManipulation.Translation.X);
                MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + shiftY - e.DeltaManipulation.Translation.Y);

                e.Handled = true;
                return;
            }
        }

        // 2. Pure 1-Finger Pan / Scroll (Up, Down, Left, Right)
        if (e.DeltaManipulation.Translation.X != 0 || e.DeltaManipulation.Translation.Y != 0)
        {
            double newH = MainScrollViewer.HorizontalOffset - e.DeltaManipulation.Translation.X;
            double newV = MainScrollViewer.VerticalOffset - e.DeltaManipulation.Translation.Y;

            MainScrollViewer.ScrollToHorizontalOffset(newH);
            MainScrollViewer.ScrollToVerticalOffset(newV);
        }

        e.Handled = true;
    }

    private void Canvas_ManipulationInertiaStarting(object? sender, System.Windows.Input.ManipulationInertiaStartingEventArgs e)
    {
        // Smooth inertia deceleration for natural finger swiping
        e.TranslationBehavior.DesiredDeceleration = 10.0 * 96.0 / (1000.0 * 1000.0);
        e.ExpansionBehavior.DesiredDeceleration = 0.1 * 96.0 / (1000.0 * 1000.0);
        e.Handled = true;
    }

    private void MainScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        // Ctrl + Mouse Wheel for pinpoint focal zoom
        if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
        {
            Point mousePos = e.GetPosition(MainScrollViewer);
            Point docPoint = MainScrollViewer.TranslatePoint(mousePos, InkLayer);

            double factor = e.Delta > 0 ? 1.15 : 0.87;
            double targetZoom = Math.Clamp(_zoomFactor * factor, 0.25, 4.5);

            _zoomFactor = targetZoom;
            CanvasScale.ScaleX = _zoomFactor;
            CanvasScale.ScaleY = _zoomFactor;
            TxtZoomLevel.Text = $"%{(int)(_zoomFactor * 100)}";

            CanvasContainer.UpdateLayout();

            Point newPos = InkLayer.TranslatePoint(docPoint, MainScrollViewer);
            MainScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset + (newPos.X - mousePos.X));
            MainScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset + (newPos.Y - mousePos.Y));

            e.Handled = true;
        }
    }

    private void ScrollViewer_ManipulationBoundaryFeedback(object? sender, System.Windows.Input.ManipulationBoundaryFeedbackEventArgs e)
    {
        // Strictly prevent Windows from translating/bouncing the entire application window
        e.Handled = true;
    }

    // ==========================================
    // LECTURE RECORDING STUDIO IMPLEMENTATION
    // ==========================================

    private void InitializeRecorder()
    {
        _recorderService = new LectureRecorderService();
        _recorderService.PreviewFrameAvailable += RecorderService_PreviewFrameAvailable;
        _recorderService.StateChanged += RecorderService_StateChanged;
        _recorderService.DurationChanged += RecorderService_DurationChanged;
        _recorderService.RecordingCompleted += RecorderService_RecordingCompleted;
        _recorderService.RecordingFailed += RecorderService_RecordingFailed;

        _audioMeterService = new AudioMeterService();
        _audioMeterService.PeakValueChanged += AudioMeter_PeakValueChanged;
    }

    private double _currentAudioDisplayLevel = 0.0;

    private void AudioMeter_PeakValueChanged(float peak)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (PrgMicLevel == null) return;

            // Sensitivity-tuned speech curve:
            // peak ranges ~ 0.0001 (silence) to 0.4+ (speech)
            double target = 0.0;
            if (peak > 0.00015f)
            {
                double volFactor = (SliderMicVolume != null ? SliderMicVolume.Value / 100.0 : 1.0);
                target = Math.Min(100.0, Math.Sqrt(peak) * 140.0 * volFactor);
            }

            // Studio VU Ballistics: fast attack, smooth decay
            if (target >= _currentAudioDisplayLevel)
            {
                _currentAudioDisplayLevel = target; // Instant attack
            }
            else
            {
                _currentAudioDisplayLevel = _currentAudioDisplayLevel * 0.80; // Smooth natural decay
                if (_currentAudioDisplayLevel < 0.5) _currentAudioDisplayLevel = 0.0;
            }

            PrgMicLevel.Value = _currentAudioDisplayLevel;

            // Dynamic color feedback: Green -> Dark Orange -> Crimson
            if (_currentAudioDisplayLevel > 80.0)
            {
                PrgMicLevel.Foreground = System.Windows.Media.Brushes.Crimson;
            }
            else if (_currentAudioDisplayLevel > 55.0)
            {
                PrgMicLevel.Foreground = System.Windows.Media.Brushes.DarkOrange;
            }
            else
            {
                PrgMicLevel.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#10B981")!;
            }
        });
    }

    private bool _isLectureMode = false;
    private bool _isPopulatingCameras = false;
    private bool _isPopulatingMics = false;
    private bool _isCameraMirrored = true;

    private void SetLectureRecordingMode(bool isLectureMode)
    {
        _isLectureMode = isLectureMode;

        if (isLectureMode)
        {
            EnsureSidebarOpen();
            if (SepLectureRecording != null) SepLectureRecording.Visibility = Visibility.Visible;
            if (PanelLectureRecording != null) PanelLectureRecording.Visibility = Visibility.Visible;

            PopulateCameras();
            PopulateMicrophones();
            UpdateCameraPlacementFromUI();
            _ = _recorderService?.StartCameraPreviewAsync(GetSelectedCameraId());
            _audioMeterService?.StartMonitoring(GetSelectedMicrophoneId());
        }
        else
        {
            if (SepLectureRecording != null) SepLectureRecording.Visibility = Visibility.Collapsed;
            if (PanelLectureRecording != null) PanelLectureRecording.Visibility = Visibility.Collapsed;

            _ = _recorderService?.StopCameraPreviewAsync();
            _audioMeterService?.StopMonitoring();
            _currentAudioDisplayLevel = 0.0;
            if (PrgMicLevel != null) PrgMicLevel.Value = 0;
        }
    }

    private void PopulateCameras()
    {
        if (CmbCameras == null || _recorderService == null) return;
        _isPopulatingCameras = true;
        try
        {
            CmbCameras.Items.Clear();
            var cameras = _recorderService.GetAvailableCameras();
            if (cameras.Count > 0)
            {
                foreach (var cam in cameras)
                {
                    CmbCameras.Items.Add(cam);
                }
                CmbCameras.SelectedIndex = 0;
            }
            else
            {
                CmbCameras.Items.Add(new CameraDeviceInfo { Name = LocalizationService.Get("CameraNone"), DeviceId = "" });
                CmbCameras.SelectedIndex = 0;
            }
        }
        finally
        {
            _isPopulatingCameras = false;
        }
    }

    private string? GetSelectedCameraId()
    {
        if (CmbCameras?.SelectedItem is CameraDeviceInfo cam && !string.IsNullOrEmpty(cam.DeviceId))
        {
            return cam.DeviceId;
        }
        return null;
    }

    private void CmbCameras_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPopulatingCameras && _isLectureMode && _recorderService != null && _recorderService.State == RecordingState.Stopped)
        {
            _ = _recorderService.StartCameraPreviewAsync(GetSelectedCameraId());
        }
    }

    private void PopulateMicrophones()
    {
        if (CmbMicrophones == null || _recorderService == null) return;
        _isPopulatingMics = true;
        try
        {
            CmbMicrophones.Items.Clear();
            var mics = _recorderService.GetAvailableMicrophones();
            if (mics.Count > 0)
            {
                foreach (var mic in mics)
                {
                    CmbMicrophones.Items.Add(mic);
                }
                CmbMicrophones.SelectedIndex = 0;
            }
            else
            {
                CmbMicrophones.Items.Add(new CameraDeviceInfo { Name = LocalizationService.Get("MicDefault"), DeviceId = "" });
                CmbMicrophones.SelectedIndex = 0;
            }
        }
        finally
        {
            _isPopulatingMics = false;
        }
    }

    private string? GetSelectedMicrophoneId()
    {
        if (CmbMicrophones?.SelectedItem is CameraDeviceInfo mic && !string.IsNullOrEmpty(mic.DeviceId))
        {
            return mic.DeviceId;
        }
        return null;
    }

    private void CmbMicrophones_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isPopulatingMics && _isLectureMode && _audioMeterService != null)
        {
            _audioMeterService.SetDevice(GetSelectedMicrophoneId());
        }
    }

    private void SliderMicVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtMicVolumeVal != null)
        {
            TxtMicVolumeVal.Text = $"%{(int)e.NewValue}";
        }
    }

    private void BtnToggleMirror_Click(object sender, RoutedEventArgs e)
    {
        _isCameraMirrored = !_isCameraMirrored;
        double scaleX = _isCameraMirrored ? -1 : 1;
        if (PreviewCameraScale != null)
        {
            PreviewCameraScale.ScaleX = scaleX;
        }
        if (PreviewPipCameraScale != null)
        {
            PreviewPipCameraScale.ScaleX = scaleX;
        }
        if (TxtMirrorIcon != null)
        {
            TxtMirrorIcon.Text = _isCameraMirrored ? LocalizationService.Get("BtnMirror") : "Normal";
        }
    }

    private void BtnPreviewMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            if (tag == "Live")
            {
                if (PanelLiveRecordingPreview != null) PanelLiveRecordingPreview.Visibility = Visibility.Visible;
                if (PanelFullCameraPreview != null) PanelFullCameraPreview.Visibility = Visibility.Collapsed;
                if (BtnPreviewModeLive != null)
                {
                    BtnPreviewModeLive.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    if (BtnPreviewModeLive.Content is TextBlock tb) tb.Foreground = Brushes.White;
                }
                if (BtnPreviewModeCam != null)
                {
                    BtnPreviewModeCam.Background = Brushes.Transparent;
                    if (BtnPreviewModeCam.Content is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                }
            }
            else
            {
                if (PanelLiveRecordingPreview != null) PanelLiveRecordingPreview.Visibility = Visibility.Collapsed;
                if (PanelFullCameraPreview != null) PanelFullCameraPreview.Visibility = Visibility.Visible;
                if (BtnPreviewModeCam != null)
                {
                    BtnPreviewModeCam.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    if (BtnPreviewModeCam.Content is TextBlock tb) tb.Foreground = Brushes.White;
                }
                if (BtnPreviewModeLive != null)
                {
                    BtnPreviewModeLive.Background = Brushes.Transparent;
                    if (BtnPreviewModeLive.Content is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                }
            }
        }
    }

    private void BtnToggleLayoutPreview_Click(object sender, RoutedEventArgs e)
    {
        if (PanelCameraLayout != null)
        {
            PanelCameraLayout.Visibility = PanelCameraLayout.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }

    private void CornerZone_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            switch (tag)
            {
                case "TopLeft":
                    if (CmbCameraPosition != null) CmbCameraPosition.SelectedIndex = 3;
                    break;
                case "TopRight":
                    if (CmbCameraPosition != null) CmbCameraPosition.SelectedIndex = 2;
                    break;
                case "BottomLeft":
                    if (CmbCameraPosition != null) CmbCameraPosition.SelectedIndex = 1;
                    break;
                case "BottomRight":
                default:
                    if (CmbCameraPosition != null) CmbCameraPosition.SelectedIndex = 0;
                    break;
            }
        }
    }

    private void CmbCameraPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateCameraPlacementFromUI();
    }

    private void CmbCameraAspect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelCustomAspect != null)
        {
            PanelCustomAspect.Visibility = (CmbCameraAspect?.SelectedIndex == 4) ? Visibility.Visible : Visibility.Collapsed;
        }
        UpdateCameraPlacementFromUI();
    }

    private void SliderCameraSize_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtCameraSizeVal != null)
        {
            TxtCameraSizeVal.Text = $"%{(int)e.NewValue}";
        }
        UpdateCameraPlacementFromUI();
    }

    private void BtnCamSizePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out double val))
        {
            if (SliderCameraSize != null)
            {
                SliderCameraSize.Value = val;
            }
        }
    }

    private void SliderCustomAspect_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtCustomAspectVal != null)
        {
            TxtCustomAspectVal.Text = $"{e.NewValue:0.00}";
        }
        UpdateCameraPlacementFromUI();
    }

    private void PanelLiveRecordingPreview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateCameraPlacementFromUI();
        UpdateLivePreviewDocumentCrop();
    }

    private void CmbAspectRatio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentTargetRatio = GetTargetRatioFromUI();
        UpdateCameraPlacementFromUI();
        UpdateLivePreviewDocumentCrop();
        NotifyRecordingCropAreaChanged();
    }

    private double GetTargetRatioFromUI()
    {
        double docAspect = 16.0 / 9.0;
        if (_pages.Count > 0 && _currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
        {
            var page = _pages[_currentPageIndex];
            if (page.Width > 0 && page.Height > 0)
            {
                docAspect = page.Width / page.Height;
            }
        }
        else if (DocumentPaperBorder != null && DocumentPaperBorder.ActualWidth > 0 && DocumentPaperBorder.ActualHeight > 0)
        {
            docAspect = DocumentPaperBorder.ActualWidth / DocumentPaperBorder.ActualHeight;
        }

        int aspectChoice = CmbAspectRatio?.SelectedIndex ?? 0;
        return aspectChoice switch
        {
            0 => docAspect,         // Belgeye Uygun
            1 => 16.0 / 9.0,        // 16:9 Full HD
            2 => 16.0 / 9.0,        // 16:9 HD
            3 => 4.0 / 3.0,         // 4:3 Klasik
            4 => 9.0 / 16.0,        // 9:16 Dikey
            5 => 1.0,               // 1:1 Kare
            _ => docAspect
        };
    }

    private void UpdateCameraPlacementFromUI()
    {
        Anchor anchor = Anchor.BottomRight;
        string posName = LocalizationService.Get("PosBottomRight");
        HorizontalAlignment hAlign = HorizontalAlignment.Right;
        VerticalAlignment vAlign = VerticalAlignment.Bottom;

        int posIdx = CmbCameraPosition?.SelectedIndex ?? 0;
        switch (posIdx)
        {
            case 1:
                anchor = Anchor.BottomLeft;
                posName = LocalizationService.Get("PosBottomLeft");
                hAlign = HorizontalAlignment.Left;
                vAlign = VerticalAlignment.Bottom;
                break;
            case 2:
                anchor = Anchor.TopRight;
                posName = LocalizationService.Get("PosTopRight");
                hAlign = HorizontalAlignment.Right;
                vAlign = VerticalAlignment.Top;
                break;
            case 3:
                anchor = Anchor.TopLeft;
                posName = LocalizationService.Get("PosTopLeft");
                hAlign = HorizontalAlignment.Left;
                vAlign = VerticalAlignment.Top;
                break;
            case 0:
            default:
                anchor = Anchor.BottomRight;
                posName = LocalizationService.Get("PosBottomRight");
                hAlign = HorizontalAlignment.Right;
                vAlign = VerticalAlignment.Bottom;
                break;
        }

        double sizeRatio = SliderCameraSize != null ? Math.Clamp(SliderCameraSize.Value / 100.0, 0.08, 0.60) : 0.22;
        string sizeName = $"%{(int)(sizeRatio * 100)}";

        int aspectIdx = CmbCameraAspect?.SelectedIndex ?? 0;
        CameraAspectMode camAspect = CameraAspectMode.Aspect16x9;
        double camAspectVal = 16.0 / 9.0;
        string aspectName = "16:9";

        switch (aspectIdx)
        {
            case 1:
                camAspect = CameraAspectMode.Aspect4x3;
                camAspectVal = 4.0 / 3.0;
                aspectName = "4:3";
                break;
            case 2:
                camAspect = CameraAspectMode.Aspect1x1;
                camAspectVal = 1.0;
                aspectName = "1:1";
                break;
            case 3:
                camAspect = CameraAspectMode.Aspect9x16;
                camAspectVal = 9.0 / 16.0;
                aspectName = "9:16";
                break;
            case 4:
                camAspect = CameraAspectMode.Custom;
                camAspectVal = SliderCustomAspect != null ? Math.Clamp(SliderCustomAspect.Value, 0.3, 3.5) : 1.78;
                aspectName = $"{camAspectVal:0.00}";
                break;
            case 0:
            default:
                camAspect = CameraAspectMode.Aspect16x9;
                camAspectVal = 16.0 / 9.0;
                aspectName = "16:9";
                break;
        }

        _currentTargetRatio = GetTargetRatioFromUI();

        // 1. Proportional Miniature Canvas sizing based on video aspect ratio
        double maxMiniW = 160.0;
        double maxMiniH = 76.0;
        double miniW, miniH;

        if (_currentTargetRatio >= 1.0)
        {
            miniW = Math.Min(maxMiniW, maxMiniH * _currentTargetRatio);
            miniH = miniW / _currentTargetRatio;
            if (miniH > maxMiniH)
            {
                miniH = maxMiniH;
                miniW = miniH * _currentTargetRatio;
            }
        }
        else
        {
            miniH = maxMiniH;
            miniW = Math.Min(maxMiniW, miniH * _currentTargetRatio);
        }

        if (MiniSlideFrame != null)
        {
            MiniSlideFrame.Width = Math.Max(35, miniW);
            MiniSlideFrame.Height = Math.Max(35, miniH);
        }

        int videoChoice = CmbAspectRatio?.SelectedIndex ?? 0;
        string aspectTag = videoChoice switch
        {
            0 => LocalizationService.Get("MiniAspectDoc"),
            1 or 2 => "16:9",
            3 => "4:3",
            4 => "9:16",
            5 => "1:1",
            _ => $"{_currentTargetRatio:0.00}"
        };

        if (TxtMiniSlideAspect != null)
        {
            TxtMiniSlideAspect.Text = videoChoice switch
            {
                0 => LocalizationService.Get("MiniAspectDocLabel"),
                1 or 2 => LocalizationService.Get("MiniAspect16x9"),
                3 => LocalizationService.Get("MiniAspect4x3"),
                4 => LocalizationService.Get("MiniAspect9x16"),
                5 => LocalizationService.Get("MiniAspect1x1"),
                _ => $"{_currentTargetRatio:0.00}"
            };
        }

        // 2. Camera Badge sizing inside Miniature Slide Canvas
        double badgeW = Math.Max(16, miniW * sizeRatio);
        double badgeH = Math.Max(12, badgeW / camAspectVal);
        if (badgeH > miniH - 4)
        {
            badgeH = Math.Max(10, miniH - 4);
            badgeW = badgeH * camAspectVal;
        }

        if (MiniCameraBadge != null)
        {
            MiniCameraBadge.HorizontalAlignment = hAlign;
            MiniCameraBadge.VerticalAlignment = vAlign;
            MiniCameraBadge.Width = badgeW;
            MiniCameraBadge.Height = badgeH;
        }

        // 3. Proportional Live Preview Viewport sizing based on video aspect ratio
        double maxLiveW = (PanelLiveRecordingPreview?.ActualWidth > 50) ? PanelLiveRecordingPreview.ActualWidth - 6 : 228.0;
        double maxLiveH = (PanelLiveRecordingPreview?.ActualHeight > 50) ? PanelLiveRecordingPreview.ActualHeight - 6 : 120.0;
        double liveVpW, liveVpH;

        if (_currentTargetRatio >= (maxLiveW / maxLiveH))
        {
            liveVpW = maxLiveW;
            liveVpH = liveVpW / _currentTargetRatio;
            if (liveVpH > maxLiveH)
            {
                liveVpH = maxLiveH;
                liveVpW = liveVpH * _currentTargetRatio;
            }
        }
        else
        {
            liveVpH = maxLiveH;
            liveVpW = liveVpH * _currentTargetRatio;
            if (liveVpW > maxLiveW)
            {
                liveVpW = maxLiveW;
                liveVpH = liveVpW / _currentTargetRatio;
            }
        }

        if (LivePreviewViewport != null)
        {
            LivePreviewViewport.Width = Math.Round(Math.Max(35, liveVpW));
            LivePreviewViewport.Height = Math.Round(Math.Max(35, liveVpH));
        }

        // 4. PIP Camera in Live Recording Preview Box
        double pipW = Math.Max(18, liveVpW * sizeRatio);
        double pipH = Math.Max(12, pipW / camAspectVal);
        if (pipH > liveVpH - 6)
        {
            pipH = Math.Max(10, liveVpH - 6);
            pipW = pipH * camAspectVal;
        }

        if (PipCameraContainer != null)
        {
            PipCameraContainer.HorizontalAlignment = hAlign;
            PipCameraContainer.VerticalAlignment = vAlign;
            PipCameraContainer.Width = Math.Round(pipW);
            PipCameraContainer.Height = Math.Round(pipH);
        }

        if (TxtCameraPlacementBadge != null)
        {
            TxtCameraPlacementBadge.Text = $"[{aspectTag}] {posName} ({sizeName}, {aspectName})";
        }

        if (BadgeCameraPlacement != null)
        {
            // Position badge opposite to the camera so they never overlap
            BadgeCameraPlacement.HorizontalAlignment = (hAlign == HorizontalAlignment.Right)
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Right;
            BadgeCameraPlacement.VerticalAlignment = VerticalAlignment.Bottom;
        }

        // 5. Update Recorder Service
        if (_recorderService != null)
        {
            _recorderService.CameraAnchor = anchor;
            _recorderService.CameraSizeRatio = sizeRatio;
            _recorderService.CameraAspectRatio = camAspect;
            _recorderService.CustomCameraAspectRatio = camAspectVal;

            if (_recorderService.State == RecordingState.Recording)
            {
                _recorderService.UpdateDynamicCameraOverlay(anchor, sizeRatio, camAspect, camAspectVal);
            }
        }
    }

    private void UpdateLivePreviewDocumentCrop()
    {
        if (DocumentPaperBorder == null || MainScrollViewer == null || LiveDocBrush == null) return;
        if (DocumentPaperBorder.ActualWidth <= 0 || DocumentPaperBorder.ActualHeight <= 0) return;

        try
        {
            // Find which sub-rectangle of DocumentPaperBorder is currently visible in MainScrollViewer
            Point p1 = MainScrollViewer.TranslatePoint(new Point(0, 0), DocumentPaperBorder);
            Point p2 = MainScrollViewer.TranslatePoint(new Point(MainScrollViewer.ActualWidth, MainScrollViewer.ActualHeight), DocumentPaperBorder);

            double docLeft = Math.Max(0, p1.X);
            double docTop = Math.Max(0, p1.Y);
            double docRight = Math.Min(DocumentPaperBorder.ActualWidth, p2.X);
            double docBottom = Math.Min(DocumentPaperBorder.ActualHeight, p2.Y);

            double docW = Math.Max(20, docRight - docLeft);
            double docH = Math.Max(20, docBottom - docTop);
            double currentRatio = docW / docH;

            double finalW = docW;
            double finalH = docH;
            double finalX = docLeft;
            double finalY = docTop;

            if (currentRatio > _currentTargetRatio)
            {
                finalH = docH;
                finalW = finalH * _currentTargetRatio;
                finalX = docLeft + (docW - finalW) / 2.0;
            }
            else
            {
                finalW = docW;
                finalH = finalW / _currentTargetRatio;
                finalY = docTop + (docH - finalH) / 2.0;
            }

            LiveDocBrush.ViewboxUnits = BrushMappingMode.Absolute;
            LiveDocBrush.Viewbox = new Rect(finalX, finalY, finalW, finalH);
        }
        catch { }
    }

    private void RecorderService_PreviewFrameAvailable(BitmapSource frame)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (ImgRecorderPreview != null)
            {
                ImgRecorderPreview.Source = frame;
            }
            if (ImgPipRecorderPreview != null)
            {
                ImgPipRecorderPreview.Source = frame;
            }
            if (PanelPreviewPlaceholder != null)
            {
                PanelPreviewPlaceholder.Visibility = Visibility.Collapsed;
            }
        });
    }

    private void RecorderService_StateChanged(RecordingState state)
    {
        Dispatcher.InvokeAsync(() =>
        {
            switch (state)
            {
                case RecordingState.Recording:
                    TxtRecordingStatus.Text = LocalizationService.Get("StatusRecording");
                    BadgeRecordingStatus.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    TxtRecordingStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    BtnRecordStart.IsEnabled = false;
                    BtnRecordPause.IsEnabled = true;
                    BtnRecordStop.IsEnabled = true;
                    TxtRecordPauseIcon.Text = "⏸️";
                    if (ImgRecorderPreview?.Source != null)
                    {
                        PanelPreviewPlaceholder.Visibility = Visibility.Collapsed;
                    }
                    break;

                case RecordingState.Paused:
                    TxtRecordingStatus.Text = LocalizationService.Get("StatusPaused");
                    BadgeRecordingStatus.Background = new SolidColorBrush(Color.FromRgb(254, 240, 138));
                    TxtRecordingStatus.Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7));
                    TxtRecordPauseIcon.Text = "▶️";
                    break;

                case RecordingState.Stopped:
                    TxtRecordingStatus.Text = LocalizationService.Get("StatusReady");
                    BadgeRecordingStatus.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                    TxtRecordingStatus.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                    BtnRecordStart.IsEnabled = true;
                    BtnRecordPause.IsEnabled = false;
                    BtnRecordStop.IsEnabled = false;
                    TxtRecordPauseIcon.Text = "⏸️";
                    TxtRecordingDuration.Text = "00:00:00";
                    TxtPreviewPlaceholder.Text = LocalizationService.Get("CameraPlaceholder");
                    if (ImgRecorderPreview?.Source == null)
                    {
                        PanelPreviewPlaceholder.Visibility = Visibility.Visible;
                    }
                    break;
            }
        });
    }

    private void RecorderService_DurationChanged(TimeSpan duration)
    {
        Dispatcher.InvokeAsync(() =>
        {
            TxtRecordingDuration.Text = duration.ToString(@"hh\:mm\:ss");
        });
    }

    private void RecorderService_RecordingCompleted(string filePath)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var res = MessageBox.Show(
                LocalizationService.Format("DialogRecordSuccessMsg", filePath),
                LocalizationService.Get("DialogRecordSuccessTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
                catch { }
            }
        });
    }

    private void RecorderService_RecordingFailed(string error)
    {
        Dispatcher.InvokeAsync(() =>
        {
            MessageBox.Show(LocalizationService.Format("DialogRecordErrorMsg", error), LocalizationService.Get("DialogRecordErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private void BtnWelcomeRecord_Click(object sender, RoutedEventArgs e)
    {
        LectureChoiceModal.Visibility = Visibility.Visible;
    }

    private void BtnLectureChoiceCancel_Click(object sender, RoutedEventArgs e)
    {
        LectureChoiceModal.Visibility = Visibility.Collapsed;
    }

    private void BtnLectureChoice16x9_Click(object sender, RoutedEventArgs e)
    {
        LectureChoiceModal.Visibility = Visibility.Collapsed;
        CreateBlankDocument(1920, 1080, LocalizationService.Get("LectureNoteTitle"), isLectureMode: true);
    }

    private void BtnLectureChoicePdf_Click(object sender, RoutedEventArgs e)
    {
        LectureChoiceModal.Visibility = Visibility.Collapsed;
        var dlg = new OpenFileDialog
        {
            Title = LocalizationService.Get("ModalChoice2Title"),
            Filter = LocalizationService.Get("DialogFilterAllSupported")
        };
        if (dlg.ShowDialog() == true)
        {
            LoadDocument(dlg.FileName, isLectureMode: true);
        }
    }

    private void EnsureSidebarOpen()
    {
        if (SidebarColumn != null && SidebarColumn.Width.Value < 50)
        {
            SidebarColumn.Width = new GridLength(260);
        }
    }

    private double _currentTargetRatio = 16.0 / 9.0;
    private DateTime _lastCropUpdateTime = DateTime.MinValue;

    public ScreenRect? CalculateDocumentCropRect(double targetRatio)
    {
        if (DocumentPaperBorder != null && DocumentPaperBorder.IsVisible && MainScrollViewer != null &&
            MainScrollViewer.ActualWidth > 100 && MainScrollViewer.ActualHeight > 100)
        {
            try
            {
                Point scrollViewerTopLeft = MainScrollViewer.PointToScreen(new Point(0, 0));
                Point scrollViewerBottomRight = MainScrollViewer.PointToScreen(new Point(MainScrollViewer.ActualWidth, MainScrollViewer.ActualHeight));

                Point paperTopLeft = DocumentPaperBorder.PointToScreen(new Point(0, 0));
                Point paperBottomRight = DocumentPaperBorder.PointToScreen(new Point(DocumentPaperBorder.ActualWidth, DocumentPaperBorder.ActualHeight));

                // Visible paper area strictly inside scroll viewer viewport
                double visLeft = Math.Max(scrollViewerTopLeft.X + 2, paperTopLeft.X);
                double visTop = Math.Max(scrollViewerTopLeft.Y + 2, paperTopLeft.Y);
                double visRight = Math.Min(scrollViewerBottomRight.X - 2, paperBottomRight.X);
                double visBottom = Math.Min(scrollViewerBottomRight.Y - 2, paperBottomRight.Y);

                double visWidth = Math.Max(50, visRight - visLeft);
                double visHeight = Math.Max(50, visBottom - visTop);
                double visRatio = visWidth / visHeight;

                // Adjust crop within the visible PDF to exactly match targetRatio
                // so that the output video is 100% covered by the PDF without black bars!
                double finalW = visWidth;
                double finalH = visHeight;
                double finalX = visLeft;
                double finalY = visTop;

                if (visRatio > targetRatio)
                {
                    // Paper is wider than video ratio -> fit height, center width
                    finalH = visHeight;
                    finalW = finalH * targetRatio;
                    finalX = visLeft + (visWidth - finalW) / 2.0;
                }
                else
                {
                    // Paper is taller than video ratio -> fit width, center height
                    finalW = visWidth;
                    finalH = finalW / targetRatio;
                    finalY = visTop + (visHeight - finalH) / 2.0;
                }

                int cropWidth = Math.Max(100, (int)Math.Round(finalW));
                int cropHeight = Math.Max(100, (int)Math.Round(finalH));
                int cropLeft = Math.Max(0, (int)Math.Round(finalX));
                int cropTop = Math.Max(0, (int)Math.Round(finalY));

                // Video encoders require even dimensions
                if (cropWidth % 2 != 0) cropWidth -= 1;
                if (cropHeight % 2 != 0) cropHeight -= 1;

                return new ScreenRect(cropLeft, cropTop, cropWidth, cropHeight);
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    private void NotifyRecordingCropAreaChanged()
    {
        if (_recorderService?.State == RecordingState.Recording)
        {
            var now = DateTime.UtcNow;
            if ((now - _lastCropUpdateTime).TotalMilliseconds < 35) return;
            _lastCropUpdateTime = now;

            var crop = CalculateDocumentCropRect(_currentTargetRatio);
            if (crop != null)
            {
                _recorderService.UpdateDynamicCropArea(crop);
            }
        }
    }

    private async void BtnRecordStart_Click(object sender, RoutedEventArgs e)
    {
        if (_recorderService == null) return;

        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "InkStudio");
            Directory.CreateDirectory(folder);
            string fileName = $"Lecture_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            string targetFile = Path.Combine(folder, fileName);

            // 1. Determine Document Aspect Ratio and Target Output Resolution
            double docAspect = 16.0 / 9.0;
            if (_pages.Count > 0 && _currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
            {
                var currentPage = _pages[_currentPageIndex];
                if (currentPage.Width > 0 && currentPage.Height > 0)
                {
                    docAspect = currentPage.Width / currentPage.Height;
                }
            }

            int width = 1920;
            int height = 1080;
            double targetRatio = docAspect;
            int aspectChoice = CmbAspectRatio?.SelectedIndex ?? 0;

            switch (aspectChoice)
            {
                case 0: // Belgeye Uygun (Sıfır Boşluk - Tam Dolu)
                    targetRatio = docAspect;
                    if (docAspect >= 1.0)
                    {
                        // Yatay belge: 1080p bazında en-boy hesapla
                        height = 1080;
                        width = (int)Math.Round(1080.0 * docAspect);
                        if (width > 1920)
                        {
                            width = 1920;
                            height = (int)Math.Round(1920.0 / docAspect);
                        }
                    }
                    else
                    {
                        // Dikey belge (A4 vb.): 1080p genişlik bazında yükseklik hesapla
                        width = 1080;
                        height = (int)Math.Round(1080.0 / docAspect);
                        if (height > 1920)
                        {
                            height = 1920;
                            width = (int)Math.Round(1920.0 * docAspect);
                        }
                    }
                    break;
                case 1: // 16:9 Full HD
                    targetRatio = 16.0 / 9.0;
                    width = 1920;
                    height = 1080;
                    break;
                case 2: // 16:9 HD
                    targetRatio = 16.0 / 9.0;
                    width = 1280;
                    height = 720;
                    break;
                case 3: // 4:3 Klasik Sunum
                    targetRatio = 4.0 / 3.0;
                    width = 1440;
                    height = 1080;
                    break;
                case 4: // 9:16 Dikey Video
                    targetRatio = 9.0 / 16.0;
                    width = 1080;
                    height = 1920;
                    break;
                case 5: // 1:1 Kare Video
                    targetRatio = 1.0;
                    width = 1080;
                    height = 1080;
                    break;
            }

            // Enforce even dimensions for H.264
            if (width % 2 != 0) width -= 1;
            if (height % 2 != 0) height -= 1;

            int fps = 30;
            if (CmbFps?.SelectedIndex == 1)
            {
                fps = 60;
            }

            string? camId = GetSelectedCameraId();
            string? micId = GetSelectedMicrophoneId();
            float micVol = SliderMicVolume != null ? (float)(SliderMicVolume.Value / 100.0) : 1.0f;

            // 2. Fit page to view so the whole document/slide is cleanly visible without scroll truncation
            FitPageToView();
            UpdateLayout();

            _currentTargetRatio = targetRatio;

            // 3. Crop recording strictly to the Document Paper matching targetRatio
            // GUARANTEED: The entire video is 100% PDF (zero black bars, zero window/taskbar leakage)
            ScreenRect? cropRect = CalculateDocumentCropRect(_currentTargetRatio);

            IntPtr hwnd = new WindowInteropHelper(this).Handle;

            await _recorderService.StartRecordingAsync(hwnd, targetFile, width, height, fps, camId, micId, micVol, cropRect);
        }
        catch (Exception ex)
        {
            MessageBox.Show(LocalizationService.Format("DialogRecordStartError", ex.Message), LocalizationService.Get("DialogErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnRecordPause_Click(object sender, RoutedEventArgs e)
    {
        if (_recorderService == null) return;
        if (_recorderService.State == RecordingState.Recording)
        {
            _recorderService.PauseRecording();
        }
        else if (_recorderService.State == RecordingState.Paused)
        {
            _recorderService.ResumeRecording();
        }
    }

    private void BtnRecordStop_Click(object sender, RoutedEventArgs e)
    {
        TxtRecordingStatus.Text = LocalizationService.Get("StatusSaving");
        BadgeRecordingStatus.Background = new SolidColorBrush(Color.FromRgb(254, 240, 138));
        TxtRecordingStatus.Foreground = new SolidColorBrush(Color.FromRgb(161, 98, 7));
        BtnRecordStop.IsEnabled = false;
        BtnRecordPause.IsEnabled = false;
        _recorderService?.StopRecording();
    }

    private void BtnShowRecordings_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "InkStudio");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(LocalizationService.Format("DialogFolderOpenError", ex.Message), LocalizationService.Get("DialogErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}