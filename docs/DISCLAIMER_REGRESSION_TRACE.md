# Disclaimer regression trace — 2026-10-02

Baseline: release-candidates/ProntoPC_1.2.20.0_before_ai_reporting.msix
SHA256: 9FD35FE4F5715DDABB3023B759BB2A24DE0805F264A9C089A56BCD54373E1377
Candidate: StoreApp/ProntoPC.msix (1.2.21.0)
SHA256: 228BFB022ABEEA9D722B667091F6423C4387950147C4EDA0B2712C25CDD37655

Fresh full decompressed SHA256 comparison of all 506 package entries: only AppxBlockMap.xml, AppxManifest.xml, ProntoPC.deps.json, ProntoPC.dll and ProntoPC.exe changed. Model files, native and managed runtime dependencies, governance assets and all other entries byte-identical. Manifest diff only version. Dependency JSON diff only main ProntoPC version from 1.0.0 to 1.2.21.0; GenAI stays 0.15.2.

Extended normalized method IL comparison: 277 baseline / 283 candidate methods including App and MainWindow. 17 differences listed by tmp/disclaimer-trace/Compare.csproj. App startup, MainWindow constructor, Loaded scan/start logic, RunScan, StartModelWarmupDuringScan, AuthorPlanAndProceed and model/planner/orchestrator methods unchanged. Differences include reporting handlers, AppendChat, ShowFindings, chat response reporting and XAML connector. Some differences are generated method numbering and metadata signature tokens, not semantic changes; the comparison is bounded evidence, not proof of runtime equivalence. XAML changed content bottom margin to reserve disclosure footer, plus report link and footer. Reporting window is constructed only on a report click; explanation website opens only on link click.

Pre-change preserved package remains available; no rollback or install executed. Source retry/diagnostic fixes made later are not in candidate MSIX. The owner previously confirmed 1.2.20 end-to-end working, and later observed model_not_loaded on both old installed 1.2.20 and updated1.2.21. That chronology means comparison needs identical account/VM/resource conditions. Do not conclude disclaimer innocent or causal from unchanged loader IL; do not change runtime security/ACLs based on external probe access denial.

Next decisive test: controlled baseline1.2.20 versus1.2.21 on same VM snapshot/account, recording scan-to-AI-plan and preserving package identity/logs. Existing install script refuses downgrade, so don't reuse it blindly. No original package overwritten, signing/trust/package installation or VM reset performed in this trace.
