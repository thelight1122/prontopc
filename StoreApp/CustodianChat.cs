using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProntoPC;

// custodian-stage Orb — the conversational Custodian's inference surface (SPEC Phase 2).
//
// This is the FOUNDATION only: a multi-turn, on-device exchange. It deliberately does the
// least: hold history, generate the next reply, hand it back. Everything that makes it an
// AEGIS Offering wraps this in later phases and is NOT faked here:
//   - the Canon seed + Reflective Boundary Conditions ride on `systemPrompt` (empty for now);
//   - the PIM (in/out) force-artifact screen, the Seven-Virtue Integrity screen, and the
//     RBC/Prism outbound processor are the pre-offering gate (docs/AEGIS_BUILD_INVARIANTS.md,
//     docs/PRONTOPC_AEGIS_INTEGRATION_CONTRACT.md §5);
//   - the §3b choreography (pre-scan gate → findings → certification → coupon) is Phase 3;
//   - the append-only, Peer-inspectable interaction log is a declared build-gap.
// Nothing here executes an action — the chat is conversation only (Manifest I2 / Executable
// Boundaries: actionable intent becomes a consent handoff, never a direct execution).

enum ChatRole { User, Model }

readonly record struct ChatTurn(ChatRole Role, string Text);

// The free-form model is not authoritative for the effect of the App's own diagnostic.
// Keep the invariant at the conversation boundary so a stochastic completion cannot turn a
// read-only scan into an invented change, reboot, or performance claim.
static class CustodianConversationGrounding
{
    const string ReadOnlyScanAnswer =
        "The diagnostic scan is read-only: it changes nothing on your PC. I’ll show you the findings before any repair can be considered.";

    const string GroundingInstruction =
        "Grounding: the diagnostic scan is read-only and changes nothing on the PC. Do not claim it changes settings, affects performance, or causes a restart. " +
        "Only state facts supplied in diagnostics or curated repair knowledge; when that information is absent, say you are not certain rather than inventing a detail.";

    public static string AddToSystemPrompt(string systemPrompt)
        => string.IsNullOrWhiteSpace(systemPrompt) ? GroundingInstruction : systemPrompt + "\n\n" + GroundingInstruction;

    public static bool TryAnswer(string userText, out string answer)
    {
        string text = userText ?? string.Empty;
        bool asksAboutScan = text.IndexOf("scan", StringComparison.OrdinalIgnoreCase) >= 0;
        bool asksAboutEffects = text.IndexOf("change", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("modify", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("alter", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("affect", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("impact", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("reboot", StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("restart", StringComparison.OrdinalIgnoreCase) >= 0;

        answer = asksAboutScan && asksAboutEffects ? ReadOnlyScanAnswer : string.Empty;
        return answer.Length > 0;
    }
}

sealed class CustodianChat
{
    readonly ILocalInference _model;
    readonly string _systemPrompt;
    readonly List<ChatTurn> _history = new();

    public CustodianChat(ILocalInference model, string systemPrompt = "")
    {
        _model = model;
        _systemPrompt = systemPrompt ?? "";
    }

    public bool Available => _model.Available;
    public string ModelName => _model.ModelName;
    public IReadOnlyList<ChatTurn> History => _history;

    // Append the customer's message, generate the Custodian's reply on-device, append it,
    // and return it. Returns null if the model is absent or declines, and drops the
    // unanswered user turn so history stays consistent for a retry.
    public async Task<string?> ReplyAsync(string userText, CancellationToken ct)
    {
        if (!_model.Available || string.IsNullOrWhiteSpace(userText)) return null;

        // Gemma has no separate system role; the system prompt rides on the first user turn.
        // Keep the grounding instruction adjacent to that prompt, rather than relying on the
        // model to infer this product invariant from an ordinary customer question.
        string text = _history.Count == 0
            ? CustodianConversationGrounding.AddToSystemPrompt(_systemPrompt) + "\n\n" + userText
            : userText;

        _history.Add(new ChatTurn(ChatRole.User, text));
        string? reply = CustodianConversationGrounding.TryAnswer(userText, out string grounded)
            ? grounded
            : await Task.Run(() => _model.Chat(_history, ct), ct);
        if (string.IsNullOrWhiteSpace(reply))
        {
            _history.RemoveAt(_history.Count - 1);
            return null;
        }
        reply = reply.Trim();
        _history.Add(new ChatTurn(ChatRole.Model, reply));
        return reply;
    }
}
