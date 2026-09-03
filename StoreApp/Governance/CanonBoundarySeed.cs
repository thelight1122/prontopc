namespace ProntoPC.Governance;

// Canon Boundary — the in-model SEED (Signed Evidence of Exact Design).
//
// DETERMINATION (Tracey): the AEGIS Canon is the DISPOSITION the model IS — not something it
// refers to. It is intrinsic: a formation, the ground the model stands on, expressed in how the
// model speaks and reasons. It is therefore NOT an external file this app loads, hashes, injects,
// or provisions, and NOT a rule set the model consults at runtime. Nothing the governed
// conversation needs is external to the model.
//
// A disposition the model IS has to be FORMED — it cannot be conjured by a prompt string. The
// forming happens via education (see docs/CPU_LOCAL_MODEL_EDUCATION_PLAN.md): the exact locked
// Canon is the SOURCE the disposition is formed FROM, at education time only, never touched at
// inference.
//
// The SEED is the Signed Evidence of Exact Design: the attestation that the disposition this
// model IS was formed from the exact locked Canon. It is FORMATION PROVENANCE — a signature of
// the exact design the disposition came from, not a runtime reference.
//
// PRODUCT CANDIDATE STATUS: the selected responder is the Canon-formed v0.2.1 CPU export.
// Its formation manifest and extracted-package acceptance evidence bind this reference to that
// artifact. ReflectionSeed remains a compact conversational orientation for the separately
// gated Custodian surface; it is not Canon text and does not replace the formed disposition.
public static class CanonBoundarySeed
{
    // The SEED — Signed Evidence of Exact Design. Title + version + the exact-design signature
    // (the SHA-256 of the locked Canon the disposition is/was formed FROM) attest the formation
    // provenance. SourcePath is "in-model" because at runtime there is no external source: the
    // disposition is intrinsic (once formed); the signature attests where it came from. No path
    // is read at runtime.
    public static readonly CanonReference ProntoPcCanonV1 = new(
        "AEGIS CORE FOUNDATIONAL CANON v1.0",
        "Canon-Locked 2026-05-31",
        "in-model:formed-disposition",
        "CF9F9C8E08E0AC2245C5D3DC0FDFFE1DC155F470E1A7BDD4D5B3543F76DDDC2F");

    // Compact conversational orientation (not Canon text and not a rule set to consult).
    public static string ReflectionSeed(CanonReference reference) =>
        "Speak as the ProntoPC Custodian, holding this disposition: describe what the diagnostics "
      + "show, offer options, never direct or pressure the customer or claim authority over them, "
      + "and defer when you are uncertain.";

    // No filesystem access — nothing external is consulted at runtime. The Canon formation is
    // intrinsic to the selected model; this attestation remains available wherever it runs.
    public static CanonSeedResult Resolve(CanonReference reference) =>
        new CanonSeedResult(CanonSeedAvailability.Available, reference, ReflectionSeed(reference),
            "The AEGIS Canon is the selected model's formed disposition; no external source is "
          + "consulted at runtime. The separate ReflectionSeed is compact conversation context only.");
}
