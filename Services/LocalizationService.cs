using System;
using System.Collections.Generic;

namespace InkStudio.Services;

public enum AppLanguage
{
    English,
    Turkish,
    German,
    Russian,
    Chinese
}

public static class LocalizationService
{
    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.English;

    public static event Action? LanguageChanged;

    public static void SetLanguage(AppLanguage language)
    {
        if (CurrentLanguage == language) return;
        CurrentLanguage = language;
        LanguageChanged?.Invoke();
    }

    public static void SetLanguageByCode(string code)
    {
        AppLanguage lang = code.ToLowerInvariant() switch
        {
            "tr" or "turkish" => AppLanguage.Turkish,
            "de" or "german" => AppLanguage.German,
            "ru" or "russian" => AppLanguage.Russian,
            "zh" or "chinese" => AppLanguage.Chinese,
            _ => AppLanguage.English
        };
        SetLanguage(lang);
    }

    public static string GetLanguageCode() => CurrentLanguage switch
    {
        AppLanguage.Turkish => "tr",
        AppLanguage.German => "de",
        AppLanguage.Russian => "ru",
        AppLanguage.Chinese => "zh",
        _ => "en"
    };

    public static string GetLanguageFlag() => CurrentLanguage switch
    {
        AppLanguage.Turkish => "🇹🇷",
        AppLanguage.German => "🇩🇪",
        AppLanguage.Russian => "🇷🇺",
        AppLanguage.Chinese => "🇨🇳",
        _ => "🇬🇧"
    };

    public static string GetLanguageDisplayName() => CurrentLanguage switch
    {
        AppLanguage.Turkish => "Türkçe",
        AppLanguage.German => "Deutsch",
        AppLanguage.Russian => "Русский",
        AppLanguage.Chinese => "中文",
        _ => "English"
    };

    public static string Get(string key)
    {
        if (Translations.TryGetValue(key, out var dict))
        {
            if (dict.TryGetValue(CurrentLanguage, out var val))
            {
                return val;
            }
            if (dict.TryGetValue(AppLanguage.English, out var enVal))
            {
                return enVal;
            }
        }
        return key;
    }

    public static string Format(string key, params object[] args)
    {
        string pattern = Get(key);
        try
        {
            return string.Format(pattern, args);
        }
        catch
        {
            return pattern;
        }
    }

