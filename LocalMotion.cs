#if V2_BETA
using OpenCvSharp;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace HomeCamMonitor;

internal sealed record MotionZone(double X, double Y, double Width, double Height)
{
    internal bool Valid => new[] { X, Y, Width, Height }.All(double.IsFinite) && X >= 0 && Y >= 0 && Width > 0 && Height > 0 && X + Width <= 1.000001 && Y + Height <= 1.000001;
}
internal sealed class LocalMotionSettings
{
    public bool Enabled { get; set; }
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Sensitivity { get; set; } = 50;
    public double MinimumAreaPercent { get; set; } = 1;
    public double MinimumSeconds { get; set; } = 0.8;
    public double HoldSeconds { get; set; } = 2;
    public List<MotionZone> Zones { get; set; } = [];
    internal LocalMotionSettings Clone() => JsonSerializer.Deserialize<LocalMotionSettings>(JsonSerializer.Serialize(this))!;
    internal void Validate()
    {
        if (Id is null || Id.Length > 100 || Sensitivity is < 1 or > 100 || !double.IsFinite(MinimumAreaPercent) || MinimumAreaPercent is < 0.05 or > 50 ||
            !double.IsFinite(MinimumSeconds) || MinimumSeconds is < 0.2 or > 10 || !double.IsFinite(HoldSeconds) || HoldSeconds is < 0.5 or > 30 ||
            Zones is null || Zones.Count > 16 || Zones.Any(z => z is null || !z.Valid)) throw new ArgumentException("Ungültige Einstellungen für die lokale Bewegungserkennung.");
    }
    internal static LocalMotionSettings ForRow(DataGridViewRow row)
    {
        if (row.Cells["LocalMotion"].Value is LocalMotionSettings value) return value;
        var fresh = new LocalMotionSettings(); row.Cells["LocalMotion"].Value = fresh; return fresh;
    }
}
internal readonly record struct MotionAnalysis(bool WarmingUp, bool Active, bool Triggered, double AreaPercent);
internal sealed class LocalMotionModel : IDisposable
{
    static LocalMotionModel() => Cv2.SetNumThreads(1);
    internal const int Width = 320, Height = 180, FrameBytes = Width * Height;
    private readonly LocalMotionSettings config;
    private readonly BackgroundSubtractorMOG2 model;
    private readonly Mat frame = new(Height, Width, MatType.CV_8UC1), blurred = new(), mask = new(), selected = new();
    private readonly Mat roi = new(Height, Width, MatType.CV_8UC1, Scalar.Black);
    private readonly Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(3, 3));
    private readonly int monitoredPixels;
    private TimeSpan? first, detectedSince, quietSince;
    private TimeSpan warmUntil;
    private int frames;
    private bool active;
    internal LocalMotionModel(LocalMotionSettings settings)
    {
        settings.Validate(); config = settings.Clone();
        model = BackgroundSubtractorMOG2.Create(150, 64 - 0.6 * config.Sensitivity, true);
        if (config.Zones.Count == 0) roi.SetTo(Scalar.White);
        else foreach (var zone in config.Zones)
        {
            var left = Math.Clamp((int)Math.Floor(zone.X * Width), 0, Width - 1); var top = Math.Clamp((int)Math.Floor(zone.Y * Height), 0, Height - 1);
            var right = Math.Clamp((int)Math.Ceiling((zone.X + zone.Width) * Width), left + 1, Width);
            var bottom = Math.Clamp((int)Math.Ceiling((zone.Y + zone.Height) * Height), top + 1, Height);
            Cv2.Rectangle(roi, new Rect(left, top, right - left, bottom - top), Scalar.White, -1);
        }
        monitoredPixels = Cv2.CountNonZero(roi);
    }
    internal MotionAnalysis Analyze(byte[] pixels, TimeSpan now)
    {
        if (pixels.Length != FrameBytes) throw new ArgumentException("Falsche Analysebildgröße.");
        first ??= now; frames++;
        Marshal.Copy(pixels, 0, frame.Data, pixels.Length);
        Cv2.GaussianBlur(frame, blurred, new OpenCvSharp.Size(5, 5), 0);
        model.Apply(blurred, mask, frames <= 25 ? -1 : 0.003);
        Cv2.Threshold(mask, mask, 200, 255, ThresholdTypes.Binary); // MOG2 shadow pixels are 127.
        if (frames <= 25 || now - first.Value < TimeSpan.FromSeconds(5)) return new(true, false, false, 0);
        // A near-full-frame change usually means exposure/IR/camera movement. Relearn quietly.
        if (Cv2.CountNonZero(mask) > FrameBytes * 0.85)
        {
            model.Apply(blurred, mask, 1); warmUntil = now + TimeSpan.FromSeconds(5); active = false; detectedSince = quietSince = null;
            return new(true, false, false, 0);
        }
        if (now < warmUntil) return new(true, false, false, 0);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Open, kernel);
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        Cv2.BitwiseAnd(mask, roi, selected);
        Cv2.FindContours(selected, out OpenCvSharp.Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var largest = contours.Length == 0 ? 0 : contours.Max(c => Cv2.ContourArea(c));
        var percent = 100 * largest / monitoredPixels;
        var detected = percent >= config.MinimumAreaPercent;
        var trigger = false;
        if (detected)
        {
            quietSince = null; detectedSince ??= now;
            if (!active && now - detectedSince.Value >= TimeSpan.FromSeconds(config.MinimumSeconds)) { active = true; trigger = true; }
        }
        else
        {
            detectedSince = null;
            if (active) { quietSince ??= now; if (now - quietSince.Value >= TimeSpan.FromSeconds(config.HoldSeconds)) { active = false; quietSince = null; } }
        }
        return new(false, active, trigger, percent);
    }
    public void Dispose() { model.Dispose(); frame.Dispose(); blurred.Dispose(); mask.Dispose(); selected.Dispose(); roi.Dispose(); kernel.Dispose(); }
}
internal static class LocalMotionStatus
{
    private static readonly ConcurrentDictionary<string, string> values = new();
    internal static void Set(string id, string message) => values[id] = message;
    internal static string Get(string id) => values.GetValueOrDefault(id, "Noch nicht gestartet. Änderungen mit Speichern übernehmen.");
}
#endif
