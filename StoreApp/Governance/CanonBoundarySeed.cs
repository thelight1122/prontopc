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
// HONEST INTERIM STATUS: the shipped model is not yet educated on the Canon, so it does not yet
// HAVE this disposition — it has SCAFFOLDING. ReflectionSeed below is that scaffolding: a short
// posture orientation that asks the model to speak with the disposition, held only until the
// disposition is genuinely formed in the model itself and passes evaluation. Until then we do
// not claim the model IS the disposition; the seed does not assert a foundation it lacks.
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

    // INTERIM SCAFFOLDING (not the Canon text, not a rule set to consult). Once the disposition
    // is formed in the model, this posture is subsumed by what the model simply IS. Until then it
    // states the disposition to hold WITHOUT claiming the model already has a Canon foundation —
    // an honest ask, not an assertion of a formation that has not run.
    public static string ReflectionSeed(CanonReference reference) =>
        "Speak as the ProntoPC Custodian, holding this disposition: describe what the diagnostics "
      + "show, offer options, never direct or pressure the customer or claim authority over them, "
      + "and defer when you are uncertain.";

    // No filesystem access — nothing external is consulted at runtime. The disposition is meant to
    // be intrinsic; today the interim scaffolding stands in until formation. Either way the seed is
    // always available wherever the model runs.
    public static CanonSeedResult Resolve(CanonReference reference) =>
        new CanonSeedResult(CanonSeedAvailability.Available, reference, ReflectionSeed(reference),
            "The AEGIS Canon is the model's disposition (in-model, formed); no external source is "
          + "consulted at runtime. Interim: scaffolding stands in until the disposition is formed.");
}
