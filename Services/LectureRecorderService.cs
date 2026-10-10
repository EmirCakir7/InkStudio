using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Graphics.Imaging;
using ScreenRecorderLib;

namespace InkStudio.Services;

public class CameraDeviceInfo
{
    public string Name { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;

    public override string ToString() => Name;
}

public enum CameraAspectMode
{
    Aspect16x9,
    Aspect4x3,
    Aspect1x1,
    Aspect9x16,
    Custom
}

public enum RecordingState
{
    Stopped,
    Recording,
    Paused
}

public class LectureRecorderService : IDisposable
{
    private Recorder? _recorder;
    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _frameReader;
    private byte[]? _previewBuffer;

    private DisplayRecordingSource? _displaySource;
    private VideoCaptureOverlay? _currentCameraOverlay;
    private int _outputWidth = 1920;
    private int _outputHeight = 1080;

    private DispatcherTimer? _timer;
    private DateTime _recordStartTime;
    private TimeSpan _pausedElapsed = TimeSpan.Zero;
    private DateTime _pauseStartTime;
    private bool _isDisposed = false;

    public RecordingState State { get; private set; } = RecordingState.Stopped;
    public string? CurrentOutputFile { get; private set; }
    public TimeSpan ElapsedDuration { get; private set; } = TimeSpan.Zero;

    public Anchor CameraAnchor { get; set; } = Anchor.BottomRight;
    public double CameraSizeRatio { get; set; } = 0.22;
    public CameraAspectMode CameraAspectRatio { get; set; } = CameraAspectMode.Aspect16x9;
    public double CustomCameraAspectRatio { get; set; } = 16.0 / 9.0;

    public event Action<BitmapSource>? PreviewFrameAvailable;
    public event Action<RecordingState>? StateChanged;
    public event Action<TimeSpan>? DurationChanged;
    public event Action<string>? RecordingCompleted;
    public event Action<string>? RecordingFailed;

    public LectureRecorderService()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (State == RecordingState.Recording)
        {
            ElapsedDuration = (DateTime.Now - _recordStartTime) - _pausedElapsed;
            DurationChanged?.Invoke(ElapsedDuration);
        }
    }

