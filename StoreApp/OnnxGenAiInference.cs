using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace ProntoPC;

// Stage 2b: the real on-device inference for the local-model Satellite, using ONNX
// Runtime GenAI. Loads a bundled Gemma (GenAI format: genai_config.json + model.onnx +
// tokenizer) and generates the plan with json_schema-guided decoding so the output is
// valid JSON restricted to the admissible action ids. Lazy-loaded off the UI thread.
//
// If no model is bundled, CreateOrAbsent returns AbsentInference, so the app runs
// unchanged (Satellite declines -> cloud broker authors).
sealed class OnnxGenAiInference : ILocalInference
{
    readonly string _dir;
    readonly string _modelName;
    readonly object _lock = new();
    Model? _model;
    Tokenizer? _tokenizer;
    DateTime _lastLoadFailureUtc = DateTime.MinValue;

    OnnxGenAiInference(string dir, string modelName) { _dir = dir; _modelName = modelName; }

    // Additive, behavior-preserving instrumentation of the LAST generation on this instance
    // (Phase 2 physical-budget observation — TOTEM-C-0081 "next"). These record only what the
    // decode loop already did; they change no search option, no template, no output. Read them
    // right after a Complete/Chat call. LastGenerateMs is decode-only wall time (excludes the
    // one-time model load), so LastGeneratedTokens / (LastGenerateMs/1000) is a fair tok/s.
    public int LastInputTokens { get; private set; }
    public int LastGeneratedTokens { get; private set; }
    public long LastGenerateMs { get; private set; }

    // A GenAI model folder is identified by its genai_config.json. Actual weights load
    // lazily on first use (off the UI thread), so this stays cheap.
    public static ILocalInference CreateOrAbsent(string dir, string modelName)
        => File.Exists(Path.Combine(dir, "genai_config.json"))
           ? new OnnxGenAiInference(dir, modelName)
           : new AbsentInference();

    public bool Available => File.Exists(Path.Combine(_dir, "genai_config.json"));
    public string ModelName => _modelName;

    public string? Complete(string prompt, string jsonSchema, CancellationToken ct)
    {
        try
        {
            EnsureLoaded();
            if (_model is null || _tokenizer is null) return null;

            // Gemma is instruction-tuned and expects its chat template; without the
            // <start_of_turn> framing the raw prompt yields garbage. ORT GenAI's Encode
            // does not apply the template, so we wrap it here.
            string wrapped = $"<start_of_turn>user\n{prompt}<end_of_turn>\n<start_of_turn>model\n";
            var sequences = _tokenizer.Encode(wrapped);
            var inputIds = sequences[0];
            int inputLen = inputIds.Length;

            using var gp = new GeneratorParams(_model);
            // A repair plan is structured output, not a creative response.  The bundled
            // GenAI configuration defaults to sampling; if guidance is unavailable on a
            // particular runtime, sampling can occasionally produce prose or malformed
            // JSON.  That makes the planner decline and, when the optional cloud fallback
            // is also unavailable, sends a paid customer to make-good.  Greedy decoding
            // keeps the local plan repeatable and parseable on every supported device.
            gp.SetSearchOption("do_sample", false);
            gp.SetSearchOption("max_length", (double)(inputLen + 1024));
            if (!string.IsNullOrEmpty(jsonSchema))
            {
                // Guided decoding: constrain the output to the JSON schema (which pins
                // action.id to the admissible enum). Best-effort — if the build lacks
                // guidance support, fall back to free generation; Envelope.Validate is
                // still the backstop downstream.
                try { gp.SetGuidance("json_schema", jsonSchema); } catch { }
            }

            using var generator = new Generator(_model, gp);
            generator.AppendTokens(inputIds);
            var swGen = Stopwatch.StartNew();
            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                generator.GenerateNextToken();
            }
            swGen.Stop();

            // Decode only the generated tail, not the echoed prompt.
            var full = generator.GetSequence(0);
            var generated = full.Slice(inputLen);
            LastInputTokens = inputLen;
            LastGeneratedTokens = generated.Length;
            LastGenerateMs = swGen.ElapsedMilliseconds;
            return _tokenizer.Decode(generated);
        }
        catch { return null; }
    }

    // Multi-turn conversational reply (SPEC Phase 2). Renders the whole history through
    // Gemma's chat template and opens a model turn. Free-form: no guided decoding.
    public string? Chat(IReadOnlyList<ChatTurn> history, CancellationToken ct)
    {
        try
        {
            EnsureLoaded();
            if (_model is null || _tokenizer is null || history is null || history.Count == 0) return null;

            // Gemma's chat template BEGINS with <bos> (bos_token_id=2). ORT GenAI's Encode
            // does not prepend it, and without it the instruction-tuned 1B degrades into
            // echoing the prompt back instead of answering (the TOTEM-0026 issue). Emit it
            // as the literal special token, exactly as the <start_of_turn> markers are.
            var sb = new StringBuilder();
            sb.Append("<bos>");
            foreach (var t in history)
                sb.Append("<start_of_turn>").Append(t.Role == ChatRole.User ? "user" : "model")
                  .Append('\n').Append(t.Text).Append("<end_of_turn>\n");
            sb.Append("<start_of_turn>model\n");

            var sequences = _tokenizer.Encode(sb.ToString());
            var inputIds = sequences[0];
            int inputLen = inputIds.Length;

            // Free-form decode under the Conversation Contract's interactive_params
            // (temperature 0.5, top_p 0.9, max_new_tokens 512) — not the genai_config
            // default (temperature 1.0), which over-randomises the reply.
            using var gp = new GeneratorParams(_model);
            gp.SetSearchOption("max_length", (double)(inputLen + 512));
            gp.SetSearchOption("do_sample", true);
            gp.SetSearchOption("temperature", 0.5);
            gp.SetSearchOption("top_p", 0.9);

            using var generator = new Generator(_model, gp);
            generator.AppendTokens(inputIds);
            var swGen = Stopwatch.StartNew();
            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                generator.GenerateNextToken();
            }
            swGen.Stop();

            var full = generator.GetSequence(0);
            var generated = full.Slice(inputLen);
            LastInputTokens = inputLen;
            LastGeneratedTokens = generated.Length;
            LastGenerateMs = swGen.ElapsedMilliseconds;
            string reply = _tokenizer.Decode(generated);
            // Trim any trailing turn marker the model may emit after its reply.
            int cut = reply.IndexOf("<end_of_turn>", StringComparison.Ordinal);
            if (cut >= 0) reply = reply.Substring(0, cut);
            return reply.Trim();
        }
        catch { return null; }
    }

    void EnsureLoaded()
    {
        if (_model != null) return;
        lock (_lock)
        {
            if (_model != null) return;

            // A first load can fail under temporary memory pressure. Do not mark the
            // bundled model permanently unavailable; the retry UI asks the customer to
            // close heavy apps, and the next attempt must be able to load again.
            if ((DateTime.UtcNow - _lastLoadFailureUtc) < TimeSpan.FromSeconds(2)) return;
            try { GC.Collect(); GC.WaitForPendingFinalizers(); _model = new Model(_dir); _tokenizer = new Tokenizer(_model); }
            catch
            {
                _lastLoadFailureUtc = DateTime.UtcNow;
                _model?.Dispose();
                _model = null;
                _tokenizer = null;
                GC.Collect();
            }
        }
    }
}
