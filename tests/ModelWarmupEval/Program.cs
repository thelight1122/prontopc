using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ProntoPC;

static void Check(bool ok, string scenario)
{
    if (!ok) throw new Exception("FAIL: " + scenario);
    Console.WriteLine("PASS: " + scenario);
}

var missing = await ModelWarmup.PrepareAsync(new AbsentInference(), null, CancellationToken.None);
Check(!missing.IsReady && missing.Attempts == 0, "missing model never reports ready");

var attempts = new List<int>();
var recoverable = new ProbeInference(1);
var recovered = await ModelWarmup.PrepareAsync(recoverable, new InlineProgress(attempts), CancellationToken.None);
Check(recovered.IsReady && recovered.Attempts == 2 && recoverable.Calls == 2 && attempts.Count == 2,
    "transient load failure is retried inside preparation before ready");

var failed = new ProbeInference(99);
var failure = await ModelWarmup.PrepareAsync(failed, null, CancellationToken.None);
Check(!failure.IsReady && failure.Attempts == 3 && failed.Calls == 3 && failure.Reason.Contains("free memory"),
    "persistent memory failure ends truthfully after exactly three attempts");

var slow = new ProbeInference(99);
using var cancel = new CancellationTokenSource();
var preparation = ModelWarmup.PrepareAsync(slow, null, cancel.Token);
cancel.CancelAfter(100);
try { await preparation; throw new Exception("FAIL: cancellation ignored"); }
catch (OperationCanceledException) { Check(slow.Calls <= 1, "cancellation prevents another retry"); }

sealed class InlineProgress(List<int> attempts) : IProgress<int>
{
    public void Report(int attempt) => attempts.Add(attempt);
}

sealed class ProbeInference(int failures) : ILocalInference
{
    public int Calls;
    public bool Available => true;
    public string ModelName => "test";
    public void WarmUp(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (++Calls <= failures) throw new OutOfMemoryException("Synthetic load pressure");
    }
    public string? Complete(string prompt, string schema, CancellationToken ct) => throw new Exception("Preparation must not generate a plan");
    public string? Chat(IReadOnlyList<ChatTurn> history, CancellationToken ct) => throw new Exception("Preparation must not chat");
}
