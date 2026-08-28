using System;
using System.IO;
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
    bool _loadFailed;

    OnnxGenAiInference(string dir, string modelName) { _dir = dir; _modelName = modelName; }

    // A GenAI model folder is identified by its genai_config.json. Actual weights load
    // lazily on first use (off the UI thread), so this stays cheap.
    public static ILocalInference CreateOrAbsent(string dir, string modelName)
        => File.Exists(Path.Combine(dir, "genai_config.json"))
           ? new OnnxGenAiInference(dir, modelName)
           : new AbsentInference();

    public bool Available => !_loadFailed && File.Exists(Path.Combine(_dir, "genai_config.json"));
    public string ModelName => _modelName;

    public string? Complete(string prompt, string jsonSchema, CancellationToken ct)
    {
        try
        {
            EnsureLoaded();
            if (_model is null || _tokenizer is null) return null;

            var sequences = _tokenizer.Encode(prompt);
            var inputIds = sequences[0];
            int inputLen = inputIds.Length;

            using var gp = new GeneratorParams(_model);
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
            while (!generator.IsDone())
            {
                ct.ThrowIfCancellationRequested();
                generator.GenerateNextToken();
            }

            // Decode only the generated tail, not the echoed prompt.
            var full = generator.GetSequence(0);
            var generated = full.Slice(inputLen);
            return _tokenizer.Decode(generated);
        }
        catch { return null; }
    }

    void EnsureLoaded()
    {
        if (_model != null || _loadFailed) return;
        lock (_lock)
        {
            if (_model != null || _loadFailed) return;
            try { _model = new Model(_dir); _tokenizer = new Tokenizer(_model); }
            catch { _loadFailed = true; _model = null; _tokenizer = null; }
        }
    }
}
