using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WakeyWakey;

/// <summary>
/// Manages the system-tray icon and the sleep-suppression lifecycle.
/// Left-click toggles suppression (indefinite). Right-click opens the
/// full menu with duration presets and an Exit item.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    // Needed to properly release GDI icon handles created via Bitmap.GetHicon().
    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);

    // ── State ────────────────────────────────────────────────────────────────
    private bool     _isActive;
    private DateTime _suppressUntil = DateTime.MinValue;

    // ── Controls ─────────────────────────────────────────────────────────────
    private readonly NotifyIcon                _trayIcon;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private readonly ToolStripMenuItem         _statusItem;
    private readonly ToolStripMenuItem         _stopItem;
    private readonly ToolStripSeparator        _stopSeparator;

    // ── Construction ─────────────────────────────────────────────────────────

    public TrayApplicationContext()
    {
        // Fires every 15 seconds while a timed suppression is running.
        _countdownTimer = new System.Windows.Forms.Timer { Interval = 15_000 };
        _countdownTimer.Tick += OnCountdownTick;

        // Header row – shows current status, not clickable.
        _statusItem = new ToolStripMenuItem { Enabled = false };

        // Only visible while active.
        _stopSeparator = new ToolStripSeparator { Visible = false };
        _stopItem      = new ToolStripMenuItem("Stop keeping awake", null, OnStop) { Visible = false };

        var keepAwakeMenu = new ToolStripMenuItem("Keep awake");
        keepAwakeMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            MakeDurationItem("Until stopped",  null),
            new ToolStripSeparator(),
            MakeDurationItem("For 30 minutes", TimeSpan.FromMinutes(30)),
            MakeDurationItem("For 1 hour",     TimeSpan.FromHours(1)),
            MakeDurationItem("For 2 hours",    TimeSpan.FromHours(2)),
            MakeDurationItem("For 4 hours",    TimeSpan.FromHours(4)),
            MakeDurationItem("For 8 hours",    TimeSpan.FromHours(8)),
        });

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            new ToolStripSeparator(),
            keepAwakeMenu,
            _stopSeparator,
            _stopItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, OnExit),
        });

        _trayIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Visible          = true,
        };
        _trayIcon.MouseClick += OnTrayClick;

        RefreshUi();
    }

    // ── Menu helpers ─────────────────────────────────────────────────────────

    private ToolStripMenuItem MakeDurationItem(string label, TimeSpan? duration)
    {
        var item = new ToolStripMenuItem(label);
        item.Click += (_, _) => Activate(duration);
        return item;
    }

    // ── Suppression logic ────────────────────────────────────────────────────

    private void Activate(TimeSpan? duration)
    {
        _suppressUntil         = duration.HasValue ? DateTime.UtcNow + duration.Value : DateTime.MaxValue;
        _isActive              = PowerManager.SuppressSleep();
        _countdownTimer.Enabled = _isActive && duration.HasValue;
        RefreshUi();
    }

    private void Deactivate()
    {
        _isActive      = false;
        _suppressUntil = DateTime.MinValue;
        _countdownTimer.Stop();
        PowerManager.RestoreSleep();
        RefreshUi();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow >= _suppressUntil)
            Deactivate();
        else
            RefreshUi();   // update remaining-time display
    }

    /// <summary>Left-click toggles on/off (indefinite by default).</summary>
    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        if (_isActive) Deactivate();
        else           Activate(null);
    }

    private void OnStop(object? sender, EventArgs e) => Deactivate();

    private void OnExit(object? sender, EventArgs e)
    {
        Deactivate();
        _trayIcon.Visible = false;
        Application.Exit();
    }

    // ── UI refresh ────────────────────────────────────────────────────────────

    private void RefreshUi()
    {
        if (_isActive)
        {
            string timeDesc = _suppressUntil == DateTime.MaxValue
                ? "until stopped"
                : $"for {FormatRemaining(_suppressUntil - DateTime.UtcNow)}";

            _statusItem.Text = $"Active \u2014 sleep suppressed {timeDesc}";
            // NotifyIcon tooltip is capped at 63 chars on older Windows versions.
            _trayIcon.Text   = Truncate($"WakeyWakey: awake {timeDesc}", 63);
        }
        else
        {
            _statusItem.Text = "Inactive \u2014 sleep allowed";
            _trayIcon.Text   = "WakeyWakey: click to keep awake";
        }

        _stopSeparator.Visible = _isActive;
        _stopItem.Visible      = _isActive;

        // Swap the tray icon graphic.
        var old = _trayIcon.Icon;
        _trayIcon.Icon = BuildTrayIcon(_isActive);
        old?.Dispose();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];

    private static string FormatRemaining(TimeSpan t)
    {
        if (t <= TimeSpan.Zero) return "0m";
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}h {t.Minutes:D2}m"
            : $"{t.Minutes}m";
    }

    // ── Icon rendering ────────────────────────────────────────────────────────

    /// <summary>
    /// Draws a small coloured circle into a 16×16 bitmap and returns it
    /// as an <see cref="Icon"/>.  Green = active, grey = inactive.
    /// </summary>
    private static Icon BuildTrayIcon(bool active)
    {
        const int N = 16;
        using var bmp = new Bitmap(N, N, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            Color fillColour   = active ? Color.FromArgb(40,  200, 70)  : Color.FromArgb(90,  90,  90);
            Color borderColour = active ? Color.FromArgb(20,  130, 40)  : Color.FromArgb(55,  55,  55);

            using (var brush = new SolidBrush(fillColour))
                g.FillEllipse(brush, 1, 1, N - 3, N - 3);

            using (var pen = new Pen(borderColour, 1.5f))
                g.DrawEllipse(pen, 1, 1, N - 3, N - 3);

            if (active)
            {
                // Subtle specular highlight to give a "lit" appearance.
                using var hi = new SolidBrush(Color.FromArgb(130, 255, 255, 255));
                g.FillEllipse(hi, 3, 3, 5, 4);
            }
        }

        // GetHicon creates an unmanaged handle; Clone() gives us a managed
        // Icon that owns its own copy, then we release the raw handle.
        IntPtr hIcon = bmp.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Deactivate();
            _trayIcon.Visible = false;
            _trayIcon.Icon?.Dispose();
            _trayIcon.Dispose();
            _countdownTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
