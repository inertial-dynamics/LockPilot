using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using GroundCon.GStreamer;
using GroundCon.Views;
using LockPilot.Shared;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Screen = Caliburn.Micro.Screen;

namespace GroundCon.ViewModels;

sealed class MainViewModel : Screen, IDisposable
{
    readonly AppSettings m_Settings;
    readonly CommandClient m_CommandClient;
    readonly OverlayReader m_OverlayReader;
    readonly GstRtpReceiver m_RtpReceiver;
    readonly Task m_OverlayTask;
    readonly Task m_RtpTask;

    public MainViewModel()
    {
        m_Settings = AppSettings.Load(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
        m_CommandClient = new(m_Settings.LockPilot.Host, m_Settings.LockPilot.CommandPort);
        m_OverlayReader = new(m_Settings.OverlayPort);
        m_RtpReceiver = GstRtpReceiver.Open(m_Settings.RtpPort);
        if (!m_RtpReceiver.IsOpened)
        {
            m_CommandClient.Dispose();
            m_OverlayReader.Dispose();
            m_RtpReceiver.Dispose();
            m_ReceiveToken.Dispose();
            throw new InvalidOperationException($"Cannot open RTP receiver: {m_RtpReceiver.Error}");
        }

        m_CommandClient.Send(new SetupMessage
        {
            AimWidth = m_Settings.AimWidth,
            AimHeight = m_Settings.AimHeight
        });

        m_OverlayTask = Task.Run(ReceiveOverlay);
        m_RtpTask = Task.Run(ReceiveRtp);
    }

    bool m_Disposed;

    public void Dispose()
    {
        if (m_Disposed)
        {
            return;
        }

        m_ReceiveToken.Cancel();
        try
        {
            Task.WaitAll(m_OverlayTask, m_RtpTask);
        }
        catch (AggregateException)
        {
        }

        m_CommandClient.Dispose();
        m_OverlayReader.Dispose();
        m_RtpReceiver.Dispose();
        m_ReceiveToken.Dispose();

        m_Disposed = true;
    }

    public int AimWidth => m_Settings.AimWidth;

    public int AimHeight => m_Settings.AimHeight;

    public double AimLeft => (FrameWidth - AimWidth) / 2.0;

    public double AimTop => (FrameHeight - AimHeight) / 2.0;

    int m_FrameWidth;

    public int FrameWidth
    {
        get => m_FrameWidth;
        private set
        {
            if (Set(ref m_FrameWidth, value))
            {
                NotifyOfPropertyChange(nameof(AimLeft));
                RecalcFit();
            }
        }
    }

    int m_FrameHeight;

    public int FrameHeight
    {
        get => m_FrameHeight;
        private set
        {
            if (Set(ref m_FrameHeight, value))
            {
                NotifyOfPropertyChange(nameof(AimTop));
                RecalcFit();
            }
        }
    }

    TargetTrackerState m_TrackerState;

    public TargetTrackerState TrackerState
    {
        get => m_TrackerState;
        private set
        {
            if (Set(ref m_TrackerState, value))
            {
                NotifyOfPropertyChange(nameof(IsTracking));
                NotifyOfPropertyChange(nameof(TrackerStateBrush));
            }
        }
    }

    public bool IsTracking => TrackerState == TargetTrackerState.Tracking;

    public IBrush TrackerStateBrush => TrackerState switch
    {
        TargetTrackerState.Tracking => Brushes.Lime,
        TargetTrackerState.Lost => Brushes.Red,
        _ => Brushes.LightGray
    };

    OverlayRect m_OverlayRect;

    public OverlayRect OverlayRect
    {
        get => m_OverlayRect;
        private set => Set(ref m_OverlayRect, value);
    }

    string m_TargetLabel;

    public string TargetLabel
    {
        get => m_TargetLabel;
        private set
        {
            if (Set(ref m_TargetLabel, value))
            {
                NotifyOfPropertyChange(nameof(HasTargetLabel));
            }
        }
    }

    public bool HasTargetLabel => TargetLabel != null;

    string m_FpsLabel;

    public string FpsLabel
    {
        get => m_FpsLabel;
        private set => Set(ref m_FpsLabel, value);
    }

    string m_YoloElapsedLabel;

    public string YoloElapsedLabel
    {
        get => m_YoloElapsedLabel;
        private set
        {
            if (Set(ref m_YoloElapsedLabel, value))
            {
                NotifyOfPropertyChange(nameof(HasYoloElapsedLabel));
            }
        }
    }

    public bool HasYoloElapsedLabel => YoloElapsedLabel != null;

    Size m_VideoSize;

    public void OnVideoSizeChanged(Size size)
    {
        m_VideoSize = size;
        RecalcFit();
    }

    private void RecalcFit()
    {
        if (FrameWidth > 0 && FrameHeight > 0 && m_VideoSize.Width > 0 && m_VideoSize.Height > 0)
        {
            FitScale = Math.Min(m_VideoSize.Width / FrameWidth, m_VideoSize.Height / FrameHeight);
        }
    }

    double m_FitScale = 1;

    public double FitScale
    {
        get => m_FitScale;
        private set
        {
            if (Set(ref m_FitScale, value))
            {
                NotifyOfPropertyChange(nameof(OverlayStrokeThickness));
            }
        }
    }

    public double OverlayStrokeThickness => 2 / FitScale;

    public void Capture() => Send(Command.Capture);

    public void Reset() => Send(Command.Reset);

    public async Task Quit()
    {
        Send(Command.Quit);

        await TryCloseAsync();
    }

    private void Send(Command command) => m_CommandClient.Send(new CommandMessage { Command = command });

    IMainView m_View;

    protected override void OnViewLoaded(object view) => m_View = view as IMainView;

    protected override Task OnDeactivateAsync(bool close, CancellationToken cancellationToken)
    {
        if (close)
        {
            Dispose();
        }
        return Task.CompletedTask;
    }

    readonly CancellationTokenSource m_ReceiveToken = new();

    private async Task ReceiveOverlay()
    {
        while (!m_ReceiveToken.IsCancellationRequested)
        {
            try
            {
                var message = await m_OverlayReader.Read(m_ReceiveToken.Token);
                Dispatcher.UIThread.Post(() =>
                {
                    TrackerState = message.State;
                    OverlayRect = message.Rect;
                    TargetLabel = message.ClassName != null ? $"{message.ClassName} {message.Confidence:p0}" : null;
                    YoloElapsedLabel = message.YoloElapsedMilliseconds != null ? $"Reloc: {message.YoloElapsedMilliseconds} ms" : null;
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    FrameBuffer m_Frame;
    readonly Lock m_FrameLock = new();
    bool m_FramePosted;

    private void ReceiveRtp()
    {
        FrameBuffer frame = null;
        while (!m_ReceiveToken.IsCancellationRequested)
        {
            if (m_RtpReceiver.Read(ref frame))
            {
                var byteCount = frame.Width * frame.Height * 4;
                lock (m_FrameLock)
                {
                    if (m_Frame == null || m_Frame.Data.Length != byteCount)
                    {
                        m_Frame = new(new byte[byteCount], frame.Width, frame.Height);
                    }
                    Buffer.BlockCopy(frame.Data, 0, m_Frame.Data, 0, byteCount);
                    if (!m_FramePosted)
                    {
                        Dispatcher.UIThread.Post(ApplyFrame, DispatcherPriority.Render);
                        m_FramePosted = true;
                    }
                }
            }
        }
    }

    int m_FpsFrameCount;
    readonly Stopwatch m_FpsWatch = Stopwatch.StartNew();

    private void ApplyFrame()
    {
        lock (m_FrameLock)
        {
            if (FrameBitmap == null || FrameBitmap.PixelSize.Width != m_Frame.Width || FrameBitmap.PixelSize.Height != m_Frame.Height)
            {
                FrameBitmap = new(new(m_Frame.Width, m_Frame.Height), new(96, 96), PixelFormats.Bgra8888);
                FrameWidth = m_Frame.Width;
                FrameHeight = m_Frame.Height;
            }

            using (var lockedBuffer = FrameBitmap.Lock())
            {
                var rowBytes = m_Frame.Width * 4;
                if (rowBytes == lockedBuffer.RowBytes)
                {
                    Marshal.Copy(m_Frame.Data, 0, lockedBuffer.Address, rowBytes * m_Frame.Height);
                }
                else
                {
                    for (var y = 0; y < m_Frame.Height; ++y)
                    {
                        Marshal.Copy(m_Frame.Data, y * rowBytes, lockedBuffer.Address + y * lockedBuffer.RowBytes, rowBytes);
                    }
                }
            }
            m_FramePosted = false;
        }

        m_FpsFrameCount++;
        var elapsedSeconds = m_FpsWatch.Elapsed.TotalSeconds;
        if (elapsedSeconds >= 1)
        {
            FpsLabel = $"FPS: {m_FpsFrameCount / elapsedSeconds:0}";
            m_FpsFrameCount = 0;
            m_FpsWatch.Restart();
        }

        m_View?.InvalidateVideo();
    }

    WriteableBitmap m_FrameBitmap;

    public WriteableBitmap FrameBitmap
    {
        get => m_FrameBitmap;
        private set => Set(ref m_FrameBitmap, value);
    }
}
