using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ProntoPC;

sealed record ModelWarmupResult(bool IsReady, string Reason, long DurationMs, int Attempts);

// A completed Task is the prerequisite for plan generation; retries stay inside preparation.
static class ModelWarmup
{
    public static async Task<ModelWarmupResult> PrepareAsync(ILocalInference inference,
        IProgress<int>? progress, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        if (!inference.Available)
        {
            Record("model_files_unavailable", 0, timer.ElapsedMilliseconds, null);
            return new(false, "The on-device AI model files could not be found. Please contact support@prontopc.online.", timer.ElapsedMilliseconds, 0);
        }
        Exception? failure = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(attempt);
            try
            {
                await Task.Run(() => inference.WarmUp(ct), ct);
                Record("model_ready", attempt, timer.ElapsedMilliseconds, null);
                return new(true, "", timer.ElapsedMilliseconds, attempt);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failure = ex;
                Record("model_load_failed", attempt, timer.ElapsedMilliseconds, ex);
                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(3), ct);
            }
        }
        string reason = failure is OutOfMemoryException
            ? "There isn't enough free memory to prepare the on-device AI. Close other apps and reopen ProntoPC."
            : failure is IOException or UnauthorizedAccessException
                ? "ProntoPC couldn't read the on-device AI model files. Please contact support@prontopc.online."
                : "The on-device AI could not start after three preparation attempts. Please contact support@prontopc.online.";
        return new(false, reason, timer.ElapsedMilliseconds, 3);
    }

    static void Record(string state, int attempt, long durationMs, Exception? error)
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ProntoPC", "logs");
            Directory.CreateDirectory(dir);
            string detail = error is null ? "" : $"|exception={error.GetType().FullName}|hresult=0x{error.HResult:X8}|message={error.Message.Replace('\r', ' ').Replace('\n', ' ')}";
            File.AppendAllText(Path.Combine(dir, "plan-authoring.log"),
                $"{DateTimeOffset.UtcNow:O}|version={typeof(ModelWarmup).Assembly.GetName().Version}|phase=model_preparation|state={state}|attempt={attempt}|duration_ms={durationMs}{detail}{Environment.NewLine}");
        }
        catch { /* A log write failure must not prevent loading the model. */ }
    }
}
