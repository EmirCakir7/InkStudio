using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace InkStudio.Services;

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
    [PreserveSig]
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    [PreserveSig]
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    [PreserveSig]
    int RegisterEndpointNotificationCallback(IntPtr pClient);
    [PreserveSig]
    int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    [PreserveSig]
    int OpenPropertyStore(int stgmAccess, out IntPtr ppProperties);
    [PreserveSig]
    int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    [PreserveSig]
    int GetState(out int pdwState);
}

[Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioClient
{
    [PreserveSig]
    int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, IntPtr audioSessionGuid);
    [PreserveSig]
    int GetBufferSize(out uint pNumBufferFrames);
    [PreserveSig]
    int GetStreamLatency(out long phnsLatency);
    [PreserveSig]
    int GetCurrentPadding(out uint pNumPaddingFrames);
    [PreserveSig]
    int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
    [PreserveSig]
    int GetMixFormat(out IntPtr ppDeviceFormat);
    [PreserveSig]
    int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
    [PreserveSig]
    int Start();
    [PreserveSig]
    int Stop();
    [PreserveSig]
    int Reset();
    [PreserveSig]
    int SetEventHandle(IntPtr eventHandle);
    [PreserveSig]
    int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object ppv);
}

[Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioCaptureClient
{
    [PreserveSig]
    int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
    [PreserveSig]
    int ReleaseBuffer(uint numFramesRead);
    [PreserveSig]
    int GetNextPacketSize(out uint pNumFramesInNextPacket);
}

[Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioMeterInformation
{
    [PreserveSig]
    int GetPeakValue(out float pfPeak);
    [PreserveSig]
    int GetMeteringChannelCount(out int pnChannelCount);
    [PreserveSig]
    int GetChannelsPeakValues(int u32ChannelCount, [In, Out] float[] afPeakValues);
    [PreserveSig]
    int QueryHardwareSupport(out int pdwHardwareSupportMask);
}

public class AudioMeterService : IDisposable
{
    private static readonly Guid IID_IAudioClient = new("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
    private static readonly Guid IID_IAudioCaptureClient = new("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
    private static readonly Guid IID_IAudioMeterInformation = new("C02216F6-8C67-4B5B-9D00-D008E73E0064");

    private readonly DispatcherTimer _timer;
    private IAudioClient? _audioClient;
    private IAudioCaptureClient? _captureClient;
    private IAudioMeterInformation? _currentMeter;
    private string? _currentDeviceId;
    private bool _isDisposed = false;

    public event Action<float>? PeakValueChanged;

    public AudioMeterService()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 Hz smooth sampling
        };
        _timer.Tick += (s, e) => PollPeakValue();
    }

    private void PollPeakValue()
    {
        if (_isDisposed) return;

        try
        {
            // Drain captured packets so WASAPI buffer doesn't stall
            if (_captureClient != null)
            {
                uint packetLength;
                while (_captureClient.GetNextPacketSize(out packetLength) == 0 && packetLength > 0)
                {
                    if (_captureClient.GetBuffer(out _, out uint framesRead, out _, out _, out _) == 0)
                    {
                        _captureClient.ReleaseBuffer(framesRead);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            // Read hardware peak level from IAudioMeterInformation
            if (_currentMeter != null)
            {
                if (_currentMeter.GetPeakValue(out float peak) == 0)
                {
                    PeakValueChanged?.Invoke(Math.Clamp(peak, 0f, 1f));
                    return;
                }
            }
        }
        catch
        {
            // Ignore temporary polling glitches
        }

        PeakValueChanged?.Invoke(0f);
    }

    public void StartMonitoring(string? deviceId = null)
    {
        _currentDeviceId = deviceId;
        UpdateCurrentMeter();
        _timer.Start();
    }

    public void StopMonitoring()
    {
        _timer.Stop();
        CleanupAudioEndpoints();
        PeakValueChanged?.Invoke(0f);
    }

    public void SetDevice(string? deviceId)
    {
        _currentDeviceId = deviceId;
        UpdateCurrentMeter();
    }

    private void UpdateCurrentMeter()
    {
        CleanupAudioEndpoints();

        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice? dev = null;

            // 1. Try to open the requested device ID
            if (!string.IsNullOrEmpty(_currentDeviceId))
            {
                int hr = enumerator.GetDevice(_currentDeviceId, out var requestedDev);
                if (hr == 0 && requestedDev != null)
                {
                    dev = requestedDev;
                }
            }

            // 2. If no specific device or failed, fall back to default capture device
            if (dev == null)
            {
                // 1 = eCapture (Microphone), 1 = eMultimedia role
                int hr = enumerator.GetDefaultAudioEndpoint(1, 1, out var defaultDev);
                if (hr == 0 && defaultDev != null)
                {
                    dev = defaultDev;
                }
                else
                {
                    // Fall back to eConsole role (0)
                    enumerator.GetDefaultAudioEndpoint(1, 0, out dev);
                }
            }

            if (dev != null)
            {
                // Activate IAudioClient to power on the capture stream in shared mode
                Guid iidClient = IID_IAudioClient;
                if (dev.Activate(ref iidClient, 1 /* CLSCTX_INPROC_SERVER */, IntPtr.Zero, out object clientObj) == 0 && clientObj is IAudioClient audioClient)
                {
                    _audioClient = audioClient;
                    if (_audioClient.GetMixFormat(out IntPtr pFormat) == 0 && pFormat != IntPtr.Zero)
                    {
                        try
                        {
                            // 1-second shared buffer (10,000,000 in 100ns units)
                            if (_audioClient.Initialize(0 /* AUDCLNT_SHAREMODE_SHARED */, 0, 10000000, 0, pFormat, IntPtr.Zero) == 0)
                            {
                                Guid iidCapture = IID_IAudioCaptureClient;
                                if (_audioClient.GetService(ref iidCapture, out object captureObj) == 0 && captureObj is IAudioCaptureClient captureClient)
                                {
                                    _captureClient = captureClient;
                                }

                                _audioClient.Start();
                            }
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(pFormat);
                        }
                    }
                }

                // Activate IAudioMeterInformation to read peak values
                Guid iidMeter = IID_IAudioMeterInformation;
                if (dev.Activate(ref iidMeter, 1, IntPtr.Zero, out object meterObj) == 0 && meterObj is IAudioMeterInformation meter)
                {
                    _currentMeter = meter;
                }
            }
        }
        catch
        {
            CleanupAudioEndpoints();
        }
    }

    private void CleanupAudioEndpoints()
    {
        try
        {
            _audioClient?.Stop();
        }
        catch { }

        _audioClient = null;
        _captureClient = null;
        _currentMeter = null;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopMonitoring();
    }
}
