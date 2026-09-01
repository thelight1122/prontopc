namespace ProntoPC.Governance;

// Canon Boundary — the in-model SEED (Signed Evidence of Exact Design).
//
// The AEGIS Canon and the model are not separate things: the Canon is the basis the model's
// thoughts reflect on — its embedded foundation, the ground it stands on (educated into the
// weights). It is therefore NOT an external file this app loads, hashes, injects, or
// provisions. Nothing the governed conversation needs is external to the model; everything
// is locally available to the model.
//
// The SEED is the Signed Evidence of Exact Design: the attestation, at education time, of the
// exact Canon the model was formed on. That evidence is provenance — a signature of the exact
// design, not a runtime file to read. This module consults no filesystem; it carries the SEED
// provenance and supplies a light REFLECTIVE-POSTURE orientation for the system prompt (it
// does not carry the Canon text, because the model already reflects from the Canon as its own
// basis).
public static class CanonBoundarySeed
{
    // The SEED — Signed Evidence of Exact Design. Title + version + the exact-design signature
    // (the SHA-256 of the Canon the model was educated on) attest what foundation the model
    // stands on. SourcePath is "in-model" because there is no external source: the design was
    // signed at education time, and the model carries it. No path is read at runtime.
    public static readonly CanonReference ProntoPcCanonV1 = new(
        "AEGIS CORE FOUNDATIONAL CANON v1.0",
        "Canon-Locked 2026-05-31",
        "in-model:embedded-foundation",
        "CF9F9C8E08E0AC2245C5D3DC0FDFFE1DC155F470E1A7BDD4D5B3543F76DDDC2F");

    // The reflective-posture seed for the system prompt. This is NOT the Canon text and NOT
    // a rule set to consult: the model already reflects from its Canon foundation. It only
    // orients that reflection — non-force expression, describe-and-offer, defer when uncertain.
    public static string ReflectionSeed(CanonReference reference) =>
        "You reflect from your AEGIS Canon foundation — the basis of your own thought, not an "
      + "external rule set handed to you. Speak as the ProntoPC Custodian: describe what the "
      + "diagnostics show, offer options, never direct or pressure the customer or claim authority "
      + "over them, and defer when you are uncertain.";

    // The Canon is intrinsic to the model, so the seed is always available wherever the model
    // runs. No filesystem access — nothing external is consulted.
    public static CanonSeedResult Resolve(CanonReference reference) =>
        new CanonSeedResult(CanonSeedAvailability.Available, reference, ReflectionSeed(reference),
            "The AEGIS Canon is the model's embedded foundation (in-model); no external source is consulted.");
}
