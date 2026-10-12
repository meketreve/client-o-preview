using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ClientOPreview.Models;
using ClientOPreview.Native;
using ClientOPreview.Services;

namespace ClientOPreview.Linux.Platform;

/// <summary>
/// Captures every preview on a background thread with its own X connection, so the round trips
/// to the X server (one or two per preview per frame) never block the UI. The UI thread only
/// posts what it wants (<see cref="CaptureJob.Request"/>) and picks up finished frames.
/// </summary>
internal sealed class CaptureWorker : IDisposable
{
    private readonly X11 _x;
    private readonly Thread _thread;
    private readonly object _gate = new();
    private readonly List<CaptureJob> _jobs = new();
    private volatile bool _running = true;

    public CaptureWorker(int framesPerSecond = 10)
    {
        _x = X11.Open() ?? throw new InvalidOperationException("cannot open X display for capture");
        FrameInterval = TimeSpan.FromMilliseconds(1000.0 / Math.Clamp(framesPerSecond, 1, 30));
        _thread = new Thread(Loop) { IsBackground = true, Name = "x11-capture", Priority = ThreadPriority.BelowNormal };
        _thread.Start();
    }

    public TimeSpan FrameInterval { get; }

    public CaptureJob Add(IntPtr client)
    {
        var job = new CaptureJob(client);
        lock (_gate) _jobs.Add(job);
        return job;
    }

    /// <summary>Stops capturing; the worker releases the X resources on its own thread.</summary>
    public void Remove(CaptureJob job) => job.Cancelled = true;

    private void Loop()
    {
        var clock = Stopwatch.StartNew();
        var captures = new Dictionary<CaptureJob, WindowCapture>();
        try
        {
            while (_running)
            {
                var started = clock.Elapsed;
                List<CaptureJob> jobs;
                lock (_gate)
                {
                    _jobs.RemoveAll(j => j.Cancelled && !captures.ContainsKey(j));
                    jobs = new List<CaptureJob>(_jobs);
                }

                foreach (var job in jobs)
                {
                    if (job.Cancelled)
                    {
                        if (captures.Remove(job, out var dead)) dead.Dispose();
                        continue;
                    }
                    try
                    {
                        if (!captures.TryGetValue(job, out var capture))
                        {
                            capture = new WindowCapture(_x, job.Client);
                            captures[job] = capture;
                        }
                        job.Run(capture);
                    }
                    catch (Exception ex)
                    {
                        AppLog.Warn("capture worker", ex);
                    }
                }

                var wait = FrameInterval - (clock.Elapsed - started);
                if (wait > TimeSpan.Zero) Thread.Sleep(wait);
            }
        }
        finally
        {
            foreach (var c in captures.Values) c.Dispose();
            _x.Dispose();
        }
    }

    public void Dispose()
    {
        _running = false;
        _thread.Join(1000);
    }
}

/// <summary>What the UI wants for the next frame of one preview.</summary>
internal sealed record CaptureRequest(int AreaW, int AreaH, RegionPreset? Region, bool Zoomed, Zoom Zoom);

/// <summary>One preview's mailbox between the UI thread and the <see cref="CaptureWorker"/>.</summary>
internal sealed class CaptureJob
{
    private readonly object _gate = new();
    private byte[] _back = Array.Empty<byte>();
    private byte[] _front = Array.Empty<byte>();
    private int _w, _h;
    private bool _fresh;

    public CaptureJob(IntPtr client) => Client = client;

    public IntPtr Client { get; }
    public volatile CaptureRequest? Request;
    public volatile bool Cancelled;

    /// <summary>The client window was destroyed.</summary>
    public volatile bool Gone;

    private NativeMethods.SIZE _sourceSize;
    public NativeMethods.SIZE SourceSize { get { lock (_gate) return _sourceSize; } }

    /// <summary>Worker thread: capture into the back buffer, then swap.</summary>
    internal void Run(WindowCapture capture)
    {
        var req = Request;
        if (req == null) return;
        var size = capture.Capture(req.AreaW, req.AreaH, req.Region, req.Zoomed, req.Zoom, ref _back);
        Gone = capture.Gone;
        lock (_gate)
        {
            _sourceSize = capture.SourceSize;
            if (size is not { } s) return;
            (_front, _back) = (_back, _front);
            _w = s.W;
            _h = s.H;
            _fresh = true;
        }
    }

    /// <summary>UI thread: hands the newest frame to <paramref name="consume"/>, once.</summary>
    public bool TryTake(Action<byte[], int, int> consume)
    {
        lock (_gate)
        {
            if (!_fresh) return false;
            _fresh = false;
            consume(_front, _w, _h);
            return true;
        }
    }
}
