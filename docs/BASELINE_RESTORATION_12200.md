# ProntoPC baseline restoration — 2026-10-02

User requested preservation of reporting/disclaimer enhancements and restoration of the pre-change app. Reversed the successful app/source edit patches from this conversation, including UI reporting, version changes, build upload change, later retry/loader instrumentation, PodEval additions and certification preflight change. Unrelated preexisting changes preserved. Added reporting source and tests archived. Existing public prontopc.online explanation page remains available; no new website changes.

Archived sources and 1.2.21 package/upload: release-candidates/disclaimer-enhancements-20261002.
Restored StoreApp/ProntoPC.msix: original1.2.20 bytes, SHA2569FD35FE4F5715DDABB3023B759BB2A24DE0805F264A9C089A56BCD54373E1377.
Restored StoreApp/ProntoPC.msixupload: SHA256F4B56BA2E8DCE0F898AE569A8FA947606D3344B2EE8A865593FE7FE34C272EA3.
Package layout extracted from original1.2.20. Current WACK restored from preserved1.2.20 report;1.2.21 reports preserved.
Release source build PASS,0warnings/errors. Extended normalized method comparison277baseline/277restored:4reported differences limited to raw metadata tokens in generated ElevatedRepairRequest overload signatures; other normalized method bodies match. This is not guest runtime acceptance.

Existing previously signed1.2.20 test package payload matches original except signing content-types metadata plus signature/catalog entries; no signing performed. Signed SHA256C206BCEF78801984D5D6A1DA829DA15251A32FAE725B1FD3946B0A0D730F218E. Copied to tests/CertVM/ProntoPC-12200-test.msix. Guest restore-12200-current-user.ps1 syntax PASS; pins hash, requires app closed, uses Add-AppxPackage -ForceUpdateFromAnyVersion as explicitly requested rollback, verifies installed1.2.20. Does not remove app/data, reset VM, create account, sign or alter trust. Owner execution still required; guest currently last confirmed1.2.21. No new WACK or submission performed. Baseline reacceptance scan->genuine AI plan remains pending.

Historical certification notes below other documents describe previous state and are superseded by this restoration until guest runtime reacceptance. Restoration does not address Microsoft11.16 reporting rejection; separate minimal disclosure/reporting approach follows working-baseline confirmation.