    public List<CameraDeviceInfo> GetAvailableCameras()
    {
        var list = new List<CameraDeviceInfo>();
        try
        {
            var cameras = Recorder.GetSystemVideoCaptureDevices();
            foreach (var cam in cameras)
            {
                list.Add(new CameraDeviceInfo
                {
                    Name = cam.FriendlyName,
                    DeviceId = cam.DeviceName
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error querying cameras: {ex.Message}");
        }
        return list;
    }

    private readonly System.Threading.SemaphoreSlim _cameraLock = new(1, 1);
    private DateTime _lastPreviewFrameTime = DateTime.MinValue;

    public async Task StartCameraPreviewAsync(string? preferredCameraId = null)
    {
        await _cameraLock.WaitAsync();
        try
        {
            await StopCameraPreviewInternalAsync();

            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            if (devices.Count == 0) return;

            DeviceInformation? target = null;
            if (!string.IsNullOrEmpty(preferredCameraId))
            {
                target = devices.FirstOrDefault(d => 
                    d.Id.Equals(preferredCameraId, StringComparison.OrdinalIgnoreCase) ||
                    preferredCameraId.IndexOf(d.Id, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.Id.IndexOf(preferredCameraId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    d.Name.IndexOf(preferredCameraId, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    preferredCameraId.IndexOf(d.Name, StringComparison.OrdinalIgnoreCase) >= 0);
            }
            target ??= devices.FirstOrDefault();
            if (target == null) return;

            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = target.Id,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };

            var mc = new MediaCapture();
            await mc.InitializeAsync(settings);

            var frameSource = mc.FrameSources.Values.FirstOrDefault(fs => 
                fs.Info.MediaStreamType == MediaStreamType.VideoPreview || 
                fs.Info.MediaStreamType == MediaStreamType.VideoRecord);

            if (frameSource == null)
            {
                mc.Dispose();
                return;
            }

            var reader = await mc.CreateFrameReaderAsync(frameSource);
            reader.FrameArrived += (s, e) =>
            {
                var now = DateTime.UtcNow;
                if ((now - _lastPreviewFrameTime).TotalMilliseconds < 30) return;
                _lastPreviewFrameTime = now;

                try
                {
                    using var frame = s.TryAcquireLatestFrame();
                    if (frame?.VideoMediaFrame != null)
                    {
                        var sb = frame.VideoMediaFrame.SoftwareBitmap;
                        if (sb != null)
                        {
                            SoftwareBitmap? bgra = null;
                            bool disposeBgra = false;
                            try
                            {
                                if (sb.BitmapPixelFormat != BitmapPixelFormat.Bgra8 || sb.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
                                {
                                    bgra = SoftwareBitmap.Convert(sb, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                                    disposeBgra = true;
                                }
                                else
                                {
                                    bgra = sb;
                                }

                                int w = bgra.PixelWidth;
                                int h = bgra.PixelHeight;
                                int byteCount = w * h * 4;

                                if (_previewBuffer == null || _previewBuffer.Length != byteCount)
                                {
                                    _previewBuffer = new byte[byteCount];
                                }
                                bgra.CopyToBuffer(_previewBuffer.AsBuffer());

                                var bmp = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, _previewBuffer, w * 4);
                                bmp.Freeze();
                                PreviewFrameAvailable?.Invoke(bmp);
                            }
                            finally
                            {
                                if (disposeBgra && bgra != null)
                                {
                                    bgra.Dispose();
                                }
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore transient preview frame drop
                }
            };

            await reader.StartAsync();
            _mediaCapture = mc;
            _frameReader = reader;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to start MediaCapture preview: {ex.Message}");
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    public async Task StopCameraPreviewAsync()
    {
        await _cameraLock.WaitAsync();
        try
        {
            await StopCameraPreviewInternalAsync();
        }
        finally
        {
            _cameraLock.Release();
        }
    }

    private async Task StopCameraPreviewInternalAsync()
    {
        if (_frameReader != null)
        {
            try
            {
                await _frameReader.StopAsync();
                _frameReader.Dispose();
            }
            catch { }
            finally
            {
                _frameReader = null;
            }
        }

        if (_mediaCapture != null)
        {
            try
            {
                _mediaCapture.Dispose();
            }
            catch { }
            finally
            {
                _mediaCapture = null;
            }
        }
    }

    public List<CameraDeviceInfo> GetAvailableMicrophones()
    {
        var list = new List<CameraDeviceInfo>();
        try
        {
            var inputs = Recorder.GetSystemAudioCaptureDevices();
            foreach (var mic in inputs)
            {
                list.Add(new CameraDeviceInfo
                {
                    Name = mic.FriendlyName,
                    DeviceId = mic.DeviceName
                });
            }
        }
        catch { }
        return list;
    }

    public async Task StartRecordingAsync(
        IntPtr windowHandle, 
        string outputFilePath, 
        int width = 1920, 
        int height = 1080, 
        int fps = 30, 
        string? cameraId = null,
        string? microphoneId = null,
        float microphoneVolume = 1.0f,
        ScreenRect? cropArea = null)
    {
        if (State != RecordingState.Stopped) return;
        await Task.Yield();

        _outputWidth = width;
        _outputHeight = height;

        // Ensure directory exists
        string? dir = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        CurrentOutputFile = outputFilePath;

        // Start from DefaultMainMonitor which has all sub-options fully initialized
        var options = RecorderOptions.DefaultMainMonitor;

        // 1. Configure display source with WindowsGraphicsCapture (bypasses DesktopDuplication access errors on hybrid GPU laptops)
        // and crop strictly to Document Bounds (PDF / PPTX / Board area only)
        if (options.SourceOptions.RecordingSources.Count > 0 && options.SourceOptions.RecordingSources[0] is DisplayRecordingSource disp)
        {
            disp.RecorderApi = RecorderApi.WindowsGraphicsCapture;
            disp.IsBorderRequired = false;
            if (cropArea != null)
            {
                disp.SourceRect = cropArea;
            }
            disp.Stretch = StretchMode.UniformToFill;
            disp.OutputSize = new ScreenSize(width, height);
            disp.Position = new ScreenPoint(0, 0);
            disp.AnchorPoint = Anchor.Center;
            _displaySource = disp;
        }

        // 2. Output Video Format & Quality - UniformToFill ensures ZERO black borders across any aspect ratio
        options.OutputOptions.RecorderMode = RecorderMode.Video;
        options.OutputOptions.OutputFrameSize = new ScreenSize(width, height);
        options.OutputOptions.Stretch = StretchMode.UniformToFill;

        options.VideoEncoderOptions.Framerate = fps;
        options.VideoEncoderOptions.Quality = 80;
        options.VideoEncoderOptions.Bitrate = fps >= 60 ? 8000 * 1000 : 5000 * 1000;

        // 3. Audio: Capture selected microphone safely
        var audioInputs = Recorder.GetSystemAudioCaptureDevices();
        if (audioInputs.Count > 0)
        {
            RecordableAudioCaptureDevice? targetMic = null;
            if (!string.IsNullOrEmpty(microphoneId))
            {
                targetMic = audioInputs.FirstOrDefault(a => a.DeviceName.Equals(microphoneId, StringComparison.OrdinalIgnoreCase) ||
                                                            a.FriendlyName.Contains(microphoneId, StringComparison.OrdinalIgnoreCase));
            }
            targetMic ??= audioInputs[0];

            options.AudioOptions.IsAudioEnabled = true;
            options.AudioOptions.Channels = AudioChannels.Mono;
            options.AudioOptions.AudioSources.Clear();
            var audioSrc = new CaptureAudioSource(targetMic.DeviceName)
            {
                Volume = Math.Clamp(microphoneVolume, 0f, 1f),
                ForceMono = true
            };
            options.AudioOptions.AudioSources.Add(audioSrc);
        }
        else
        {
            options.AudioOptions.IsAudioEnabled = false;
        }

        // 4. Picture-in-Picture Webcam Overlay
        // Because MediaCapture is initialized with SharedReadOnly, camera preview continues uninterrupted!
        options.OverlayOptions.Overlays.Clear();
        _currentCameraOverlay = null;
        try
        {
            var cameras = Recorder.GetSystemVideoCaptureDevices();
            RecordableCamera? targetCamera = null;
            if (!string.IsNullOrEmpty(cameraId))
            {
                targetCamera = cameras.FirstOrDefault(c => c.DeviceName.Equals(cameraId, StringComparison.OrdinalIgnoreCase) ||
                                                           c.FriendlyName.Contains(cameraId, StringComparison.OrdinalIgnoreCase));
            }
            targetCamera ??= cameras.FirstOrDefault();

            if (targetCamera != null)
            {
                int pipWidth = (int)(width * Math.Clamp(CameraSizeRatio, 0.08, 0.60));
                int pipHeight = CameraAspectRatio switch
                {
                    CameraAspectMode.Aspect4x3 => (int)(pipWidth * 3.0 / 4.0),
                    CameraAspectMode.Aspect1x1 => pipWidth,
                    CameraAspectMode.Aspect9x16 => (int)(pipWidth * 16.0 / 9.0),
                    CameraAspectMode.Custom => (int)(pipWidth / Math.Max(0.2, CustomCameraAspectRatio)),
                    _ => (int)(pipWidth * 9.0 / 16.0) // 16:9
                };

                // Enforce even dimensions for H.264
                if (pipWidth % 2 != 0) pipWidth -= 1;
                if (pipHeight % 2 != 0) pipHeight -= 1;

                int margin = 24;
                var camOverlay = new VideoCaptureOverlay
                {
                    DeviceName = targetCamera.DeviceName,
                    AnchorPoint = CameraAnchor,
                    Offset = new ScreenSize(margin, margin),
                    Size = new ScreenSize(pipWidth, pipHeight),
                    Stretch = StretchMode.UniformToFill
                };
                options.OverlayOptions.Overlays.Add(camOverlay);
                _currentCameraOverlay = camOverlay;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to attach webcam overlay: {ex.Message}");
        }

        // Create recorder instance
        _recorder = Recorder.CreateRecorder(options);
        _recorder.OnRecordingComplete += Recorder_OnRecordingComplete;
        _recorder.OnRecordingFailed += Recorder_OnRecordingFailed;
        _recorder.OnStatusChanged += Recorder_OnStatusChanged;

        try
        {
            _recorder.Record(outputFilePath);
            _recordStartTime = DateTime.Now;
            _pausedElapsed = TimeSpan.Zero;
            ElapsedDuration = TimeSpan.Zero;
            _timer?.Start();

            State = RecordingState.Recording;
            StateChanged?.Invoke(State);
        }
        catch (Exception ex)
        {
            State = RecordingState.Stopped;
            StateChanged?.Invoke(State);
            RecordingFailed?.Invoke($"Kayıt başlatılamadı: {ex.Message}");
        }
    }

    /// <summary>
    /// Dynamically updates the crop area in real time when the user zooms or pans the document!
    /// </summary>
    public void UpdateDynamicCropArea(ScreenRect newCropRect)
    {
        if (_recorder != null && _displaySource != null && State == RecordingState.Recording)
        {
            try
            {
                var updatedDisp = new DisplayRecordingSource(_displaySource)
                {
                    SourceRect = newCropRect
                };
                var builder = _recorder.GetDynamicOptionsBuilder();
                builder.SetUpdatedRecordingSource(updatedDisp);
                bool applied = builder.Apply();
                if (applied)
                {
                    _displaySource = updatedDisp;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDynamicCropArea error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Dynamically updates the camera overlay position, size, and aspect ratio during active recording.
    /// </summary>
    public void UpdateDynamicCameraOverlay(Anchor newAnchor, double newSizeRatio, CameraAspectMode newAspect, double? customAspect = null)
    {
        CameraAnchor = newAnchor;
        CameraSizeRatio = newSizeRatio;
        CameraAspectRatio = newAspect;
        if (customAspect.HasValue)
        {
            CustomCameraAspectRatio = customAspect.Value;
        }

        if (_recorder != null && _currentCameraOverlay != null && State == RecordingState.Recording)
        {
            try
            {
                int pipWidth = (int)(_outputWidth * Math.Clamp(CameraSizeRatio, 0.08, 0.60));
                int pipHeight = CameraAspectRatio switch
                {
                    CameraAspectMode.Aspect4x3 => (int)(pipWidth * 3.0 / 4.0),
                    CameraAspectMode.Aspect1x1 => pipWidth,
                    CameraAspectMode.Aspect9x16 => (int)(pipWidth * 16.0 / 9.0),
                    CameraAspectMode.Custom => (int)(pipWidth / Math.Max(0.2, CustomCameraAspectRatio)),
                    _ => (int)(pipWidth * 9.0 / 16.0)
                };
                if (pipWidth % 2 != 0) pipWidth -= 1;
                if (pipHeight % 2 != 0) pipHeight -= 1;

                int margin = 24;
                var updatedOverlay = new VideoCaptureOverlay(_currentCameraOverlay)
                {
                    AnchorPoint = CameraAnchor,
                    Offset = new ScreenSize(margin, margin),
                    Size = new ScreenSize(pipWidth, pipHeight),
                    Stretch = StretchMode.UniformToFill
                };
                var builder = _recorder.GetDynamicOptionsBuilder();
                builder.SetUpdatedOverlay(updatedOverlay);
                bool applied = builder.Apply();
                if (applied)
                {
                    _currentCameraOverlay = updatedOverlay;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateDynamicCameraOverlay error: {ex.Message}");
            }
        }
    }

    public void PauseRecording()
    {
        if (State == RecordingState.Recording && _recorder != null)
        {
            try
            {
                _recorder.Pause();
                _pauseStartTime = DateTime.Now;
                State = RecordingState.Paused;
                StateChanged?.Invoke(State);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Pause failed: {ex.Message}");
            }
        }
    }

    public void ResumeRecording()
    {
        if (State == RecordingState.Paused && _recorder != null)
        {
            try
            {
                _recorder.Resume();
                _pausedElapsed += (DateTime.Now - _pauseStartTime);
                State = RecordingState.Recording;
                StateChanged?.Invoke(State);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Resume failed: {ex.Message}");
            }
        }
    }

    public void StopRecording()
    {
        if (State != RecordingState.Stopped && _recorder != null)
        {
            try
            {
                _timer?.Stop();
                _recorder.Stop();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Stop failed: {ex.Message}");
            }
        }
    }

    private void Recorder_OnStatusChanged(object? sender, RecordingStatusEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Recorder status: {e.Status}");
    }

    private void Recorder_OnRecordingComplete(object? sender, RecordingCompleteEventArgs e)
    {
        State = RecordingState.Stopped;
        _timer?.Stop();

        var rec = _recorder;
        _recorder = null;
        _displaySource = null;
        _currentCameraOverlay = null;

        StateChanged?.Invoke(State);
        RecordingCompleted?.Invoke(e.FilePath);

        // Dispose recorder on background thread to prevent deadlocking native callback thread
        if (rec != null)
        {
            Task.Run(() =>
            {
                try
                {
                    rec.Dispose();
                }
                catch { }
            });
        }
    }

    private void Recorder_OnRecordingFailed(object? sender, RecordingFailedEventArgs e)
    {
        State = RecordingState.Stopped;
        _timer?.Stop();

        var rec = _recorder;
        _recorder = null;
        _displaySource = null;
        _currentCameraOverlay = null;

        StateChanged?.Invoke(State);
        RecordingFailed?.Invoke(e.Error);

        if (rec != null)
        {
            Task.Run(() =>
            {
                try
                {
                    rec.Dispose();
                }
                catch { }
            });
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _timer?.Stop();
        _recorder?.Dispose();
        _recorder = null;
        _displaySource = null;
        _currentCameraOverlay = null;

        _ = StopCameraPreviewAsync();
    }
}
