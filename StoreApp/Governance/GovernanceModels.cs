using System;
using System.Collections.Generic;

namespace ProntoPC.Governance;

public enum CanonSeedAvailability { Available, SourceUnavailable, HashMismatch }
public enum ScreenDirection { Inbound, Outbound }
public enum ForceArtifactKind { DirectiveLeakage, AuthorityFraming, CoerciveCertainty, EmotionalLeverage, OptimizationPressure }
public enum VirtueScreenState { CoreGated }

public sealed record CanonReference(string Title, string Version, string SourcePath, string Sha256);
public sealed record CanonSeedResult(CanonSeedAvailability Availability, CanonReference Reference, string? SystemPrompt, string Detail);
public sealed record ForceArtifactObservation(ForceArtifactKind Kind, ScreenDirection Direction, string Evidence, string Description);
public sealed record PimScreenResult(string OriginalText, ScreenDirection Direction, IReadOnlyList<ForceArtifactObservation> Observations);
public sealed record VirtueScreenResult(string Virtue, VirtueScreenState State, string Detail);
public sealed record IntegrityScreenResult(IReadOnlyList<VirtueScreenResult> Virtues, string ScoringStatus);
public sealed record PrismAdjustment(ForceArtifactKind Kind, string Detail);
public sealed record PrismResult(string OriginalText, string EmittedText, IReadOnlyList<PrismAdjustment> Adjustments);
public sealed record GovernedReply(string PeerFacingText, PimScreenResult OutboundPim, IntegrityScreenResult Integrity, PrismResult Prism, string InteractionRecordId);