    private static readonly Dictionary<string, Dictionary<AppLanguage, string>> Translations = new()
    {
        // Welcome Screen
        ["WelcomeSubtitle"] = new()
        {
            [AppLanguage.English] = "MPP 2.0 Pen & Document Studio",
            [AppLanguage.Turkish] = "MPP 2.0 Kalem ve Belge Stüdyosu",
            [AppLanguage.German] = "MPP 2.0 Stift- & Dokumentenstudio",
            [AppLanguage.Russian] = "Студия пера и документов MPP 2.0",
            [AppLanguage.Chinese] = "MPP 2.0 手写笔与文档工作室"
        },
        ["CardNewTitle"] = new()
        {
            [AppLanguage.English] = "Create New PDF",
            [AppLanguage.Turkish] = "Yeni PDF Oluştur",
            [AppLanguage.German] = "Neues PDF erstellen",
            [AppLanguage.Russian] = "Создать новый PDF",
            [AppLanguage.Chinese] = "新建 PDF"
        },
        ["CardNewDesc"] = new()
        {
            [AppLanguage.English] = "Start drawing and taking notes on a blank page",
            [AppLanguage.Turkish] = "Boş bir sayfa açarak çizmeye ve not almaya başlayın",
            [AppLanguage.German] = "Auf einer leeren Seite zeichnen und Notizen machen",
            [AppLanguage.Russian] = "Начните рисовать и делать заметки на чистом листе",
            [AppLanguage.Chinese] = "在空白页上开始绘制和做笔记"
        },
        ["CardOpenTitle"] = new()
        {
            [AppLanguage.English] = "Open File",
            [AppLanguage.Turkish] = "Dosya Aç",
            [AppLanguage.German] = "Datei öffnen",
            [AppLanguage.Russian] = "Открыть файл",
            [AppLanguage.Chinese] = "打开文件"
        },
        ["CardOpenDesc"] = new()
        {
            [AppLanguage.English] = "Open a PDF or PowerPoint document from your device",
            [AppLanguage.Turkish] = "Cihazınızdaki PDF veya PowerPoint dosyasını açın",
            [AppLanguage.German] = "PDF- oder PowerPoint-Dokument von Ihrem Gerät öffnen",
            [AppLanguage.Russian] = "Открыть файл PDF или PowerPoint с вашего устройства",
            [AppLanguage.Chinese] = "打开设备上的 PDF 或 PowerPoint 文件"
        },
        ["CardRecordTitle"] = new()
        {
            [AppLanguage.English] = "Lecture Recording",
            [AppLanguage.Turkish] = "Ders Kaydı",
            [AppLanguage.German] = "Vorlesungsaufnahme",
            [AppLanguage.Russian] = "Запись лекции",
            [AppLanguage.Chinese] = "课程录制"
        },
        ["CardRecordDesc"] = new()
        {
            [AppLanguage.English] = "Record lectures on 16:9 board or PDF with camera and microphone",
            [AppLanguage.Turkish] = "16:9 tahta veya PDF ile kamera ve sesli ders kaydedin",
            [AppLanguage.German] = "Vorlesungen auf 16:9 Tafel oder PDF mit Kamera und Mikrofon aufnehmen",
            [AppLanguage.Russian] = "Запись лекций на доске 16:9 или PDF с камерой и микрофоном",
            [AppLanguage.Chinese] = "使用 16:9 白板或 PDF 录制包含摄像头和麦克风的课程"
        },
        ["BadgePressure"] = new()
        {
            [AppLanguage.English] = "4096 Levels MPP Hardware Pressure",
            [AppLanguage.Turkish] = "4096 Seviye MPP Donanım Basıncı",
            [AppLanguage.German] = "4096 Stufen MPP-Hardware-Druck",
            [AppLanguage.Russian] = "4096 уровней аппаратного давления MPP",
            [AppLanguage.Chinese] = "4096 级 MPP 硬件压感"
        },
        ["BadgeTilt"] = new()
        {
            [AppLanguage.English] = "Tilt Detection",
            [AppLanguage.Turkish] = "Eğim Algılama",
            [AppLanguage.German] = "Neigungserkennung",
            [AppLanguage.Russian] = "Распознавание наклона",
            [AppLanguage.Chinese] = "倾斜感应"
        },
        ["BadgePalm"] = new()
        {
            [AppLanguage.English] = "Palm Rejection",
            [AppLanguage.Turkish] = "Avuç İçi Engelleme (Palm Rejection)",
            [AppLanguage.German] = "Handflächenunterdrückung",
            [AppLanguage.Russian] = "Защита от касания ладонью",
            [AppLanguage.Chinese] = "防误触 (Palm Rejection)"
        },

        // Top Toolbar
        ["BtnOpen"] = new()
        {
            [AppLanguage.English] = "Open",
            [AppLanguage.Turkish] = "Aç",
            [AppLanguage.German] = "Öffnen",
            [AppLanguage.Russian] = "Открыть",
            [AppLanguage.Chinese] = "打开"
        },
        ["BtnSave"] = new()
        {
            [AppLanguage.English] = "Save",
            [AppLanguage.Turkish] = "Kaydet",
            [AppLanguage.German] = "Speichern",
            [AppLanguage.Russian] = "Сохранить",
            [AppLanguage.Chinese] = "保存"
        },
        ["BtnNew"] = new()
        {
            [AppLanguage.English] = "New",
            [AppLanguage.Turkish] = "Yeni",
            [AppLanguage.German] = "Neu",
            [AppLanguage.Russian] = "Новый",
            [AppLanguage.Chinese] = "新建"
        },
        ["BtnPages"] = new()
        {
            [AppLanguage.English] = "Pages",
            [AppLanguage.Turkish] = "Sayfalar",
            [AppLanguage.German] = "Seiten",
            [AppLanguage.Russian] = "Страницы",
            [AppLanguage.Chinese] = "页面"
        },
        ["BtnClear"] = new()
        {
            [AppLanguage.English] = "Clear",
            [AppLanguage.Turkish] = "Temizle",
            [AppLanguage.German] = "Löschen",
            [AppLanguage.Russian] = "Очистить",
            [AppLanguage.Chinese] = "清除"
        },
        ["BtnUndo"] = new()
        {
            [AppLanguage.English] = "Undo",
            [AppLanguage.Turkish] = "Geri Al",
            [AppLanguage.German] = "Rückgängig",
            [AppLanguage.Russian] = "Отменить",
            [AppLanguage.Chinese] = "撤消"
        },
        ["BtnRedo"] = new()
        {
            [AppLanguage.English] = "Redo",
            [AppLanguage.Turkish] = "Yinele",
            [AppLanguage.German] = "Wiederholen",
            [AppLanguage.Russian] = "Повторить",
            [AppLanguage.Chinese] = "重做"
        },
        ["BtnColor"] = new()
        {
            [AppLanguage.English] = "Color",
            [AppLanguage.Turkish] = "Renk",
            [AppLanguage.German] = "Farbe",
            [AppLanguage.Russian] = "Цвет",
            [AppLanguage.Chinese] = "颜色"
        },
        ["TipColorPicker"] = new()
        {
            [AppLanguage.English] = "Choose Pen Color",
            [AppLanguage.Turkish] = "Kalem Rengi Seç",
            [AppLanguage.German] = "Stiftfarbe wählen",
            [AppLanguage.Russian] = "Выбрать цвет пера",
            [AppLanguage.Chinese] = "选择笔颜色"
        },
        ["HeaderColorPicker"] = new()
        {
            [AppLanguage.English] = "Choose Pen Color",
            [AppLanguage.Turkish] = "Kalem Rengi Seçin",
            [AppLanguage.German] = "Stiftfarbe wählen",
            [AppLanguage.Russian] = "Выберите цвет пера",
            [AppLanguage.Chinese] = "选择画笔颜色"
        },
        ["LblThickness"] = new()
        {
            [AppLanguage.English] = "Thickness:",
            [AppLanguage.Turkish] = "Kalınlık:",
            [AppLanguage.German] = "Stärke:",
            [AppLanguage.Russian] = "Толщина:",
            [AppLanguage.Chinese] = "粗细:"
        },
        ["TipDecreaseWidth"] = new()
        {
            [AppLanguage.English] = "Decrease Thickness (-2)",
            [AppLanguage.Turkish] = "Kalınlığı Azalt (-2)",
            [AppLanguage.German] = "Stärke verringern (-2)",
            [AppLanguage.Russian] = "Уменьшить толщину (-2)",
            [AppLanguage.Chinese] = "减小粗细 (-2)"
        },
        ["TipIncreaseWidth"] = new()
        {
            [AppLanguage.English] = "Increase Thickness (+2)",
            [AppLanguage.Turkish] = "Kalınlığı Arttır (+2)",
            [AppLanguage.German] = "Stärke erhöhen (+2)",
            [AppLanguage.Russian] = "Увеличить толщину (+2)",
            [AppLanguage.Chinese] = "增加粗细 (+2)"
        },
        ["TipStrokeWidth"] = new()
        {
            [AppLanguage.English] = "Stroke Thickness",
            [AppLanguage.Turkish] = "Çizgi Kalınlığı",
            [AppLanguage.German] = "Strichstärke",
            [AppLanguage.Russian] = "Толщина линии",
            [AppLanguage.Chinese] = "线条粗细"
        },
        ["TipTouchInking"] = new()
        {
            [AppLanguage.English] = "Draw with finger when enabled. When disabled, finger pans and zooms.",
            [AppLanguage.Turkish] = "Açıkken parmakla da çizilir. Kapalıyken parmak yalnızca sayfayı kaydırır ve büyütür.",
            [AppLanguage.German] = "Wenn aktiviert, mit Finger zeichnen. Wenn deaktiviert, verschiebt und zoomt der Finger.",
            [AppLanguage.Russian] = "Включено: рисование пальцем. Выключено: палец прокручивает и масштабирует.",
            [AppLanguage.Chinese] = "开启时可用手指绘图；关闭时手指仅用于平移和缩放。"
        },
        ["CheckTouchInking"] = new()
        {
            [AppLanguage.English] = "🖐️ Touch Inking",
            [AppLanguage.Turkish] = "🖐️ Parmakla Çiz",
            [AppLanguage.German] = "🖐️ Mit Finger zeichnen",
            [AppLanguage.Russian] = "🖐️ Рисование пальцем",
            [AppLanguage.Chinese] = "🖐️ 手指书写"
        },
        ["TipToggleSidebar"] = new()
        {
            [AppLanguage.English] = "Show/Hide Page Thumbnails",
            [AppLanguage.Turkish] = "Sayfa Küçük Resimlerini Göster/Gizle",
            [AppLanguage.German] = "Seiten-Miniaturansichten ein-/ausblenden",
            [AppLanguage.Russian] = "Показать/скрыть миниатюры страниц",
            [AppLanguage.Chinese] = "显示/隐藏页面缩略图"
        },
        ["TipCloseDoc"] = new()
        {
            [AppLanguage.English] = "Close Document and Return to Welcome Screen",
            [AppLanguage.Turkish] = "Belgeyi Kapat ve Başlangıç Ekranına Dön",
            [AppLanguage.German] = "Dokument schließen und zum Startbildschirm zurückkehren",
            [AppLanguage.Russian] = "Закрыть документ и вернуться на начальный экран",
            [AppLanguage.Chinese] = "关闭文档并返回主屏幕"
        },

        // Tools Menu
        ["ToolPickerHeader"] = new()
        {
            [AppLanguage.English] = "Select Drawing Tool",
            [AppLanguage.Turkish] = "Çizim Aracı Seçin",
            [AppLanguage.German] = "Zeichenwerkzeug wählen",
            [AppLanguage.Russian] = "Выберите инструмент",
            [AppLanguage.Chinese] = "选择绘图工具"
        },
        ["ToolPickerTip"] = new()
        {
            [AppLanguage.English] = "Select Drawing Tool (Pen, Pencil, Highlighter, Eraser, Lasso)",
            [AppLanguage.Turkish] = "Çizim Aracı Seç (Kalem, Kurşun, Vurgu, Silgi, Kement)",
            [AppLanguage.German] = "Zeichenwerkzeug wählen (Stift, Bleistift, Textmarker, Radierer, Lasso)",
            [AppLanguage.Russian] = "Выбрать инструмент (ручка, карандаш, маркер, ластик, лассо)",
            [AppLanguage.Chinese] = "选择绘图工具 (钢笔、铅笔、荧光笔、橡皮擦、套索)"
        },
        ["ToolPen"] = new()
        {
            [AppLanguage.English] = "Pen",
            [AppLanguage.Turkish] = "Kalem",
            [AppLanguage.German] = "Stift",
            [AppLanguage.Russian] = "Ручка",
            [AppLanguage.Chinese] = "钢笔"
        },
        ["ToolPenTitle"] = new()
        {
            [AppLanguage.English] = "Fountain Pen",
            [AppLanguage.Turkish] = "Dolma Kalem",
            [AppLanguage.German] = "Füllfederhalter",
            [AppLanguage.Russian] = "Перьевая ручка",
            [AppLanguage.Chinese] = "钢笔"
        },
        ["ToolPenDesc"] = new()
        {
            [AppLanguage.English] = "Smooth ink & hardware pressure",
            [AppLanguage.Turkish] = "Pürüzsüz mürekkep & basınç",
            [AppLanguage.German] = "Glatter Tintenfluss & Druck",
            [AppLanguage.Russian] = "Гладкие чернила и нажим",
            [AppLanguage.Chinese] = "流畅墨水与硬件压感"
        },
        ["ToolPencil"] = new()
        {
            [AppLanguage.English] = "Pencil",
            [AppLanguage.Turkish] = "Kurşun",
            [AppLanguage.German] = "Bleistift",
            [AppLanguage.Russian] = "Карандаш",
            [AppLanguage.Chinese] = "铅笔"
        },
        ["ToolPencilTitle"] = new()
        {
            [AppLanguage.English] = "Pencil",
            [AppLanguage.Turkish] = "Kurşun Kalem",
            [AppLanguage.German] = "Bleistift",
            [AppLanguage.Russian] = "Карандаш",
            [AppLanguage.Chinese] = "铅笔"
        },
        ["ToolPencilDesc"] = new()
        {
            [AppLanguage.English] = "Tilt & shading support",
            [AppLanguage.Turkish] = "Eğim & gölgeleme desteği",
            [AppLanguage.German] = "Neigungs- & Schattierungsunterstützung",
            [AppLanguage.Russian] = "Поддержка наклона и теней",
            [AppLanguage.Chinese] = "倾斜与阴影支持"
        },
        ["ToolHighlighter"] = new()
        {
            [AppLanguage.English] = "Highlighter",
            [AppLanguage.Turkish] = "Vurgulayıcı",
            [AppLanguage.German] = "Textmarker",
            [AppLanguage.Russian] = "Маркер",
            [AppLanguage.Chinese] = "荧光笔"
        },
        ["ToolHighlighterTitle"] = new()
        {
            [AppLanguage.English] = "Highlighter",
            [AppLanguage.Turkish] = "Fosforlu Kalem",
            [AppLanguage.German] = "Textmarker",
            [AppLanguage.Russian] = "Маркер",
            [AppLanguage.Chinese] = "荧光笔"
        },
        ["ToolHighlighterDesc"] = new()
        {
            [AppLanguage.English] = "Translucent text highlighter",
            [AppLanguage.Turkish] = "Saydam metin vurgulayıcı",
            [AppLanguage.German] = "Durchscheinender Textmarker",
            [AppLanguage.Russian] = "Полупрозрачный маркер",
            [AppLanguage.Chinese] = "半透明文本高亮"
        },
        ["ToolEraser"] = new()
        {
            [AppLanguage.English] = "Eraser",
            [AppLanguage.Turkish] = "Silgi",
            [AppLanguage.German] = "Radierer",
            [AppLanguage.Russian] = "Ластик",
            [AppLanguage.Chinese] = "橡皮擦"
        },
        ["ToolEraserTitle"] = new()
        {
            [AppLanguage.English] = "Eraser",
            [AppLanguage.Turkish] = "Silgi",
            [AppLanguage.German] = "Radierer",
            [AppLanguage.Russian] = "Ластик",
            [AppLanguage.Chinese] = "橡皮擦"
        },
        ["ToolEraserDesc"] = new()
        {
            [AppLanguage.English] = "Erase ink strokes (pen tail also erases)",
            [AppLanguage.Turkish] = "Mürekkep çizgilerini sil (kalem arkası da siler)",
            [AppLanguage.German] = "Tintenstriche löschen (Stiftende radiert auch)",
            [AppLanguage.Russian] = "Стереть штрихи (ластик пера также работает)",
            [AppLanguage.Chinese] = "擦除笔迹 (笔尾也可直接擦除)"
        },
        ["ToolLasso"] = new()
        {
            [AppLanguage.English] = "Lasso",
            [AppLanguage.Turkish] = "Kement",
            [AppLanguage.German] = "Lasso",
            [AppLanguage.Russian] = "Лассо",
            [AppLanguage.Chinese] = "套索"
        },
        ["ToolLassoTitle"] = new()
        {
            [AppLanguage.English] = "Lasso Selection",
            [AppLanguage.Turkish] = "Kement Seçimi",
            [AppLanguage.German] = "Lasso-Auswahl",
            [AppLanguage.Russian] = "Выделение лассо",
            [AppLanguage.Chinese] = "套索选择"
        },
        ["ToolLassoDesc"] = new()
        {
            [AppLanguage.English] = "Select, move or resize drawings",
            [AppLanguage.Turkish] = "Çizimleri seç, taşı veya boyutlandır",
            [AppLanguage.German] = "Zeichnungen auswählen, bewegen oder skalieren",
            [AppLanguage.Russian] = "Выделить, переместить или масштабировать",
            [AppLanguage.Chinese] = "选择、移动或缩放图画"
        },

        // Sidebar
        ["SidebarPagesTitle"] = new()
        {
            [AppLanguage.English] = "Pages & Slides",
            [AppLanguage.Turkish] = "Sayfalar & Slaytlar",
            [AppLanguage.German] = "Seiten & Folien",
            [AppLanguage.Russian] = "Страницы и слайды",
            [AppLanguage.Chinese] = "页面与幻灯片"
        },

        // Lecture Studio Panel
        ["LectureStudioHeader"] = new()
        {
            [AppLanguage.English] = "🎥 Lecture Recording",
            [AppLanguage.Turkish] = "🎥 Ders Kaydı",
            [AppLanguage.German] = "🎥 Vorlesungsaufnahme",
            [AppLanguage.Russian] = "🎥 Запись лекции",
            [AppLanguage.Chinese] = "🎥 课程录制"
        },
        ["StatusReady"] = new()
        {
            [AppLanguage.English] = "● READY",
            [AppLanguage.Turkish] = "● HAZIR",
            [AppLanguage.German] = "● BEREIT",
            [AppLanguage.Russian] = "● ГОТОВ",
            [AppLanguage.Chinese] = "● 就绪"
        },
        ["StatusRecording"] = new()
        {
            [AppLanguage.English] = "● RECORDING",
            [AppLanguage.Turkish] = "● KAYITTA",
            [AppLanguage.German] = "● AUFNAHME",
            [AppLanguage.Russian] = "● ЗАПИСЬ",
            [AppLanguage.Chinese] = "● 录制中"
        },
        ["StatusPaused"] = new()
        {
            [AppLanguage.English] = "● PAUSED",
            [AppLanguage.Turkish] = "● DURAKLATILDI",
            [AppLanguage.German] = "● PAUSIERT",
            [AppLanguage.Russian] = "● ПАУЗА",
            [AppLanguage.Chinese] = "● 已暂停"
        },
        ["StatusSaving"] = new()
        {
            [AppLanguage.English] = "● SAVING...",
            [AppLanguage.Turkish] = "● KAYDEDİLİYOR...",
            [AppLanguage.German] = "● SPEICHERN...",
            [AppLanguage.Russian] = "● СОХРАНЕНИЕ...",
            [AppLanguage.Chinese] = "● 正在保存..."
        },
        ["BtnLivePreview"] = new()
        {
            [AppLanguage.English] = "🎬 Live",
            [AppLanguage.Turkish] = "🎬 Canlı",
            [AppLanguage.German] = "🎬 Live",
            [AppLanguage.Russian] = "🎬 Прямой эфир",
            [AppLanguage.Chinese] = "🎬 实时"
        },
        ["BtnCameraOnly"] = new()
        {
            [AppLanguage.English] = "📷 Camera",
            [AppLanguage.Turkish] = "📷 Kamera",
            [AppLanguage.German] = "📷 Kamera",
            [AppLanguage.Russian] = "📷 Камера",
            [AppLanguage.Chinese] = "📷 摄像头"
        },
        ["BtnMirror"] = new()
        {
            [AppLanguage.English] = "🪞 Mirror",
            [AppLanguage.Turkish] = "🪞 Yansıt",
            [AppLanguage.German] = "🪞 Spiegeln",
            [AppLanguage.Russian] = "🪞 Отразить",
            [AppLanguage.Chinese] = "🪞 镜像"
        },
        ["BtnLayout"] = new()
        {
            [AppLanguage.English] = "📐 Layout",
            [AppLanguage.Turkish] = "📐 Konum",
            [AppLanguage.German] = "📐 Layout",
            [AppLanguage.Russian] = "📐 Позиция",
            [AppLanguage.Chinese] = "📐 布局"
        },
        ["BtnStartRecord"] = new()
        {
            [AppLanguage.English] = "🔴 Start Recording",
            [AppLanguage.Turkish] = "🔴 Kaydı Başlat",
            [AppLanguage.German] = "🔴 Aufnahme starten",
            [AppLanguage.Russian] = "🔴 Начать запись",
            [AppLanguage.Chinese] = "🔴 开始录制"
        },
        ["TipRecordPause"] = new()
        {
            [AppLanguage.English] = "Pause / Resume Recording",
            [AppLanguage.Turkish] = "Kaydı Duraklat / Devam Et",
            [AppLanguage.German] = "Aufnahme pausieren / fortsetzen",
            [AppLanguage.Russian] = "Пауза / продолжить запись",
            [AppLanguage.Chinese] = "暂停/继续录制"
        },
        ["TipRecordStop"] = new()
        {
            [AppLanguage.English] = "Stop and Save as MP4",
            [AppLanguage.Turkish] = "Kaydı Bitir ve MP4 Olarak Kaydet",
            [AppLanguage.German] = "Aufnahme beenden und als MP4 speichern",
            [AppLanguage.Russian] = "Остановить и сохранить в MP4",
            [AppLanguage.Chinese] = "结束并保存为 MP4"
        },
        ["BtnOpenRecordings"] = new()
        {
            [AppLanguage.English] = "📁 Open Recordings",
            [AppLanguage.Turkish] = "📁 Kayıtları Aç",
            [AppLanguage.German] = "📁 Aufnahmen öffnen",
            [AppLanguage.Russian] = "📁 Открыть записи",
            [AppLanguage.Chinese] = "📁 打开录制文件夹"
        },
        ["TipOpenRecordings"] = new()
        {
            [AppLanguage.English] = "Open Recordings Folder (Videos\\InkStudio Recordings)",
            [AppLanguage.Turkish] = "Kayıtlar Klasörünü Aç (Videolar\\InkStudio Kayıtları)",
            [AppLanguage.German] = "Aufnahmeordner öffnen (Videos\\InkStudio-Aufnahmen)",
            [AppLanguage.Russian] = "Открыть папку записей (Видео\\Записи InkStudio)",
            [AppLanguage.Chinese] = "打开录制文件夹 (视频\\InkStudio 录制)"
        },
        ["LblCamera"] = new()
        {
            [AppLanguage.English] = "Camera:",
            [AppLanguage.Turkish] = "Kamera:",
            [AppLanguage.German] = "Kamera:",
            [AppLanguage.Russian] = "Камера:",
            [AppLanguage.Chinese] = "摄像头:"
        },
        ["LblMicrophone"] = new()
        {
            [AppLanguage.English] = "🎙️ Microphone:",
            [AppLanguage.Turkish] = "🎙️ Mikrofon:",
            [AppLanguage.German] = "🎙️ Mikrofon:",
            [AppLanguage.Russian] = "🎙️ Микрофон:",
            [AppLanguage.Chinese] = "🎙️ 麦克风:"
        },
        ["LblAspect"] = new()
        {
            [AppLanguage.English] = "Aspect & Resolution:",
            [AppLanguage.Turkish] = "En-Boy & Çözünürlük:",
            [AppLanguage.German] = "Format & Auflösung:",
            [AppLanguage.Russian] = "Соотношение и разрешение:",
            [AppLanguage.Chinese] = "宽高比与分辨率:"
        },
        ["LblAudioLevel"] = new()
        {
            [AppLanguage.English] = "Vol: ",
            [AppLanguage.Turkish] = "Ses: ",
            [AppLanguage.German] = "Lautst.: ",
            [AppLanguage.Russian] = "Громк.: ",
            [AppLanguage.Chinese] = "音量: "
        },
        ["LblMicVol"] = new()
        {
            [AppLanguage.English] = "Level:",
            [AppLanguage.Turkish] = "Düzey:",
            [AppLanguage.German] = "Pegel:",
            [AppLanguage.Russian] = "Уровень:",
            [AppLanguage.Chinese] = "电平:"
        },
        ["TipMicVolume"] = new()
        {
            [AppLanguage.English] = "Microphone Volume",
            [AppLanguage.Turkish] = "Mikrofon Ses Düzeyi",
            [AppLanguage.German] = "Mikrofonlautstärke",
            [AppLanguage.Russian] = "Громкость микрофона",
            [AppLanguage.Chinese] = "麦克风音量"
        },
        ["AspectFitDocument"] = new()
        {
            [AppLanguage.English] = "📄 Fit to Document (Zero Blank)",
            [AppLanguage.Turkish] = "📄 Belgeye Uygun (Sıfır Boşluk)",
            [AppLanguage.German] = "📄 An Dokument anpassen (Voll)",
            [AppLanguage.Russian] = "📄 По документу (без полей)",
            [AppLanguage.Chinese] = "📄 适应文档 (无黑边)"
        },
        ["Aspect16x9Fhd"] = new()
        {
            [AppLanguage.English] = "📺 16:9 (Full HD 1080p)",
            [AppLanguage.Turkish] = "📺 16:9 (Full HD 1080p)",
            [AppLanguage.German] = "📺 16:9 (Full HD 1080p)",
            [AppLanguage.Russian] = "📺 16:9 (Full HD 1080p)",
            [AppLanguage.Chinese] = "📺 16:9 (全高清 1080p)"
        },
        ["Aspect16x9Hd"] = new()
        {
            [AppLanguage.English] = "📺 16:9 (HD 720p)",
            [AppLanguage.Turkish] = "📺 16:9 (HD 720p)",
            [AppLanguage.German] = "📺 16:9 (HD 720p)",
            [AppLanguage.Russian] = "📺 16:9 (HD 720p)",
            [AppLanguage.Chinese] = "📺 16:9 (高清 720p)"
        },
        ["Aspect4x3"] = new()
        {
            [AppLanguage.English] = "🖥️ 4:3 (Classic Slide)",
            [AppLanguage.Turkish] = "🖥️ 4:3 (Klasik Sunum)",
            [AppLanguage.German] = "🖥️ 4:3 (Klassische Folie)",
            [AppLanguage.Russian] = "🖥️ 4:3 (Классическая презентация)",
            [AppLanguage.Chinese] = "🖥️ 4:3 (经典幻灯片)"
        },
        ["Aspect9x16"] = new()
        {
            [AppLanguage.English] = "📱 9:16 (Vertical Video)",
            [AppLanguage.Turkish] = "📱 9:16 (Dikey Video)",
            [AppLanguage.German] = "📱 9:16 (Vertikales Video)",
            [AppLanguage.Russian] = "📱 9:16 (Вертикальное видео)",
            [AppLanguage.Chinese] = "📱 9:16 (竖屏视频)"
        },
        ["Aspect1x1"] = new()
        {
            [AppLanguage.English] = "⏹️ 1:1 (Square Video)",
            [AppLanguage.Turkish] = "⏹️ 1:1 (Kare Video)",
            [AppLanguage.German] = "⏹️ 1:1 (Quadratisches Video)",
            [AppLanguage.Russian] = "⏹️ 1:1 (Квадратное видео)",
            [AppLanguage.Chinese] = "⏹️ 1:1 (正方形视频)"
        },
        ["CameraLayoutTitle"] = new()
        {
            [AppLanguage.English] = "📐 Camera Recording Position & Size",
            [AppLanguage.Turkish] = "📐 Kamera Kayıt Konumu & Boyutu",
            [AppLanguage.German] = "📐 Kameraposition & -größe",
            [AppLanguage.Russian] = "📐 Позиция и размер камеры",
            [AppLanguage.Chinese] = "📐 摄像头录制位置与尺寸"
        },
        ["LblPosition"] = new()
        {
            [AppLanguage.English] = "Position:",
            [AppLanguage.Turkish] = "Konum:",
            [AppLanguage.German] = "Position:",
            [AppLanguage.Russian] = "Позиция:",
            [AppLanguage.Chinese] = "位置:"
        },
        ["LblCameraAspect"] = new()
        {
            [AppLanguage.English] = "Camera Aspect:",
            [AppLanguage.Turkish] = "Kamera En-Boy:",
            [AppLanguage.German] = "Kameraformat:",
            [AppLanguage.Russian] = "Формат камеры:",
            [AppLanguage.Chinese] = "摄像头比例:"
        },
        ["CameraSize"] = new()
        {
            [AppLanguage.English] = "Camera Size:",
            [AppLanguage.Turkish] = "Kamera Boyutu:",
            [AppLanguage.German] = "Kameragröße:",
            [AppLanguage.Russian] = "Размер камеры:",
            [AppLanguage.Chinese] = "摄像头大小:"
        },
        ["PosBottomRight"] = new()
        {
            [AppLanguage.English] = "Bottom-Right",
            [AppLanguage.Turkish] = "Sağ Alt",
            [AppLanguage.German] = "Unten rechts",
            [AppLanguage.Russian] = "Снизу справа",
            [AppLanguage.Chinese] = "右下角"
        },
        ["PosBottomLeft"] = new()
        {
            [AppLanguage.English] = "Bottom-Left",
            [AppLanguage.Turkish] = "Sol Alt",
            [AppLanguage.German] = "Unten links",
            [AppLanguage.Russian] = "Снизу слева",
            [AppLanguage.Chinese] = "左下角"
        },
        ["PosTopRight"] = new()
        {
            [AppLanguage.English] = "Top-Right",
            [AppLanguage.Turkish] = "Sağ Üst",
            [AppLanguage.German] = "Oben rechts",
            [AppLanguage.Russian] = "Сверху справа",
            [AppLanguage.Chinese] = "右上角"
        },
        ["PosTopLeft"] = new()
        {
            [AppLanguage.English] = "Top-Left",
            [AppLanguage.Turkish] = "Sol Üst",
            [AppLanguage.German] = "Oben links",
            [AppLanguage.Russian] = "Сверху слева",
            [AppLanguage.Chinese] = "左上角"
        },
        ["CamAspect16x9"] = new()
        {
            [AppLanguage.English] = "16:9 Wide",
            [AppLanguage.Turkish] = "16:9 Geniş",
            [AppLanguage.German] = "16:9 Breit",
            [AppLanguage.Russian] = "16:9 Широкий",
            [AppLanguage.Chinese] = "16:9 宽屏"
        },
        ["CamAspect4x3"] = new()
        {
            [AppLanguage.English] = "4:3 Classic",
            [AppLanguage.Turkish] = "4:3 Klasik",
            [AppLanguage.German] = "4:3 Klassisch",
            [AppLanguage.Russian] = "4:3 Классический",
            [AppLanguage.Chinese] = "4:3 传统"
        },
        ["CamAspect1x1"] = new()
        {
            [AppLanguage.English] = "1:1 Square",
            [AppLanguage.Turkish] = "1:1 Kare",
            [AppLanguage.German] = "1:1 Quadrat",
            [AppLanguage.Russian] = "1:1 Квадрат",
            [AppLanguage.Chinese] = "1:1 正方形"
        },
        ["CamAspect9x16"] = new()
        {
            [AppLanguage.English] = "9:16 Vertical",
            [AppLanguage.Turkish] = "9:16 Dikey",
            [AppLanguage.German] = "9:16 Vertikal",
            [AppLanguage.Russian] = "9:16 Вертикальный",
            [AppLanguage.Chinese] = "9:16 竖屏"
        },
        ["CamAspectCustom"] = new()
        {
            [AppLanguage.English] = "🎛️ Custom Ratio",
            [AppLanguage.Turkish] = "🎛️ Özel En-Boy",
            [AppLanguage.German] = "🎛️ Benutzerdefiniert",
            [AppLanguage.Russian] = "🎛️ Пользовательский",
            [AppLanguage.Chinese] = "🎛️ 自定义比例"
        },
        ["CameraPlaceholder"] = new()
        {
            [AppLanguage.English] = "Camera Preview",
            [AppLanguage.Turkish] = "Kamera Önizlemesi",
            [AppLanguage.German] = "Kameravorschau",
            [AppLanguage.Russian] = "Предпросмотр камеры",
            [AppLanguage.Chinese] = "摄像头预览"
        },
        ["CameraNone"] = new()
        {
            [AppLanguage.English] = "No Camera Found",
            [AppLanguage.Turkish] = "Kamera Bulunamadı",
            [AppLanguage.German] = "Keine Kamera gefunden",
            [AppLanguage.Russian] = "Камера не найдена",
            [AppLanguage.Chinese] = "未找到摄像头"
        },
        ["MicDefault"] = new()
        {
            [AppLanguage.English] = "Default Microphone",
            [AppLanguage.Turkish] = "Varsayılan Mikrofon",
            [AppLanguage.German] = "Standardmikrofon",
            [AppLanguage.Russian] = "Микрофон по умолчанию",
            [AppLanguage.Chinese] = "默认麦克风"
        },

        // Lecture Choice Modal
        ["ModalLectureTitle"] = new()
        {
            [AppLanguage.English] = "Lecture Recording Mode",
            [AppLanguage.Turkish] = "Ders Kaydı Modu",
            [AppLanguage.German] = "Vorlesungsaufnahmemodus",
            [AppLanguage.Russian] = "Режим записи лекции",
            [AppLanguage.Chinese] = "课程录制模式"
        },
        ["ModalLectureSubtitle"] = new()
        {
            [AppLanguage.English] = "Choose canvas for your lecture:",
            [AppLanguage.Turkish] = "Ders anlatımı için kullanmak istediğiniz tuvali seçin:",
            [AppLanguage.German] = "Wählen Sie die Arbeitsfläche für Ihre Vorlesung:",
            [AppLanguage.Russian] = "Выберите холст для проведения лекции:",
            [AppLanguage.Chinese] = "选择授课画板:"
        },
        ["ModalChoice1Title"] = new()
        {
            [AppLanguage.English] = "16:9 Blank Canvas (Recommended)",
            [AppLanguage.Turkish] = "16:9 Boş Beyaz Sayfa (Önerilen)",
            [AppLanguage.German] = "16:9 Leere weiße Tafel (Empfohlen)",
            [AppLanguage.Russian] = "16:9 Чистая белая доска (Рекомендуется)",
            [AppLanguage.Chinese] = "16:9 空白画板 (推荐)"
        },
        ["ModalChoice1Desc"] = new()
        {
            [AppLanguage.English] = "Widescreen whiteboard in Full HD (1920x1080) video ratio",
            [AppLanguage.Turkish] = "Full HD (1920x1080) video oranında geniş beyaz tahta",
            [AppLanguage.German] = "Breite Tafel im Full HD (1920x1080) Videoformat",
            [AppLanguage.Russian] = "Широкая доска в формате Full HD (1920x1080)",
            [AppLanguage.Chinese] = "全高清 (1920x1080) 宽屏视频白板"
        },
        ["ModalChoice2Title"] = new()
        {
            [AppLanguage.English] = "Lecture on Document / Slides",
            [AppLanguage.Turkish] = "PDF Belgesi Üzerinde Ders",
            [AppLanguage.German] = "Vorlesung auf Dokument / Folien",
            [AppLanguage.Russian] = "Лекция по документу / слайдам",
            [AppLanguage.Chinese] = "在课件或 PDF 上授课"
        },
        ["ModalChoice2Desc"] = new()
        {
            [AppLanguage.English] = "Open lecture slides or PDF document and annotate",
            [AppLanguage.Turkish] = "Ders notu veya slayt açıp üzerine yazarak anlatın",
            [AppLanguage.German] = "Öffnen Sie Folien oder ein PDF und zeichnen Sie darauf",
            [AppLanguage.Russian] = "Откройте слайды или PDF и пишите поверх них",
            [AppLanguage.Chinese] = "打开课件或 PDF 并进行书写讲解"
        },
        ["ModalBtnCancel"] = new()
        {
            [AppLanguage.English] = "Cancel",
            [AppLanguage.Turkish] = "Vazgeç",
            [AppLanguage.German] = "Abbrechen",
            [AppLanguage.Russian] = "Отмена",
            [AppLanguage.Chinese] = "取消"
        },

        // Footer & Navigation
        ["FooterPrev"] = new()
        {
            [AppLanguage.English] = "◀ Prev",
            [AppLanguage.Turkish] = "◀ Önceki",
            [AppLanguage.German] = "◀ Zurück",
            [AppLanguage.Russian] = "◀ Назад",
            [AppLanguage.Chinese] = "◀ 上一页"
        },
        ["FooterNext"] = new()
        {
            [AppLanguage.English] = "Next ▶",
            [AppLanguage.Turkish] = "Sonraki ▶",
            [AppLanguage.German] = "Weiter ▶",
            [AppLanguage.Russian] = "Вперед ▶",
            [AppLanguage.Chinese] = "下一页 ▶"
        },
        ["FooterLblPage"] = new()
        {
            [AppLanguage.English] = "Page",
            [AppLanguage.Turkish] = "Sayfa",
            [AppLanguage.German] = "Seite",
            [AppLanguage.Russian] = "Стр.",
            [AppLanguage.Chinese] = "页码"
        },
        ["FooterTipJumpPage"] = new()
        {
            [AppLanguage.English] = "Type page number and press Enter",
            [AppLanguage.Turkish] = "Gitmek istediğiniz sayfa numarasını yazıp Enter'a basın",
            [AppLanguage.German] = "Seitenzahl eingeben und Enter drücken",
            [AppLanguage.Russian] = "Введите номер страницы и нажмите Enter",
            [AppLanguage.Chinese] = "输入页码并按回车"
        },
        ["FooterBtnGo"] = new()
        {
            [AppLanguage.English] = "Go ➔",
            [AppLanguage.Turkish] = "Git ➔",
            [AppLanguage.German] = "Gehe zu ➔",
            [AppLanguage.Russian] = "Перейти ➔",
            [AppLanguage.Chinese] = "跳转 ➔"
        },
        ["FooterTipGo"] = new()
        {
            [AppLanguage.English] = "Go to entered page",
            [AppLanguage.Turkish] = "Yazılan Sayfaya Git",
            [AppLanguage.German] = "Zur eingegebenen Seite springen",
            [AppLanguage.Russian] = "Перейти к указанной странице",
            [AppLanguage.Chinese] = "跳转至输入页面"
        },
        ["TelemetryPenNone"] = new()
        {
            [AppLanguage.English] = "Pen: Not detected",
            [AppLanguage.Turkish] = "Kalem: Algılanmadı",
            [AppLanguage.German] = "Stift: Nicht erkannt",
            [AppLanguage.Russian] = "Перо: Не обнаружено",
            [AppLanguage.Chinese] = "手写笔: 未检测到"
        },
        ["TelemetryPenReady"] = new()
        {
            [AppLanguage.English] = "Pen: Ready",
            [AppLanguage.Turkish] = "Kalem: Hazır",
            [AppLanguage.German] = "Stift: Bereit",
            [AppLanguage.Russian] = "Перо: Готово",
            [AppLanguage.Chinese] = "手写笔: 就绪"
        },
        ["TelemetryPressure"] = new()
        {
            [AppLanguage.English] = "Pressure: {0}%",
            [AppLanguage.Turkish] = "Basınç: %{0}",
            [AppLanguage.German] = "Druck: {0}%",
            [AppLanguage.Russian] = "Нажим: {0}%",
            [AppLanguage.Chinese] = "压感: {0}%"
        },
        ["TelemetryTilt"] = new()
        {
            [AppLanguage.English] = "Tilt: {0}°",
            [AppLanguage.Turkish] = "Eğim: {0}°",
            [AppLanguage.German] = "Neigung: {0}°",
            [AppLanguage.Russian] = "Наклон: {0}°",
            [AppLanguage.Chinese] = "倾斜: {0}°"
        },
        ["TelemetryModeDefault"] = new()
        {
            [AppLanguage.English] = "Mode: Pen draws, finger pans/zooms",
            [AppLanguage.Turkish] = "Mod: Kalemle Dokunulduğunda Çizer, Parmakla Kaydırır",
            [AppLanguage.German] = "Modus: Stift zeichnet, Finger scrollt/zoomt",
            [AppLanguage.Russian] = "Режим: Перо рисует, палец прокручивает",
            [AppLanguage.Chinese] = "模式: 笔书写，手指平移缩放"
        },
        ["TelemetryModeEraser"] = new()
        {
            [AppLanguage.English] = "Mode: [ERASER ACTIVE]",
            [AppLanguage.Turkish] = "Mod: [ARKA SİLGİ AKTİF]",
            [AppLanguage.German] = "Modus: [RADIERER AKTIV]",
            [AppLanguage.Russian] = "Режим: [ЛАСТИК АКТИВЕН]",
            [AppLanguage.Chinese] = "模式: [橡皮擦激活]"
        },
        ["TelemetryModeLasso"] = new()
        {
            [AppLanguage.English] = "Mode: [BARREL BUTTON - LASSO]",
            [AppLanguage.Turkish] = "Mod: [GÖVDE DÜĞMESİ - KEMENT]",
            [AppLanguage.German] = "Modus: [STIFTASTE - LASSO]",
            [AppLanguage.Russian] = "Режим: [КНОПКА ПЕРА - ЛАССО]",
            [AppLanguage.Chinese] = "模式: [笔侧键 - 套索]"
        },
        ["TelemetryModeTouch"] = new()
        {
            [AppLanguage.English] = "Mode: [TOUCH INKING ACTIVE]",
            [AppLanguage.Turkish] = "Mod: [DOKUNARAK ÇİZİM AKTİF]",
            [AppLanguage.German] = "Modus: [FINGERZEICHNEN AKTIV]",
            [AppLanguage.Russian] = "Режим: [РИСОВАНИЕ ПАЛЬЦЕМ]",
            [AppLanguage.Chinese] = "模式: [手指绘图已激活]"
        },

        // Loading Overlay
        ["LoadingDocTitle"] = new()
        {
            [AppLanguage.English] = "Loading Document...",
            [AppLanguage.Turkish] = "Belge Açılıyor...",
            [AppLanguage.German] = "Dokument wird geladen...",
            [AppLanguage.Russian] = "Открытие документа...",
            [AppLanguage.Chinese] = "正在打开文档..."
        },
        ["LoadingDocSubtitle"] = new()
        {
            [AppLanguage.English] = "Scanning pages in high quality...",
            [AppLanguage.Turkish] = "Sayfalar yüksek kalitede taranıyor...",
            [AppLanguage.German] = "Seiten werden in hoher Qualität gerendert...",
            [AppLanguage.Russian] = "Страницы обрабатываются в высоком качестве...",
            [AppLanguage.Chinese] = "正在高精度渲染页面..."
        },
        ["LoadingPptSubtitle"] = new()
        {
            [AppLanguage.English] = "Converting PowerPoint slides in high quality...",
            [AppLanguage.Turkish] = "PowerPoint slaytları yüksek kalitede dönüştürülüyor...",
            [AppLanguage.German] = "PowerPoint-Folien werden in hoher Qualität konvertiert...",
            [AppLanguage.Russian] = "Слайды PowerPoint конвертируются в высоком качестве...",
            [AppLanguage.Chinese] = "正在高品质转换 PowerPoint 幻灯片..."
        },
        ["LoadingSavingPdfTitle"] = new()
        {
            [AppLanguage.English] = "Saving PDF...",
            [AppLanguage.Turkish] = "PDF Kaydediliyor...",
            [AppLanguage.German] = "PDF wird gespeichert...",
            [AppLanguage.Russian] = "Сохранение PDF...",
            [AppLanguage.Chinese] = "正在保存 PDF..."
        },
        ["LoadingSavingPdfSubtitle"] = new()
        {
            [AppLanguage.English] = "Merging vector ink strokes and pages...",
            [AppLanguage.Turkish] = "Vektörel çizimler ve sayfalar birleştiriliyor...",
            [AppLanguage.German] = "Vektortinte und Seiten werden zusammengeführt...",
            [AppLanguage.Russian] = "Объединение векторных штрихов со страницами...",
            [AppLanguage.Chinese] = "正在合并矢量笔迹与页面..."
        },
        ["LoadingPageProcessing"] = new()
        {
            [AppLanguage.English] = "Processing page {0} / {1}...",
            [AppLanguage.Turkish] = "Sayfa {0} / {1} işleniyor...",
            [AppLanguage.German] = "Seite {0} / {1} wird verarbeitet...",
            [AppLanguage.Russian] = "Обработка страницы {0} / {1}...",
            [AppLanguage.Chinese] = "正在处理第 {0} / {1} 页..."
        },

        // Dialogs & Prompts
        ["DialogCloseTitle"] = new()
        {
            [AppLanguage.English] = "Close Document",
            [AppLanguage.Turkish] = "Belgeyi Kapat",
            [AppLanguage.German] = "Dokument schließen",
            [AppLanguage.Russian] = "Закрыть документ",
            [AppLanguage.Chinese] = "关闭文档"
        },
        ["DialogCloseConfirm"] = new()
        {
            [AppLanguage.English] = "Do you want to close current work and return to the welcome screen?",
            [AppLanguage.Turkish] = "Mevcut çalışmayı kapatıp başlangıç ekranına dönmek istiyor musunuz?",
            [AppLanguage.German] = "Möchten Sie das aktuelle Dokument schließen und zum Startbildschirm zurückkehren?",
            [AppLanguage.Russian] = "Закрыть текущую работу и вернуться на начальный экран?",
            [AppLanguage.Chinese] = "您确定要关闭当前工作并返回欢迎屏幕吗？"
        },
        ["DialogNewTitle"] = new()
        {
            [AppLanguage.English] = "New Note",
            [AppLanguage.Turkish] = "Yeni Not",
            [AppLanguage.German] = "Neue Notiz",
            [AppLanguage.Russian] = "Новая заметка",
            [AppLanguage.Chinese] = "新建画板"
        },
        ["DialogNewConfirm"] = new()
        {
            [AppLanguage.English] = "Do you want to reset current work and create a new blank note?",
            [AppLanguage.Turkish] = "Mevcut çalışmayı sıfırlayıp yeni bir not sayfası açmak istiyor musunuz?",
            [AppLanguage.German] = "Möchten Sie die aktuelle Arbeit verwerfen und eine neue Notiz erstellen?",
            [AppLanguage.Russian] = "Сбросить текущую работу и создать новую заметку?",
            [AppLanguage.Chinese] = "您确定要重置当前工作并创建新的空白画板吗？"
        },
        ["DialogClearTitle"] = new()
        {
            [AppLanguage.English] = "Clear Page",
            [AppLanguage.Turkish] = "Sayfayı Temizle",
            [AppLanguage.German] = "Seite leeren",
            [AppLanguage.Russian] = "Очистить страницу",
            [AppLanguage.Chinese] = "清空页面"
        },
        ["DialogClearConfirm"] = new()
        {
            [AppLanguage.English] = "Are you sure you want to clear all ink drawings on this page?",
            [AppLanguage.Turkish] = "Bu sayfadaki tüm kalem çizimlerini silmek istediğinize emin misiniz?",
            [AppLanguage.German] = "Möchten Sie wirklich alle Zeichnungen auf dieser Seite löschen?",
            [AppLanguage.Russian] = "Вы действительно хотите удалить все рисунки на этой странице?",
            [AppLanguage.Chinese] = "您确定要清除该页面上的所有笔迹吗？"
        },
        ["DialogSaveSuccessTitle"] = new()
        {
            [AppLanguage.English] = "Save Successful",
            [AppLanguage.Turkish] = "Kayıt Başarılı",
            [AppLanguage.German] = "Erfolgreich gespeichert",
            [AppLanguage.Russian] = "Сохранено успешно",
            [AppLanguage.Chinese] = "保存成功"
        },
        ["DialogSaveSuccessMsg"] = new()
        {
            [AppLanguage.English] = "Your document with all drawings has been saved as a high quality vector PDF!\n\nFile: {0}",
            [AppLanguage.Turkish] = "Belgeniz tüm kalem çizimleri ve notlarınızla birlikte yüksek kaliteli vektörel PDF olarak kaydedildi!\n\nDosya: {0}",
            [AppLanguage.German] = "Ihr Dokument wurde mit allen Zeichnungen als hochauflösendes Vektor-PDF gespeichert!\n\nDatei: {0}",
            [AppLanguage.Russian] = "Ваш документ со всеми рисунками сохранен как качественный векторный PDF!\n\nФайл: {0}",
            [AppLanguage.Chinese] = "您的文档及所有笔迹已成功保存为高精度矢量 PDF！\n\n文件: {0}"
        },
        ["DialogRecordSuccessTitle"] = new()
        {
            [AppLanguage.English] = "Lecture Recording Completed",
            [AppLanguage.Turkish] = "Ders Kaydı Tamamlandı",
            [AppLanguage.German] = "Vorlesungsaufnahme abgeschlossen",
            [AppLanguage.Russian] = "Запись лекции завершена",
            [AppLanguage.Chinese] = "课程录制完成"
        },
        ["DialogRecordSuccessMsg"] = new()
        {
            [AppLanguage.English] = "Your lecture recording was completed and saved as MP4!\n\nFile: {0}\n\nWould you like to open the video now?",
            [AppLanguage.Turkish] = "Ders kaydınız başarıyla tamamlandı ve MP4 olarak kaydedildi!\n\nDosya: {0}\n\nKaydedilen ders videosunu şimdi açmak ister misiniz?",
            [AppLanguage.German] = "Ihre Vorlesungsaufnahme wurde als MP4 gespeichert!\n\nDatei: {0}\n\nMöchten Sie das Video jetzt öffnen?",
            [AppLanguage.Russian] = "Запись лекции успешно завершена и сохранена в формате MP4!\n\nФайл: {0}\n\nОткрыть записанное видео сейчас?",
            [AppLanguage.Chinese] = "课程录制已完成并保存为 MP4！\n\n文件: {0}\n\n是否立即打开该视频？"
        },
        ["DialogErrorTitle"] = new()
        {
            [AppLanguage.English] = "Error",
            [AppLanguage.Turkish] = "Hata",
            [AppLanguage.German] = "Fehler",
            [AppLanguage.Russian] = "Ошибка",
            [AppLanguage.Chinese] = "错误"
        },
        ["DialogWarningTitle"] = new()
        {
            [AppLanguage.English] = "Warning",
            [AppLanguage.Turkish] = "Uyarı",
            [AppLanguage.German] = "Warnung",
            [AppLanguage.Russian] = "Предупреждение",
            [AppLanguage.Chinese] = "警告"
        },
        ["DialogDocReadError"] = new()
        {
            [AppLanguage.English] = "Could not read document pages.",
            [AppLanguage.Turkish] = "Belge sayfaları okunamadı.",
            [AppLanguage.German] = "Dokumentseiten konnten nicht gelesen werden.",
            [AppLanguage.Russian] = "Не удалось прочитать страницы документа.",
            [AppLanguage.Chinese] = "无法读取文档页面。"
        },
        ["DialogFilterAllSupported"] = new()
        {
            [AppLanguage.English] = "Supported Documents (*.pdf;*.pptx;*.ppt)|*.pdf;*.pptx;*.ppt|PDF Documents (*.pdf)|*.pdf|PowerPoint Presentations (*.pptx;*.ppt)|*.pptx;*.ppt|All Files (*.*)|*.*",
            [AppLanguage.Turkish] = "Desteklenen Belgeler (*.pdf;*.pptx;*.ppt)|*.pdf;*.pptx;*.ppt|PDF Dosyaları (*.pdf)|*.pdf|PowerPoint Sunumları (*.pptx;*.ppt)|*.pptx;*.ppt|Tüm Dosyalar (*.*)|*.*",
            [AppLanguage.German] = "Unterstützte Dokumente (*.pdf;*.pptx;*.ppt)|*.pdf;*.pptx;*.ppt|PDF-Dokumente (*.pdf)|*.pdf|PowerPoint-Präsentationen (*.pptx;*.ppt)|*.pptx;*.ppt|Alle Dateien (*.*)|*.*",
            [AppLanguage.Russian] = "Поддерживаемые документы (*.pdf;*.pptx;*.ppt)|*.pdf;*.pptx;*.ppt|Файлы PDF (*.pdf)|*.pdf|Презентации PowerPoint (*.pptx;*.ppt)|*.pptx;*.ppt|Все файлы (*.*)|*.*",
            [AppLanguage.Chinese] = "支持的文档 (*.pdf;*.pptx;*.ppt)|*.pdf;*.pptx;*.ppt|PDF 文档 (*.pdf)|*.pdf|PowerPoint 演示文稿 (*.pptx;*.ppt)|*.pptx;*.ppt|所有文件 (*.*)|*.*"
        },
        ["DialogFilterPdfOnly"] = new()
        {
            [AppLanguage.English] = "PDF Document (*.pdf)|*.pdf",
            [AppLanguage.Turkish] = "PDF Belgesi (*.pdf)|*.pdf",
            [AppLanguage.German] = "PDF-Dokument (*.pdf)|*.pdf",
            [AppLanguage.Russian] = "Документ PDF (*.pdf)|*.pdf",
            [AppLanguage.Chinese] = "PDF 文档 (*.pdf)|*.pdf"
        },
        ["DefaultNoteTitle"] = new()
        {
            [AppLanguage.English] = "InkStudio - New Notebook [MPP 2.0]",
            [AppLanguage.Turkish] = "InkStudio - Yeni Not Defteri [MPP 2.0]",
            [AppLanguage.German] = "InkStudio - Neues Notizbuch [MPP 2.0]",
            [AppLanguage.Russian] = "InkStudio - Новый блокнот [MPP 2.0]",
            [AppLanguage.Chinese] = "InkStudio - 新笔记本 [MPP 2.0]"
        },
        ["LectureNoteTitle"] = new()
        {
            [AppLanguage.English] = "InkStudio - Lecture Recording [16:9]",
            [AppLanguage.Turkish] = "InkStudio - Ders Kaydı [16:9]",
            [AppLanguage.German] = "InkStudio - Vorlesungsaufnahme [16:9]",
            [AppLanguage.Russian] = "InkStudio - Запись лекции [16:9]",
            [AppLanguage.Chinese] = "InkStudio - 课程录制 [16:9]"
        },
        ["WindowTitle"] = new()
        {
            [AppLanguage.English] = "InkStudio - MPP 2.0 Pen & Document Studio",
            [AppLanguage.Turkish] = "InkStudio - MPP 2.0 Kalem ve Belge Stüdyosu",
            [AppLanguage.German] = "InkStudio - MPP 2.0 Stift- und Dokumentstudio",
            [AppLanguage.Russian] = "InkStudio - Студия пера и документов MPP 2.0",
            [AppLanguage.Chinese] = "InkStudio - MPP 2.0 手写笔与文档工作台"
        },
        ["TipDecreaseWidth"] = new()
        {
            [AppLanguage.English] = "Decrease Thickness (-2)",
            [AppLanguage.Turkish] = "Kalınlığı Azalt (-2)",
            [AppLanguage.German] = "Dicke verringern (-2)",
            [AppLanguage.Russian] = "Уменьшить толщину (-2)",
            [AppLanguage.Chinese] = "减小粗细 (-2)"
        },
        ["TipIncreaseWidth"] = new()
        {
            [AppLanguage.English] = "Increase Thickness (+2)",
            [AppLanguage.Turkish] = "Kalınlığı Artır (+2)",
            [AppLanguage.German] = "Dicke erhöhen (+2)",
            [AppLanguage.Russian] = "Увеличить толщину (+2)",
            [AppLanguage.Chinese] = "增加粗细 (+2)"
        },
        ["TipSliderWidth"] = new()
        {
            [AppLanguage.English] = "Stroke Thickness",
            [AppLanguage.Turkish] = "Çizgi Kalınlığı",
            [AppLanguage.German] = "Strichstärke",
            [AppLanguage.Russian] = "Толщина линии",
            [AppLanguage.Chinese] = "笔画粗细"
        },
        ["TipUndo"] = new()
        {
            [AppLanguage.English] = "Undo (Ctrl+Z)",
            [AppLanguage.Turkish] = "Geri Al (Ctrl+Z)",
            [AppLanguage.German] = "Rückgängig (Ctrl+Z)",
            [AppLanguage.Russian] = "Отменить (Ctrl+Z)",
            [AppLanguage.Chinese] = "撤消 (Ctrl+Z)"
        },
        ["TipRedo"] = new()
        {
            [AppLanguage.English] = "Redo (Ctrl+Y)",
            [AppLanguage.Turkish] = "Yinele (Ctrl+Y)",
            [AppLanguage.German] = "Wiederholen (Ctrl+Y)",
            [AppLanguage.Russian] = "Повторить (Ctrl+Y)",
            [AppLanguage.Chinese] = "重做 (Ctrl+Y)"
        },
        ["TipClear"] = new()
        {
            [AppLanguage.English] = "Clear Page",
            [AppLanguage.Turkish] = "Sayfayı Temizle",
            [AppLanguage.German] = "Seite leeren",
            [AppLanguage.Russian] = "Очистить страницу",
            [AppLanguage.Chinese] = "清除页面"
        },
        ["TouchInkingText"] = new()
        {
            [AppLanguage.English] = "🖐️ Touch Inking",
            [AppLanguage.Turkish] = "🖐️ Parmakla Çiz",
            [AppLanguage.German] = "🖐️ Fingerzeichnen",
            [AppLanguage.Russian] = "🖐️ Рисование пальцем",
            [AppLanguage.Chinese] = "🖐️ 触控绘图"
        },
        ["TouchInkingTip"] = new()
        {
            [AppLanguage.English] = "When enabled, finger draws strokes. When disabled, finger pans and zooms.",
            [AppLanguage.Turkish] = "Açıkken parmakla da çizilir. Kapalıyken parmak yalnızca sayfayı kaydırır ve büyütür.",
            [AppLanguage.German] = "Aktiviert: Finger zeichnet. Deaktiviert: Finger scrollt und zoomt.",
            [AppLanguage.Russian] = "Включено: палец рисует. Выключено: палец прокручивает и масштабирует.",
            [AppLanguage.Chinese] = "开启时可用手指绘图；关闭时手指仅用于平移和缩放。"
        },
        ["TipToggleSidebar"] = new()
        {
            [AppLanguage.English] = "Show / Hide Page Thumbnails",
            [AppLanguage.Turkish] = "Sayfa Küçük Resimlerini Göster/Gizle",
            [AppLanguage.German] = "Seitenvorschau ein-/ausblenden",
            [AppLanguage.Russian] = "Показать/скрыть миниатюры страниц",
            [AppLanguage.Chinese] = "显示/隐藏页面缩略图"
        },
        ["BtnToggleSidebarText"] = new()
        {
            [AppLanguage.English] = "📑 Pages",
            [AppLanguage.Turkish] = "📑 Sayfalar",
            [AppLanguage.German] = "📑 Seiten",
            [AppLanguage.Russian] = "📑 Страницы",
            [AppLanguage.Chinese] = "📑 页面"
        },
        ["TipAspectResolution"] = new()
        {
            [AppLanguage.English] = "Recording aspect ratio and video resolution",
            [AppLanguage.Turkish] = "Kayıt en-boy oranı ve video çözünürlüğü",
            [AppLanguage.German] = "Aufnahme-Seitenverhältnis und Auflösung",
            [AppLanguage.Russian] = "Соотношение сторон и разрешение видео",
            [AppLanguage.Chinese] = "录制宽高比与视频分辨率"
        },
        ["TipZoomOut"] = new()
        {
            [AppLanguage.English] = "Zoom Out (-)",
            [AppLanguage.Turkish] = "Uzaklaştır (-)",
            [AppLanguage.German] = "Verkleinern (-)",
            [AppLanguage.Russian] = "Уменьшить (-)",
            [AppLanguage.Chinese] = "缩小 (-)"
        },
        ["TipZoomIn"] = new()
        {
            [AppLanguage.English] = "Zoom In (+)",
            [AppLanguage.Turkish] = "Yakınlaştır (+)",
            [AppLanguage.German] = "Vergrößern (+)",
            [AppLanguage.Russian] = "Увеличить (+)",
            [AppLanguage.Chinese] = "放大 (+)"
        },
        ["TipFitPage"] = new()
        {
            [AppLanguage.English] = "Fit Page to View",
            [AppLanguage.Turkish] = "Sayfayı Ekrana Sığdır",
            [AppLanguage.German] = "Seite an Bildschirm anpassen",
            [AppLanguage.Russian] = "По размеру экрана",
            [AppLanguage.Chinese] = "适应屏幕"
        },
        ["CameraBadge"] = new()
        {
            [AppLanguage.English] = "📷 Camera",
            [AppLanguage.Turkish] = "📷 Kamera",
            [AppLanguage.German] = "📷 Kamera",
            [AppLanguage.Russian] = "📷 Камера",
            [AppLanguage.Chinese] = "📷 摄像头"
        },
        ["MiniAspectDoc"] = new()
        {
            [AppLanguage.English] = "Document",
            [AppLanguage.Turkish] = "Belge",
            [AppLanguage.German] = "Dokument",
            [AppLanguage.Russian] = "Документ",
            [AppLanguage.Chinese] = "文档"
        },
        ["MiniAspectDocLabel"] = new()
        {
            [AppLanguage.English] = "📄 Document Area",
            [AppLanguage.Turkish] = "📄 Belge Alanı",
            [AppLanguage.German] = "📄 Dokumentbereich",
            [AppLanguage.Russian] = "📄 Область документа",
            [AppLanguage.Chinese] = "📄 文档区域"
        },
        ["MiniAspect16x9"] = new()
        {
            [AppLanguage.English] = "16:9 Wide",
            [AppLanguage.Turkish] = "16:9 Geniş",
            [AppLanguage.German] = "16:9 Breit",
            [AppLanguage.Russian] = "16:9 Широкий",
            [AppLanguage.Chinese] = "16:9 宽屏"
        },
        ["MiniAspect4x3"] = new()
        {
            [AppLanguage.English] = "4:3 Presentation",
            [AppLanguage.Turkish] = "4:3 Sunum",
            [AppLanguage.German] = "4:3 Präsentation",
            [AppLanguage.Russian] = "4:3 Презентация",
            [AppLanguage.Chinese] = "4:3 演示"
        },
        ["MiniAspect9x16"] = new()
        {
            [AppLanguage.English] = "9:16 Vertical",
            [AppLanguage.Turkish] = "9:16 Dikey",
            [AppLanguage.German] = "9:16 Vertikal",
            [AppLanguage.Russian] = "9:16 Вертикальный",
            [AppLanguage.Chinese] = "9:16 竖屏"
        },
        ["MiniAspect1x1"] = new()
        {
            [AppLanguage.English] = "1:1 Square",
            [AppLanguage.Turkish] = "1:1 Kare",
            [AppLanguage.German] = "1:1 Quadrat",
            [AppLanguage.Russian] = "1:1 Квадрат",
            [AppLanguage.Chinese] = "1:1 正方形"
        },
        ["DialogRecordErrorTitle"] = new()
        {
            [AppLanguage.English] = "Recording Error",
            [AppLanguage.Turkish] = "Kayıt Hatası",
            [AppLanguage.German] = "Aufnahmefehler",
            [AppLanguage.Russian] = "Ошибка записи",
            [AppLanguage.Chinese] = "录制错误"
        },
        ["DialogRecordErrorMsg"] = new()
        {
            [AppLanguage.English] = "An error occurred during lecture recording:\n{0}",
            [AppLanguage.Turkish] = "Ders kaydı sırasında bir hata oluştu:\n{0}",
            [AppLanguage.German] = "Bei der Vorlesungsaufnahme ist ein Fehler aufgetreten:\n{0}",
            [AppLanguage.Russian] = "Произошла ошибка при записи лекции:\n{0}",
            [AppLanguage.Chinese] = "课程录制过程中发生错误:\n{0}"
        },
        ["DialogRecordStartError"] = new()
        {
            [AppLanguage.English] = "Could not start recording:\n{0}",
            [AppLanguage.Turkish] = "Kayıt başlatılamadı:\n{0}",
            [AppLanguage.German] = "Aufnahme konnte nicht gestartet werden:\n{0}",
            [AppLanguage.Russian] = "Не удалось начать запись:\n{0}",
            [AppLanguage.Chinese] = "无法开始录制:\n{0}"
        },
        ["DialogFolderOpenError"] = new()
        {
            [AppLanguage.English] = "Could not open recordings folder:\n{0}",
            [AppLanguage.Turkish] = "Kayıtlar klasörü açılamadı:\n{0}",
            [AppLanguage.German] = "Aufnahmeordner konnte nicht geöffnet werden:\n{0}",
            [AppLanguage.Russian] = "Не удалось открыть папку записей:\n{0}",
            [AppLanguage.Chinese] = "无法打开录制文件夹:\n{0}"
        }
    };
}
