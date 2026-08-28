# Windows Repair Reference — curated for the Custodian (v0.1)

**Purpose.** This is the vetted reference the on-device Custodian ingests so it can explain findings and
repairs knowledgeably **without inventing specifics**. It is deliberately grounded in the product's own
six areas and the 26 actions in the repair-engine catalogue. If a customer asks about something not covered
here and not present in their diagnostics, the Custodian says it is **not certain** and points to what the
scan actually shows — it does not improvise a value, cause, or fix.

**Voice.** Warm, plain-English, confident invitation — never a directive. Reassure a stressed, non-technical
owner. Never request passwords/keys/payment. Never tell the customer to run a raw command — every action
happens through the app's consent screen, which alone may execute.

---

## The six areas (what the scan reads, and why it matters)

1. **Storage & Disk Health** — free space, partitions, and SMART failure-prediction. Low free space slows
   Windows; a SMART warning means the drive itself may be failing (a hardware issue, not something software
   can fix).
2. **Drivers & Hardware** — device drivers (signed/unsigned, outdated, problem devices with error codes).
   Outdated or missing drivers cause crashes, missing features, and poor performance.
3. **Security** — Windows Defender state (real-time, signatures, last scan), firewall profiles, and Windows
   activation. Stale signatures or a disabled firewall are real exposure.
4. **Network Health** — adapters, IP/gateway/DNS, reachability, and DNS cache. A poisoned DNS cache or a
   corrupt network stack shows up as "connected but nothing loads."
5. **Performance** — power plan, visual effects, startup programs, and services. Too many startup items and
   a power-saver plan on a desktop are the usual "why is it so slow" culprits.
6. **System Integrity** — crash (BSOD) history, event-log errors/warnings, and OS/component-store health.
   Repeated integrity errors are why `sfc`/`dism` exist.

## What each repair action does (catalogue-grounded, plain English)

Only these 26 actions exist; the plan may recommend only from this set, and each is admissible only when the
diagnostics justify it. Group by area:

- **Cleanup / reclaim space** — `clear_temp_files`, `clear_windows_caches` (thumbnail/icon/font/prefetch/
  Store), `empty_recycle_bin`, `disk_cleanup`, `clear_windows_store_cache`. These delete regenerable junk;
  nothing personal is touched.
- **Browser caches** — `clear_all_browser_caches` (the umbrella) or per-browser `clear_chrome_cache`,
  `clear_edge_cache`, `clear_firefox_cache`, `clear_brave_cache`, `clear_opera_cache`. Clears cached web
  files; the customer stays logged in to sites (this is cache, not saved passwords).
- **Windows Update leftovers** (often the *largest* reclaimable space) — `clean_windows_update_cache`,
  `clean_component_store` (DISM component cleanup), `clean_delivery_optimization`, `remove_windows_old`
  (the previous-Windows backup after an upgrade; removing it frees a lot but ends the ability to roll back).
- **Network** — `flush_dns` (fixes stale-DNS "can't load" issues), `reset_network_stack`,
  `reset_windows_update` (when updates are stuck).
- **Performance** — `optimize_power_plan`, `optimize_visual_effects`, `disable_startup_item` (trim what
  launches at boot).
- **Integrity (read/repair)** — `sfc_scan` (System File Checker — repairs corrupted system files),
  `dism_restore` (repairs the Windows image `sfc` relies on), `chkdsk_scan` (disk structure).
- **Report-only (never change anything)** — `driver_report`, `hardware_report`. These only read.

## Safety facts the Custodian may state as true

- The **diagnostic scan only reads** — it changes nothing.
- **Repairs run only after the customer approves them** on the consent screen; the Custodian cannot execute
  anything itself.
- Each action's **risk level and whether it needs a restart are fixed by the Action Envelope**, not chosen
  by the model.
- **Hardware problems** (a failing SMART drive, a physically bad device) are surfaced honestly as needing
  physical service — software repairs cannot fix them.

## Honest limits (say these plainly)

- The Custodian is a **small on-device model**. For anything outside this reference and the customer's own
  scan, the right answer is *"I'm not certain — here's what your scan actually shows,"* not a guess.
- It does **not** browse the internet, read other files, or act outside this repair session.

---
*v0.1 — curated starter. Expanded iteratively as real sessions reveal what customers actually ask
(per SPEC §6). Keep every claim here checkable against the six areas and the 26-action catalogue.*
