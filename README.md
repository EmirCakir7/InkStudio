# InkStudio 🖋️🎥

<p align="center">
  <img src="Assets/logo.png" alt="InkStudio Logo" width="128" height="128" />
</p>

<p align="center">
  <b>Modern MPP 2.0 Digital Inking, Annotation & Integrated Lecture Recording Studio</b><br>
  Built with .NET 9, WPF, and Windows Graphics Capture (WGC).
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-9.0_Windows-512BD4?logo=dotnet&logoColor=white" alt=".NET 9" />
  <img src="https://img.shields.io/badge/Platform-Windows_10%2F11_x64-0078D6?logo=windows&logoColor=white" alt="Platform" />
  <img src="https://img.shields.io/badge/MPP-2.0_Stylus_Ready-059669" alt="MPP 2.0" />
  <img src="https://img.shields.io/badge/Languages-EN%20%7C%20TR%20%7C%20DE%20%7C%20RU%20%7C%20ZH-blueviolet" alt="i18n" />
  <img src="https://img.shields.io/badge/License-MIT-green" alt="License" />
</p>

---

## 🌟 Highlights

**InkStudio** is a next-generation Windows desktop application engineered for educators, content creators, students, and professionals who demand a responsive digital whiteboard, seamless PDF/presentation annotation, and an integrated studio for recording high-fidelity video lectures without black bars or setup hassles.

---

## ✨ Features

### 🖊️ Hardware-Accelerated MPP 2.0 Stylus & Ink Canvas
- **4096 Levels of Hardware Pressure:** Natural stroke dynamics with real-time pressure sensitivity.
- **Hardware Tilt Detection:** Dynamic shading with tilt angle (`TiltX` and `TiltY`).
- **Hardware Palm Rejection:** Rest your hand freely on the touchscreen while writing with an active stylus.
- **Hardware Button Support:**
  - Inverted stylus top automatically switches to an eraser.
  - Barrel/side button initiates lasso selection.
- **5 Drawing Instruments:** Pen, Pencil, Highlighter, Eraser (Stroke & Point), and Lasso Selection.
- **25 Color Swatches & Precision Thickness:** Quick picker and `-` / `+` thickness adjustments.

### 🎥 Integrated Lecture Recording Studio
- **Zero-Black-Bars Document Recording:** The screen capture dynamically hugs the visible document bounds, ensuring 100% of the video canvas is clean document content without letterboxing.
- **Multi-Aspect Ratio & Resolution:**
  - `📄 Fit to Document` (Zero Blank Margin)
  - `📺 16:9 Full HD (1080p)` & `HD (720p)`
  - `🖥️ 4:3 Classic Presentation`
  - `📱 9:16 Vertical Video` (Reels, TikTok, Shorts)
  - `⏹️ 1:1 Square Video`
- **Picture-in-Picture (PiP) Webcam:**
  - 4 Corner Snap Zones (Top-Left, Top-Right, Bottom-Left, Bottom-Right).
  - Custom size presets (15%, 22%, 30%, 40%) and slider.
  - Camera Aspect Options: 16:9, 4:3, 1:1, 9:16, or custom ratio.
  - Horizontal Flip / Mirror mode for natural camera interaction.
- **Live Video Composition Preview:** Real-time mini preview displaying both slide content and webcam placement as they will appear in the output MP4 file.
- **Live Audio Meter:** Real-time microphone input volume indicator with peak monitoring.
- **Hardware H.264 Encoder:** Smooth 30fps and 60fps recording output direct to MP4.

### 📑 Document & Presentation Annotations
- **PDF & PowerPoint (PPTX / PPT) Ingestion:** High-resolution page scanning and rendering.
- **Vector Merged PDF Export:** Saves ink annotations directly into vector PDF documents without quality loss.
- **Multi-Page Management:** Sidebar thumbnails, page jumping, add/remove pages.

### 🌐 Multilingual (100% Localized)
Fully dynamic language switcher with zero app restarts required:
- 🇬🇧 **English** (Default)
- 🇹🇷 **Türkçe**
- 🇩🇪 **Deutsch**
- 🇷🇺 **Русский**
- 🇨🇳 **中文**

### 🖐️ Multi-Touch & Navigation
- Smooth two-finger pinch-to-zoom and pan navigation.
- Dedicated "Touch Inking" toggle for touchscreen-only devices.

---

## 🛠️ Tech Stack & Architecture

- **Framework:** .NET 9 (WPF Windows 10/11 x64)
- **Stylus Engine:** Windows Ink Platform with MPP 2.0 WinPointer APIs
- **Screen Recording:** [ScreenRecorderLib](https://github.com/AaronAalto/ScreenRecorderLib) (DirectX & WGC Windows Graphics Capture)
- **Audio Metering:** [NAudio](https://github.com/naudio/NAudio)
- **PDF Processing:** [PdfiumViewer](https://github.com/pvginkel/PdfiumViewer) & `Windows.Data.Pdf`
- **Slide Processing:** `DocumentFormat.OpenXml`

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11 (64-bit)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Clone & Build
```bash
# Clone the repository
git clone https://github.com/<your-username>/InkStudio.git
cd InkStudio

# Restore dependencies & build
dotnet build -c Release -r win-x64

# Publish single-folder standalone executable
dotnet publish -c Release -p:Platform=x64 -r win-x64 --self-contained false -o publish
```

### Running the App
```bash
dotnet run --project InkStudio.csproj
```
or run `publish/InkStudio.exe` directly.

---

## 📄 License
This project is licensed under the [MIT License](LICENSE).
