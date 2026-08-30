# Custodian Governance Module Set

This is a separate, buildable governance layer for the ProntoPC Custodian. It implements the buildable
elements of the filed AEGIS pre-offering gate without wiring a peer-facing conversation into the application.

- `CanonBoundarySeed` reads only an exact, hash-matching external Canon source. It neither copies nor maintains
  an app-owned Canon. If the declared source is unavailable, it produces no seeded prompt and reports that state.
- `ForceArtifactScreen` is PIM for inbound and outbound text. It observes directive leakage, authority framing,
  coercive certainty, emotional leverage, and optimization pressure without discarding the input.
- `SevenVirtueScreen` returns all seven binary-screen placeholders with the explicit status `CoreGated`; it does
  not invent IRG scoring.
- `ReflectiveBoundaryPrism` shapes peer-facing expression only. When outbound observations exist, it provides a
  proportional optional reflection while retaining the raw response for the interaction ledger.
- `AppendOnlyInteractionLedger` is separate from the Totem development ledger. It writes timestamped JSONL records
  with a SHA-256 predecessor chain and exposes verified reads for Peer inspection.
- `CustodianGovernancePipeline` is the future integration seam: PIM inbound → model → PIM outbound → Integrity →
  RBC/Prism → interaction ledger.

The later integration is intentionally limited to `CustodianChat`; it is outside this module-only change and
remains gated until a runtime Canon source and the peer-facing path are explicitly approved.
