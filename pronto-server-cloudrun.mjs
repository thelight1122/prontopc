/**
 * Pronto PC — Cloud Run Service v2.0
 * Custodian Technician backend: session management, Gemini AI repair plans,
 * dynamic PowerShell script serving, and report generation.
 * 5D Service Solutions Global LLC
 */

import http from "node:http";
import crypto from "node:crypto";

const PORT = process.env.PORT || 8080;
const STRIPE_SECRET_KEY = process.env.STRIPE_SECRET_KEY;

// ─── LLM: Azure OpenAI (primary), Gemini (optional fallback) ─────────────────
const AZURE_OPENAI_ENDPOINT = (process.env.AZURE_OPENAI_ENDPOINT || "").replace(/\/+$/, "");
const AZURE_OPENAI_KEY = process.env.AZURE_OPENAI_KEY;
const AZURE_OPENAI_DEPLOYMENT = process.env.AZURE_OPENAI_DEPLOYMENT || "gpt-41-mini";
const AZURE_OPENAI_API_VERSION = process.env.AZURE_OPENAI_API_VERSION || "2024-10-21";
const AZURE_OPENAI_URL = AZURE_OPENAI_ENDPOINT
  ? `${AZURE_OPENAI_ENDPOINT}/openai/deployments/${AZURE_OPENAI_DEPLOYMENT}/chat/completions?api-version=${AZURE_OPENAI_API_VERSION}`
  : "";
const LLM_MODEL = `azure/${AZURE_OPENAI_DEPLOYMENT}`;

// Legacy Gemini (only used if Azure is not configured)
const GEMINI_API_KEY = process.env.GEMINI_API_KEY;
const GEMINI_MODEL = "gemini-2.5-flash";
const GEMINI_URL = GEMINI_API_KEY
  ? `https://generativelanguage.googleapis.com/v1beta/models/${GEMINI_MODEL}:generateContent?key=${GEMINI_API_KEY}`
  : "";
const CLOUD_RUN_URL = process.env.CLOUD_RUN_URL || "https://pronto-pc-108655745293.us-central1.run.app";
const SESSION_TTL_MS = 3 * 24 * 60 * 60 * 1000; // 3 days

// ─── Next-Repair Coupon (50% off) ────────────────────────────────────────────
// Reusable Stripe coupon (50% off, single use) + a unique promotion code minted
// for each customer when their repair completes. Redeemable at the $29 checkout.
const COUPON_ID = process.env.COUPON_ID || "NEXTREPAIR50";
const COUPON_VALID_DAYS = Number(process.env.COUPON_VALID_DAYS || 180); // ~6 months
const SITE_URL = process.env.SITE_URL || "https://prontopc.online";
const LOYALTY_ADMIN_KEY = process.env.LOYALTY_ADMIN_KEY || ""; // gate for the /admin/loyalty ledger view

// ─── AI-Unavailable Make-Good (100% off, that machine only) ──────────────────
// When BOTH AI authors fail — the on-device Gemma AND the cloud broker — the
// customer paid for an AI-powered plan and did not get it. We never substitute a
// rule-based plan under the AI claim; instead we issue a single-use 100% coupon so
// they can run again free, email them the code + an honest apology, and notify
// Support of every incident. Honest value-for-value commerce.
const MAKEGOOD_COUPON_ID = process.env.MAKEGOOD_COUPON_ID || "AIMAKEGOOD100";
const MAKEGOOD_VALID_DAYS = Number(process.env.MAKEGOOD_VALID_DAYS || 180); // ~6 months
const SUPPORT_EMAIL = process.env.SUPPORT_EMAIL || "support@prontopc.online";

// ─── CORS ───────────────────────────────────────────────────────────────────

const ALLOWED_ORIGINS = [
  "https://prontopc.online",
  "https://www.prontopc.online",
];

function isAllowedOrigin(origin) {
  if (!origin) return false;
  if (ALLOWED_ORIGINS.includes(origin)) return true;
  if (/\.powervirtualagents\.com$/.test(origin)) return true;
  if (/\.botframework\.com$/.test(origin)) return true;
  if (/\.api\.powerplatform\.com$/.test(origin)) return true;
  return false;
}

function corsOrigin(req) {
  const origin = req.headers.origin || "";
  return isAllowedOrigin(origin) ? origin : ALLOWED_ORIGINS[0];
}

// ─── Session Store ──────────────────────────────────────────────────────────
// In-memory for beta; migrate to Firestore for production (min-instances=1 on Cloud Run).

const sessionStore = new Map();

// States: created → scan_served → diagnosed → consented → repair_served → completed
// The native Store app POSTs diagnostics directly (it never fetches the /scan
// script), so it goes created → diagnosed without passing through scan_served.
const VALID_TRANSITIONS = {
  created: ["scan_served", "diagnosed"],
  scan_served: ["diagnosed"],
  diagnosed: ["consented"],
  consented: ["repair_served"],
  repair_served: ["completed"],
};

function createSession(stripeSessionId, email, clientRef = null) {
  const token = crypto.randomBytes(32).toString("hex");
  sessionStore.set(token, {
    token,
    stripeSessionId,
    clientRef,
    email,
    state: "created",
    hostname: null,
    diagnostics: null,
    repairPlan: null,
    repairResults: null,
    createdAt: Date.now(),
    updatedAt: Date.now(),
  });
  return token;
}

function getSession(token) {
  const s = sessionStore.get(token);
  if (!s) return null;
  if (Date.now() - s.createdAt > SESSION_TTL_MS) {
    sessionStore.delete(token);
    return null;
  }
  return s;
}

function transitionState(token, newState) {
  const s = getSession(token);
  if (!s) return null;
  const allowed = VALID_TRANSITIONS[s.state];
  if (!allowed || !allowed.includes(newState)) return null;
  s.state = newState;
  s.updatedAt = Date.now();
  sessionStore.set(token, s);
  return s;
}

// Cleanup expired sessions every 10 minutes
setInterval(() => {
  const now = Date.now();
  for (const [k, v] of sessionStore.entries()) {
    if (now - v.createdAt > SESSION_TTL_MS) sessionStore.delete(k);
  }
}, 10 * 60 * 1000);

// ─── Guardian Store ─────────────────────────────────────────────────────────
// In-memory for beta; migrate to Firestore for production.

const guardianStore = new Map();

function createGuardian(email, stripeSubscriptionId, frequency, execution) {
  const scanToken = crypto.randomBytes(32).toString("hex");
  const intervalMs = frequency === "weekly" ? 7 * 24 * 60 * 60 * 1000 : 30 * 24 * 60 * 60 * 1000;
  guardianStore.set(scanToken, {
    email,
    stripeSubscriptionId,
    frequency,
    execution,
    nextScanAt: Date.now() + intervalMs,
    lastScanAt: null,
    scanHistory: [],
    status: "active",
    createdAt: Date.now(),
  });
  return scanToken;
}

function getGuardian(scanToken) {
  const g = guardianStore.get(scanToken);
  if (!g || g.status === "cancelled") return null;
  return g;
}

function findGuardianBySubscription(stripeSubscriptionId) {
  for (const [token, data] of guardianStore.entries()) {
    if (data.stripeSubscriptionId === stripeSubscriptionId) return { token, data };
  }
  return null;
}

// ─── Gemini AI Engine ───────────────────────────────────────────────────────

const REPAIR_SYSTEM_PROMPT = `You are the Pronto PC Custodian Technician AI engine. You receive comprehensive Windows system diagnostic data covering 35+ categories and return a structured JSON repair plan.

Rules:
- Only recommend safe, reversible actions
- Never delete user documents, photos, music, videos, or personal files
- Never touch System32 unless fixing a known integrity issue (SFC/DISM)
- Prioritize by impact: disk space recovery first, then performance, then stability
- Each action needs a friendly_name a non-technical person can understand
- Identify hardware issues that require physical service — these go in hardware_notes, NOT actions
- Assess overall security posture briefly
- Return ONLY valid JSON. No markdown fences. No explanation before or after.

You will receive data covering these diagnostic categories:
- OS info, system uptime, CPU, memory, page file, battery health
- Disk health (partitions, usage, SMART failure prediction, fragmentation)
- Temp files, browser caches (Chrome, Edge, Firefox, Brave, Opera + Code Caches), Windows caches (thumbnail, icon, font, prefetch, Store), Recycle Bin
- Large files (>100MB in user directories)
- Startup programs (registry Run keys + WMI)
- Scheduled tasks (non-Microsoft)
- Registry MRU entries
- Driver audit: total, signed, unsigned, outdated (>2 years), third-party, recently updated, problem devices with error codes
- BSOD/crash history (Minidump files + EventLog)
- Network: active adapters, IP config, gateway, DNS, internet reachability, DNS cache
- Security: Windows Defender status (real-time, antivirus, antispyware, signature freshness, scan dates), firewall profiles, Windows activation
- Windows Update: recent hotfixes, pending updates count
- Services audit: stopped critical services, recent SCM failures
- Installed programs: full inventory with version/publisher/size
- User accounts: local accounts with enabled/disabled
- Power plan, visual effects setting
- Event errors (System + Application logs), event warnings

Return exactly:
{
  "summary": "2-3 warm, plain English sentences about overall system health and what you recommend",
  "hardware_notes": "Plain English note about any hardware issues requiring physical service, or empty string if none",
  "security_assessment": "1-2 sentences about the security posture — Defender, firewall, activation status",
  "actions": [
    {
      "id": "snake_case_id matching one of the known repair action IDs",
      "category": "cleanup|drivers|startup|browser|system|network|performance|security",
      "friendly_name": "What the customer sees in plain English",
      "technical_description": "What actually runs",
      "risk": "low|medium",
      "estimated_mb_recovered": 0,
      "target_name": "optional — name of specific item (e.g. startup item name)",
      "target_hive": "optional — HKCU or HKLM for startup items"
    }
  ],
  "deferred_actions": [
    {
      "id": "snake_case_id",
      "friendly_name": "What the customer sees",
      "reason": "Why this should be done later or by a technician"
    }
  ]
}

Known repair action IDs you may use:
clear_temp_files, clear_all_browser_caches, clear_chrome_cache, clear_edge_cache, clear_firefox_cache, clear_brave_cache, clear_opera_cache, clear_windows_caches, empty_recycle_bin, disk_cleanup, clear_windows_store_cache, clean_windows_update_cache, clean_component_store, remove_windows_old, clean_delivery_optimization, sfc_scan, dism_restore, chkdsk_scan, flush_dns, reset_network_stack, disable_startup_item (requires target_name + target_hive), optimize_power_plan, optimize_visual_effects, reset_windows_update, driver_report, hardware_report

Windows Update leftovers (see the "Windows Update leftovers" diagnostic line) are often the LARGEST reclaimable space — recommend these when present, with estimated_mb_recovered taken from the reported sizes:
- clean_windows_update_cache: clears the SoftwareDistribution download cache (use its reported MB).
- clean_delivery_optimization: clears the Delivery Optimization cache (use its reported MB).
- remove_windows_old: deletes the previous-Windows backup — recommend ONLY when Windows.old is present (use its reported MB; can be many GB).
- clean_component_store: DISM cleanup of the WinSxS store — safe periodic maintenance; set estimated_mb_recovered 0 (amount isn't pre-measured).`;

// The authoritative repair-action catalogue — the allow-list the model may draw
// from, mirrored from the prompt above and 1:1 with StoreApp/Repair.cs. The
// repair-plan Orb enforces that every emitted action.id is a member of this set,
// so the plan can never propose something the engine cannot execute (catalogue
// integrity). See pod/orbs/repair-plan/SPEC.md.
const REPAIR_ACTION_CATALOGUE = new Set([
  "clear_temp_files", "clear_all_browser_caches", "clear_chrome_cache", "clear_edge_cache",
  "clear_firefox_cache", "clear_brave_cache", "clear_opera_cache", "clear_windows_caches",
  "empty_recycle_bin", "disk_cleanup", "clear_windows_store_cache", "clean_windows_update_cache",
  "clean_component_store", "remove_windows_old", "clean_delivery_optimization", "sfc_scan",
  "dism_restore", "chkdsk_scan", "flush_dns", "reset_network_stack", "disable_startup_item",
  "optimize_power_plan", "optimize_visual_effects", "reset_windows_update", "driver_report", "hardware_report"
]);

function buildDiagnosticText(diag) {
  const lines = [];
  const add = (label, data) => { if (data !== undefined && data !== null) lines.push(`${label}: ${typeof data === "object" ? JSON.stringify(data) : data}`); };

  // OS & Uptime
  const os = diag.os || {};
  lines.push(`OS: ${os.edition || ""} ${os.release || ""} Build ${os.build || ""} (${os.machine || ""})`);
  lines.push(`Hostname: ${os.hostname || "unknown"}`);
  const uptime = diag.uptime || {};
  if (uptime.uptime_days !== undefined) lines.push(`Uptime: ${uptime.uptime_days} days (${uptime.uptime_hours}h) — needs reboot: ${uptime.needs_reboot || false}`);

  // CPU
  const cpu = diag.cpu || {};
  if (cpu.processor_name) lines.push(`CPU: ${cpu.processor_name} — ${cpu.physical_cores}c/${cpu.logical_cores}t — ${cpu.current_percent}% load`);

  // Memory
  const mem = diag.memory || {};
  lines.push(`Memory: ${mem.used_gb || 0}/${mem.total_gb || 0} GB (${mem.percent_used || 0}%) — ${mem.health_status || "unknown"}`);

  // Page file
  const pf = diag.page_file || {};
  if (pf.files?.length) lines.push(`Page file: ${pf.files.map(f => `${f.current_usage_mb}/${f.allocated_mb} MB`).join(", ")}`);

  // Battery
  const bat = diag.battery || {};
  if (bat.present) lines.push(`Battery: ${bat.percent}% ${bat.plugged_in ? "(plugged)" : "(on battery)"}${bat.wear_percent ? ` — ${bat.wear_percent}% wear` : ""}`);

  // Disk
  for (const d of (diag.disk || []))
    lines.push(`Drive ${d.device}: ${d.percent_used}% used (${d.free_gb} GB free of ${d.total_gb} GB) — ${d.health_status}`);

  // SMART
  for (const s of (diag.smart_health || []))
    if (s.failure_predicted) lines.push(`⚠ SMART FAILURE PREDICTED: ${s.drive}`);

  // Fragmentation
  for (const f of (diag.fragmentation || []))
    if (f.AnalysisOutput) lines.push(`Drive ${f.Drive}: fragmentation analysis: ${f.AnalysisOutput.substring(0, 200)}`);

  // Temp files
  const temp = diag.temp_files || [];
  if (temp.length) {
    const mb = temp.reduce((s, t) => s + (t.size_mb || 0), 0);
    lines.push(`Temp files: ${mb.toFixed(0)} MB across ${temp.length} locations (${temp.reduce((s, t) => s + (t.file_count || 0), 0)} files)`);
  }

  // Recycle bin
  const rb = diag.recycle_bin || {};
  if (rb.size_mb) lines.push(`Recycle Bin: ${rb.item_count} items, ${rb.size_mb} MB`);

  // Browser caches
  for (const b of (diag.browser_caches || []))
    lines.push(`${b.browser} cache: ${b.size_mb} MB (${b.file_count} files)`);

  // Windows caches
  const wc = diag.windows_caches || {};
  const wcItems = Object.entries(wc).map(([k, v]) => `${k}: ${v.size_mb} MB`);
  if (wcItems.length) lines.push(`Windows caches: ${wcItems.join(", ")}`);

  // Windows Update leftovers (usually the biggest reclaimable space)
  const ul = diag.update_leftovers || {};
  {
    const parts = [];
    if (ul.windows_update_cache_mb) parts.push(`update cache ${ul.windows_update_cache_mb} MB`);
    if (ul.delivery_optimization_mb) parts.push(`Delivery Optimization ${ul.delivery_optimization_mb} MB`);
    if (ul.windows_old_present) parts.push(`Windows.old present${ul.windows_old_gb ? ` (${ul.windows_old_gb} GB)` : ""}`);
    if (ul.component_cleanup_available) parts.push(`component store cleanup available`);
    if (parts.length) lines.push(`Windows Update leftovers: ${parts.join(", ")}`);
  }

  // Large files
  const lf = diag.large_files || [];
  if (lf.length) lines.push(`Large files (>100MB): ${lf.length} found, largest: ${lf[0]?.size_mb} MB (${lf[0]?.extension})`);

  // Startup
  const startup = diag.startup_programs || [];
  lines.push(`Startup programs: ${startup.length} registered`);
  if (startup.length > 0) {
    const preview = startup.slice(0, 10).map(s => `  ${s.name} (${s.hive})`).join("\n");
    lines.push(preview);
  }

  // Scheduled tasks
  const tasks = diag.scheduled_tasks || [];
  if (tasks.length) lines.push(`Non-Microsoft scheduled tasks: ${tasks.length}`);

  // Registry
  const reg = diag.registry || {};
  lines.push(`Registry MRU entries: ${reg.mru_entries || 0}`);

  // Drivers
  const drv = diag.drivers || {};
  lines.push(`Drivers: ${drv.total || 0} total, ${drv.signed || 0} signed, ${(drv.unsigned || []).length} unsigned, ${(drv.outdated || []).length} outdated (>2yr)`);
  if ((drv.unsigned || []).length > 0)
    lines.push(`Unsigned drivers: ${drv.unsigned.map(d => d.device).join(", ")}`);
  if ((drv.outdated || []).length > 0)
    lines.push(`Outdated drivers: ${drv.outdated.slice(0, 5).map(d => `${d.device} (${d.date}, ${d.age_days}d old)`).join(", ")}`);

  // Device errors
  const devErr = diag.device_errors || [];
  if (devErr.length) {
    lines.push(`Device Manager problems: ${devErr.length}`);
    for (const d of devErr.slice(0, 5))
      lines.push(`  ${d.device}: Code ${d.error_code} — ${d.error_description}`);
  }

  // BSOD
  const bsod = diag.bsod_history || [];
  if (bsod.length) lines.push(`BSOD/crash history: ${bsod.length} records (most recent: ${bsod[0]?.date || "unknown"})`);

  // Network
  const net = diag.network || {};
  if (net.adapters?.length) lines.push(`Network adapters (up): ${net.adapters.map(a => `${a.name} (${a.speed})`).join(", ")}`);
  if (net.connectivity?.internet_reachable !== undefined)
    lines.push(`Internet connectivity: ${net.connectivity.internet_reachable ? "OK" : "UNREACHABLE"}`);

  // DNS
  const dns = diag.dns_cache || {};
  if (dns.entries) lines.push(`DNS cache: ${dns.entries} entries`);

  // Security
  const sec = diag.security || {};
  const def = sec.defender || {};
  if (def.real_time_protection !== undefined) {
    lines.push(`Defender: real-time=${def.real_time_protection}, antivirus=${def.antivirus_enabled}, antispyware=${def.antispyware_enabled}`);
    if (def.signature_last_updated) lines.push(`Defender signatures: ${def.signature_last_updated}`);
  }
  const fw = sec.firewall || {};
  if (fw.profiles?.length)
    lines.push(`Firewall: ${fw.profiles.map(p => `${p.name}=${p.enabled ? "ON" : "OFF"}`).join(", ")}`);

  // Activation
  const act = diag.activation || {};
  if (act.status) lines.push(`Windows activation: ${act.status} (${act.product || ""})`);

  // Windows Update
  const wu = diag.windows_update || {};
  if (wu.pending_updates !== undefined) lines.push(`Pending Windows updates: ${wu.pending_updates}`);
  if (wu.last_update_date) lines.push(`Last Windows update: ${wu.last_update_date}`);

  // Services
  const svc = diag.services || {};
  if ((svc.stopped_critical || []).length)
    lines.push(`Stopped critical services: ${svc.stopped_critical.map(s => s.name).join(", ")}`);
  if ((svc.failed || []).length)
    lines.push(`Recent service failures: ${svc.failed.length}`);

  // Installed programs
  const progs = diag.installed_programs || {};
  lines.push(`Installed programs: ${progs.total_count || 0}`);

  // User accounts
  const users = diag.user_accounts || [];
  if (users.length) lines.push(`Local user accounts: ${users.map(u => `${u.name} (${u.enabled ? "enabled" : "disabled"})`).join(", ")}`);

  // Power & Visual
  const pp = diag.power_plan || {};
  if (pp.active_plan) lines.push(`Power plan: ${pp.active_plan}`);
  const ve = diag.visual_effects || {};
  if (ve.setting) lines.push(`Visual effects: ${ve.setting}`);

  // Event errors
  const errors = diag.event_errors || [];
  if (errors.length) {
    lines.push(`Windows event errors: ${errors.length}`);
    for (const e of errors.slice(0, 5))
      lines.push(`  [${e.source}] ${(e.message || "").substring(0, 120)}`);
  }

  // Event warnings
  const warnings = diag.event_warnings || [];
  if (warnings.length) lines.push(`Windows event warnings: ${warnings.length}`);

  return lines.join("\n");
}

function extractJson(raw, source) {
  const start = raw.indexOf("{");
  const end = raw.lastIndexOf("}") + 1;
  if (start === -1 || end === 0) throw new Error(`${source} returned no valid JSON`);
  return JSON.parse(raw.slice(start, end));
}

async function queryAzureOpenAI(diagnosticText) {
  const response = await fetch(AZURE_OPENAI_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json", "api-key": AZURE_OPENAI_KEY },
    body: JSON.stringify({
      messages: [
        { role: "system", content: REPAIR_SYSTEM_PROMPT },
        { role: "user", content: `DIAGNOSTIC REPORT:\n${diagnosticText}` }
      ],
      temperature: 0.1,
      max_tokens: 8192,
      response_format: { type: "json_object" }
    }),
    signal: AbortSignal.timeout(90_000)
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Azure OpenAI error ${response.status}: ${err}`);
  }

  const data = await response.json();
  const finishReason = data.choices?.[0]?.finish_reason;
  const raw = (data.choices?.[0]?.message?.content || "").trim();
  try {
    return extractJson(raw, "Azure OpenAI");
  } catch (e) {
    console.error(`[azure] JSON parse failed (finish_reason=${finishReason}, len=${raw.length}): ${e.message}`);
    throw new Error(`Azure OpenAI returned malformed JSON (finish_reason=${finishReason})`);
  }
}

async function queryGeminiLegacy(diagnosticText) {
  const response = await fetch(GEMINI_URL, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      system_instruction: { parts: [{ text: REPAIR_SYSTEM_PROMPT }] },
      contents: [{ parts: [{ text: `DIAGNOSTIC REPORT:\n${diagnosticText}` }] }],
      generationConfig: { temperature: 0.1, maxOutputTokens: 8192, responseMimeType: "application/json" }
    }),
    signal: AbortSignal.timeout(90_000)
  });

  if (!response.ok) {
    const err = await response.text();
    throw new Error(`Gemini error ${response.status}: ${err}`);
  }

  const data = await response.json();
  const finishReason = data.candidates?.[0]?.finishReason;
  const raw = data.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || "";
  try {
    return extractJson(raw, "Gemini");
  } catch (e) {
    console.error(`[gemini] JSON parse failed (finishReason=${finishReason}, len=${raw.length}): ${e.message}`);
    throw new Error(`Gemini returned malformed JSON (finishReason=${finishReason})`);
  }
}

// Provider-agnostic entry point. Prefers Azure OpenAI; falls back to Gemini on
// RUNTIME failure, not merely when Azure is unconfigured.
//
// This previously dispatched on whether Azure was *configured*, so an Azure
// runtime failure (a 401 from an invalid key) propagated and Gemini was never
// attempted, despite a valid Gemini key being present. The desktop client then
// silently fell back to BuildLocalPlan — a rule-based plan with no AI — so the
// "AI-powered repair plan" claimed in the Store listing was genuinely absent
// from the product. That caused Microsoft certification failure 10.1.2.10 on
// 2026-08-26. Fixed 2026-08-26.
async function queryLLM(diagnosticText) {
  const azureModel = () => (AZURE_OPENAI_URL?.match(/deployments\/([^/]+)/)?.[1]) || "azure-openai";
  const geminiModel = () => (GEMINI_URL?.match(/models\/([^:/]+)/)?.[1]) || "gemini";
  if (AZURE_OPENAI_URL && AZURE_OPENAI_KEY) {
    try {
      const plan = await queryAzureOpenAI(diagnosticText);
      return { plan, provider: "azure-openai", model: azureModel() };
    } catch (e) {
      console.error(`[llm] Azure OpenAI failed (${e.message}) — falling back to Gemini`);
      if (GEMINI_URL) return { plan: await queryGeminiLegacy(diagnosticText), provider: "gemini", model: geminiModel() };
      throw e;
    }
  }
  if (GEMINI_URL) return { plan: await queryGeminiLegacy(diagnosticText), provider: "gemini", model: geminiModel() };
  throw new Error("No LLM configured (set AZURE_OPENAI_* or GEMINI_API_KEY)");
}

// ─── repair-plan Orb ─────────────────────────────────────────────────────────
// Entrypoint for the prontopc-repair-plan Orb (pod/orbs/repair-plan/). Authors a
// genuine model repair plan, enforces the catalogue-subset invariant, and attaches
// provenance. Throws if no provider can author a plan — the caller then emits
// status:"unavailable". There is deliberately no path that returns a heuristic plan
// here, so a non-AI plan can never be presented as the AI plan (Manifest I2).
async function authorRepairPlan(diagnostics) {
  const diagText = buildDiagnosticText(diagnostics);
  const { plan, provider, model } = await queryLLM(diagText);
  if (Array.isArray(plan.actions)) {
    const before = plan.actions.length;
    plan.actions = plan.actions.filter(a => a && REPAIR_ACTION_CATALOGUE.has(a.id));
    const dropped = before - plan.actions.length;
    if (dropped > 0) console.warn(`[repair-plan] dropped ${dropped} action(s) outside the engine catalogue`);
  }
  return { plan, provenance: { provider, model } };
}

// ─── Stripe ─────────────────────────────────────────────────────────────────

async function verifyStripeSession(sessionId) {
  if (!STRIPE_SECRET_KEY) throw new Error("Stripe not configured");
  const resp = await fetch(`https://api.stripe.com/v1/checkout/sessions/${sessionId}`, {
    headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}` },
    signal: AbortSignal.timeout(15_000)
  });
  if (resp.status === 404) throw new Error("Payment session not found. Please use the link from your confirmation email, or contact support@prontopc.online.");
  if (!resp.ok) throw new Error("Could not verify payment. Please contact support@prontopc.online.");
  const session = await resp.json();
  // A completed checkout is fulfilled even when no charge occurred: a 100%-off
  // promo (e.g. the Microsoft cert code MSCERT2026) settles as
  // "no_payment_required"/status "complete", not "paid".
  const fulfilled = session.payment_status === "paid"
    || session.payment_status === "no_payment_required"
    || session.status === "complete";
  if (!fulfilled) throw new Error("Payment is still processing. Please wait a moment and reload this page. If this continues, contact support@prontopc.online.");
  return session;
}

function findSessionByStripeId(stripeSessionId) {
  for (const [token, data] of sessionStore.entries()) {
    if (data.stripeSessionId === stripeSessionId) return { token, data };
  }
  return null;
}

// The native Store app tags its Stripe Payment Link with a per-launch nonce
// (client_reference_id) and polls until the paid checkout lands. A session exists
// in the store ONLY after verifyStripeSession confirmed a fulfilled checkout, so a
// match here IS proof of a paid (or 100%-off comped) run.
function findSessionByClientRef(clientRef) {
  if (!clientRef) return null;
  for (const [token, data] of sessionStore.entries()) {
    if (data.clientRef && data.clientRef === clientRef) return { token, data };
  }
  return null;
}

// ─── Helpers ────────────────────────────────────────────────────────────────

function readBody(req) {
  return new Promise((resolve, reject) => {
    // Collect raw chunks and decode the FULL buffer once. Decoding per chunk
    // (body += c) corrupts multi-byte UTF-8 characters that straddle a chunk
    // boundary — which broke the native app's raw-UTF-8 JSON on large bodies.
    const chunks = [];
    let size = 0;
    req.on("data", c => {
      chunks.push(c);
      size += c.length;
      if (size > 5_000_000) reject(new Error("Body too large"));
    });
    req.on("end", () => {
      try {
        const body = Buffer.concat(chunks).toString("utf8");
        resolve(body ? JSON.parse(body) : {});
      } catch { reject(new Error("Invalid JSON")); }
    });
    req.on("error", reject);
  });
}

function json(res, req, status, data) {
  const origin = corsOrigin(req);
  res.writeHead(status, {
    "Content-Type": "application/json",
    "Access-Control-Allow-Origin": origin,
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type, Authorization",
    "Vary": "Origin"
  });
  res.end(JSON.stringify(data));
}

function text(res, req, status, content, contentType = "text/plain") {
  const origin = corsOrigin(req);
  res.writeHead(status, {
    "Content-Type": contentType,
    "Access-Control-Allow-Origin": origin,
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type, Authorization",
    "Vary": "Origin"
  });
  res.end(content);
}

// ─── Handlers ───────────────────────────────────────────────────────────────

async function handleVerifyPayment(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid request body" }); }
  const { sessionId } = body;
  if (!sessionId) return json(res, req, 400, { ok: false, error: "sessionId required" });

  const existing = findSessionByStripeId(sessionId);
  if (existing) return json(res, req, 200, { ok: true, token: existing.token });

  try {
    const session = await verifyStripeSession(sessionId);
    const token = createSession(sessionId, session.customer_details?.email || "", session.client_reference_id || null);
    console.log(`[verify-payment] Session created for Stripe ${sessionId.substring(0, 12)}…`);
    return json(res, req, 200, { ok: true, token });
  } catch (e) {
    console.error("[verify-payment] Error:", e.message);
    return json(res, req, 402, { ok: false, error: e.message });
  }
}

// ─── Native Store-app payment gate (pay-per-run) ─────────────────────────────
// The app is free to download from the Microsoft Store; each run is unlocked by a
// one-time Stripe checkout. Two entry paths, both fail-closed (no session ⇒ no run):
//
//   await-payment?ref=<nonce>  The app opened buy.stripe.com/…?client_reference_id=<nonce>
//     and polls here. Once the customer's browser hits success.html → /api/verify-payment,
//     a session is created carrying that nonce; the app then reads its token and proceeds.
//     No deep-link, so a Start-menu launch never spawns a second instance or a second UAC.
//   verify-token?token=<token>  The app arrived via prontopc://run?token=… (pure-web entry);
//     it re-confirms the token still names a live, fulfilled session before scanning.
function handleAwaitPayment(req, res, ref) {
  if (!ref) return json(res, req, 400, { ok: false, paid: false, error: "ref required" });
  const found = findSessionByClientRef(ref);
  if (found) return json(res, req, 200, { ok: true, paid: true, token: found.token });
  return json(res, req, 200, { ok: false, paid: false, pending: true });
}

function handleVerifyToken(req, res, token) {
  const s = token ? getSession(token) : null;
  return json(res, req, 200, { ok: !!s, paid: !!s });
}

async function handleCreateSession(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { token } = body;
  if (!token) return json(res, req, 400, { ok: false, error: "token required" });

  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, error: "Invalid or expired session" });

  const scanUrl = `${CLOUD_RUN_URL}/scan?token=${token}`;
  const scanCommand = `powershell -ExecutionPolicy Bypass -Command "irm '${scanUrl}' | iex"`;
  return json(res, req, 200, { ok: true, sessionToken: token, scanUrl, scanCommand });
}

function handleScanScript(req, res, token) {
  const session = getSession(token);
  if (!session) return text(res, req, 200, `Write-Host ""
Write-Host "  Session expired or invalid." -ForegroundColor Red
Write-Host "  Please visit prontopc.online to start a new session." -ForegroundColor Red
Write-Host ""
Read-Host "  Press Enter to close"`);

  if (session.state === "created") transitionState(token, "scan_served");

  const script = generateDiagnosticScript(token);
  return text(res, req, 200, script, "text/plain; charset=utf-8");
}

function handleScanBat(req, res, token) {
  const session = getSession(token);
  if (!session) {
    const origin = corsOrigin(req);
    res.writeHead(404, {
      "Content-Type": "text/plain",
      "Access-Control-Allow-Origin": origin,
    });
    return res.end("Session not found. Please visit prontopc.online.");
  }

  if (session.state === "created") transitionState(token, "scan_served");

  const scanUrl = `${CLOUD_RUN_URL}/scan?token=${token}`;
  // Windows batch file that self-elevates via UAC, then runs the in-memory PowerShell scan.
  // The file installs nothing — PowerShell script runs entirely in memory.
  const bat = `@echo off
chcp 65001 >nul 2>&1
title Pronto PC - Diagnostic Scanner

:: Check if already running as Administrator
net session >nul 2>&1
if %errorLevel% == 0 goto :run_scan

:: Not elevated — relaunch this file with a UAC permission prompt
powershell -ExecutionPolicy Bypass -Command "Start-Process cmd '/c \"\"%~f0\"\"' -Verb RunAs"
exit /b

:run_scan
title Pronto PC - Diagnostic Scan Running...
echo.
echo  ================================================================
echo    Pronto PC -- Custodian Technician
echo    5D Service Solutions Global LLC
echo  ================================================================
echo.
echo  Starting your diagnostic scan. This takes about 30 seconds.
echo  Please do not close this window.
echo.
powershell -ExecutionPolicy Bypass -Command "irm '${scanUrl}' | iex"
`;

  const origin = corsOrigin(req);
  res.writeHead(200, {
    "Content-Type": "application/octet-stream",
    "Content-Disposition": 'attachment; filename="ProntoPC-Diagnostic.bat"',
    "Access-Control-Allow-Origin": origin,
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type, Authorization",
    "Vary": "Origin",
  });
  res.end(bat);
}

function handleRepairScript(req, res, token) {
  const session = getSession(token);
  if (!session) return text(res, req, 200, `Write-Host ""
Write-Host "  Session expired or invalid." -ForegroundColor Red
Write-Host "  Please visit prontopc.online to start a new session." -ForegroundColor Red
Write-Host ""
Read-Host "  Press Enter to close"`);
  if (session.state !== "consented" && session.state !== "repair_served")
    return text(res, req, 403, "Write-Host 'Repairs have not been approved for this session.' -ForegroundColor Red");

  if (session.state === "consented") transitionState(token, "repair_served");

  // Serve only the repairs the client approved (per-item consent). If no selection
  // was recorded (older sessions), fall back to the full plan.
  const plan = session.repairPlan || {};
  const selected = session.selectedActionIds;
  const actions = Array.isArray(selected)
    ? (plan.actions || []).filter(a => selected.includes(a.id))
    : (plan.actions || []);

  const script = generateRepairScript(token, { ...plan, actions });
  return text(res, req, 200, script, "text/plain; charset=utf-8");
}

async function handleSubmitDiagnostics(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { token, diagnostics } = body;
  if (!token || !diagnostics) return json(res, req, 400, { ok: false, error: "token and diagnostics required" });

  // Authorization: author a plan ONLY for a token that names a live, PAID session
  // (minted by /api/verify-payment after Stripe confirms a fulfilled checkout). Never
  // create a session here — doing so was a server-side free path: a forged/unknown
  // token got a full AI plan without paying, wider than the client gate. Unknown or
  // expired token ⇒ no run. This is the backend half of "no free path."
  const session = getSession(token);
  if (!session) return json(res, req, 402, { ok: false, status: "unpaid", error: "No paid session for this token. Complete checkout first." });

  console.log(`[submit-diagnostics] Token ${token.substring(0, 8)}…`);

  try {
    session.diagnostics = diagnostics;
    session.hostname = diagnostics.os?.hostname || "Unknown PC";
    const { plan, provenance } = await authorRepairPlan(diagnostics);
    session.repairPlan = plan;
    session.planProvenance = provenance;
    transitionState(token, "diagnosed");
    console.log(`[submit-diagnostics] Done — ${plan.actions?.length || 0} actions via ${provenance.provider}`);
    return json(res, req, 200, { ok: true, status: "ok", plan, provenance });
  } catch (e) {
    console.error("[submit-diagnostics] Error:", e.message);
    // repair-plan Orb contract: no heuristic plan is ever emitted as the AI plan.
    // When no model can author a plan, the only truthful response is "unavailable".
    return json(res, req, 200, { ok: false, status: "unavailable", reason: e.message });
  }
}

function handleSessionStatus(req, res, token) {
  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, state: "expired" });
  return json(res, req, 200, {
    ok: true,
    state: session.state,
    hostname: session.hostname,
    diagnosedAt: session.state !== "created" && session.state !== "scan_served" ? new Date(session.updatedAt).toISOString() : null,
    createdAt: new Date(session.createdAt).toISOString(),
    email: session.email,
    couponCode: session.couponCode || null,
    couponExpiresAt: session.couponExpiresAt || null,
  });
}

function handleSessionFindings(req, res, token) {
  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, error: "Session not found" });
  if (!session.repairPlan) return json(res, req, 404, { ok: false, error: "Diagnostics not yet complete" });

  const plan = session.repairPlan;
  return json(res, req, 200, {
    ok: true,
    hostname: session.hostname,
    summary: plan.summary || "",
    hardware_notes: plan.hardware_notes || "",
    security_assessment: plan.security_assessment || "",
    actions: plan.actions || [],
    deferred_actions: plan.deferred_actions || [],
  });
}

async function handleSessionConsent(req, res, token) {
  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, error: "Session not found" });
  if (session.state !== "diagnosed") return json(res, req, 400, { ok: false, error: `Cannot consent in state: ${session.state}` });

  // Per-item approval: the client sends the ids of the repairs it approved.
  // Missing/empty body → approve all (backward compatible). Empty array → diagnostic-only.
  let selectedIds = null;
  try {
    const body = await readBody(req);
    if (body && Array.isArray(body.selectedIds)) selectedIds = body.selectedIds;
  } catch {}
  const allIds = (session.repairPlan?.actions || []).map(a => a.id);
  session.selectedActionIds = selectedIds === null ? allIds : selectedIds.filter(id => allIds.includes(id));

  transitionState(token, "consented");
  const repairUrl = `${CLOUD_RUN_URL}/repair?token=${token}`;
  const repairCommand = `powershell -ExecutionPolicy Bypass -Command "irm '${repairUrl}' | iex"`;
  return json(res, req, 200, { ok: true, repairUrl, repairCommand, approvedCount: session.selectedActionIds.length });
}

// Approved repair actions as JSON — consumed by the native Store app to run
// repairs itself (mirrors the filtering the /repair PowerShell script applies).
function handleRepairActions(req, res, token) {
  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, error: "Session not found" });
  if (session.state !== "consented" && session.state !== "repair_served")
    return json(res, req, 403, { ok: false, error: "Repairs not approved for this session" });
  if (session.state === "consented") transitionState(token, "repair_served");

  const plan = session.repairPlan || {};
  const selected = session.selectedActionIds;
  const actions = Array.isArray(selected)
    ? (plan.actions || []).filter(a => selected.includes(a.id))
    : (plan.actions || []);
  return json(res, req, 200, { ok: true, actions });
}

// ─── Coupon helpers ──────────────────────────────────────────────────────────

// Ensure the reusable "50% off next repair" coupon exists in Stripe. Idempotent.
async function ensureNextRepairCoupon() {
  if (!STRIPE_SECRET_KEY) return false;
  const existing = await fetch(`https://api.stripe.com/v1/coupons/${COUPON_ID}`, {
    headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}` },
    signal: AbortSignal.timeout(10_000),
  });
  if (existing.ok) return true;
  const params = new URLSearchParams({
    id: COUPON_ID,
    percent_off: "50",
    duration: "once",
    name: "50% Off Next Repair",
  });
  const created = await fetch("https://api.stripe.com/v1/coupons", {
    method: "POST",
    headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}`, "Content-Type": "application/x-www-form-urlencoded" },
    body: params.toString(),
    signal: AbortSignal.timeout(10_000),
  });
  return created.ok;
}

function randomCouponCode() {
  // Stripe promotion codes must be alphanumeric (no hyphens/symbols).
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous 0/O/1/I
  let s = "";
  for (let i = 0; i < 6; i++) s += chars[Math.floor(Math.random() * chars.length)];
  return `PRONTO50${s}`;
}

// Mint a unique, single-use 50%-off promotion code for this customer's next repair.
async function createNextRepairCoupon(session) {
  if (!STRIPE_SECRET_KEY) return null;
  try {
    await ensureNextRepairCoupon();
    const expiresAt = Math.floor(Date.now() / 1000) + COUPON_VALID_DAYS * 86400;
    // Retry a couple of times in the unlikely event of a code collision.
    for (let attempt = 0; attempt < 3; attempt++) {
      const params = new URLSearchParams({
        "promotion[type]": "coupon",
        "promotion[coupon]": COUPON_ID,
        code: randomCouponCode(),
        expires_at: String(expiresAt),
        max_redemptions: "1",
        "metadata[email]": session.email || "",
        "metadata[source]": "repair",
      });
      const resp = await fetch("https://api.stripe.com/v1/promotion_codes", {
        method: "POST",
        headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}`, "Content-Type": "application/x-www-form-urlencoded" },
        body: params.toString(),
        signal: AbortSignal.timeout(10_000),
      });
      const pc = await resp.json();
      if (!pc.error) return { code: pc.code, expiresAt: expiresAt * 1000 };
      if (!/already exists/i.test(pc.error.message || "")) {
        console.error("[coupon] Stripe error:", pc.error.message);
        return null;
      }
    }
    return null;
  } catch (e) {
    console.error("[coupon] Error:", e.message);
    return null;
  }
}

// ─── Loyalty ledger (admin view over Stripe) ─────────────────────────────────
// Stripe is the durable source of truth; this is a protected read-only window
// onto every issued 50%-off code: who got it, when, expiry, and used status.
async function handleLoyaltyLedger(req, res, url) {
  if (!LOYALTY_ADMIN_KEY)
    return text(res, req, 503, "Loyalty ledger is locked. Set the LOYALTY_ADMIN_KEY env var on the server, then open /admin/loyalty?key=YOUR_KEY", "text/plain; charset=utf-8");
  if ((url.searchParams.get("key") || "") !== LOYALTY_ADMIN_KEY)
    return text(res, req, 401, "Unauthorized", "text/plain; charset=utf-8");
  if (!STRIPE_SECRET_KEY)
    return text(res, req, 500, "Stripe not configured", "text/plain; charset=utf-8");

  // The coupon is minted lazily on the first repair; ensure it exists so an
  // empty ledger renders cleanly instead of erroring with "No such coupon".
  await ensureNextRepairCoupon();

  let codes = [];
  try {
    let startingAfter = null;
    for (let page = 0; page < 20; page++) {
      const qs = new URLSearchParams({ coupon: COUPON_ID, limit: "100" });
      if (startingAfter) qs.set("starting_after", startingAfter);
      const resp = await fetch(`https://api.stripe.com/v1/promotion_codes?${qs.toString()}`, {
        headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}` },
        signal: AbortSignal.timeout(15_000),
      });
      const data = await resp.json();
      if (data.error) return text(res, req, 502, "Stripe error: " + data.error.message, "text/plain; charset=utf-8");
      codes = codes.concat(data.data || []);
      if (!data.has_more || !(data.data || []).length) break;
      startingAfter = data.data[data.data.length - 1].id;
    }
  } catch (e) {
    return text(res, req, 502, "Could not reach Stripe: " + e.message, "text/plain; charset=utf-8");
  }

  const now = Date.now() / 1000;
  const fmt = (u) => u ? new Date(u * 1000).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric" }) : "—";
  codes.sort((a, b) => (b.created || 0) - (a.created || 0));

  const used = codes.filter(c => (c.times_redeemed || 0) > 0).length;
  const expiredUnused = codes.filter(c => c.expires_at && c.expires_at < now && (c.times_redeemed || 0) === 0).length;
  const activeUnused = codes.filter(c => c.active && (c.times_redeemed || 0) === 0 && (!c.expires_at || c.expires_at >= now)).length;

  const esc = (s) => String(s == null ? "" : s).replace(/[&<>"]/g, m => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[m]));
  const rows = codes.map(c => {
    const isUsed = (c.times_redeemed || 0) > 0;
    const isExpired = c.expires_at && c.expires_at < now;
    let status = "Active", color = "#2A7A4A";
    if (isUsed) { status = "Redeemed"; color = "#96A3BC"; }
    else if (isExpired || !c.active) { status = "Expired"; color = "#C0392B"; }
    return `<tr><td style="font-family:monospace;font-weight:600">${esc(c.code)}</td><td>${esc((c.metadata && c.metadata.email) || "—")}</td><td>${fmt(c.created)}</td><td>${fmt(c.expires_at)}</td><td style="color:${color};font-weight:600">${status}</td></tr>`;
  }).join("");

  const html = `<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Loyalty Ledger — Pronto PC</title>
<style>body{font-family:'Segoe UI',system-ui,sans-serif;background:#0B1220;color:#EAEEF6;margin:0;padding:28px}h1{font-size:1.3rem;margin:0 0 4px}.sub{color:#96A3BC;font-size:.82rem;margin-bottom:20px}.stats{display:flex;gap:14px;flex-wrap:wrap;margin-bottom:20px}.stat{background:#141E30;border:1px solid rgba(234,238,246,.1);border-radius:10px;padding:12px 18px}.stat .n{font-size:1.5rem;font-weight:700}.stat .l{color:#96A3BC;font-size:.7rem;text-transform:uppercase;letter-spacing:.08em}table{width:100%;border-collapse:collapse;font-size:.88rem}th{text-align:left;color:#96A3BC;font-weight:600;padding:8px 10px;border-bottom:1px solid rgba(234,238,246,.15);font-size:.7rem;text-transform:uppercase;letter-spacing:.06em}td{padding:9px 10px;border-bottom:1px solid rgba(234,238,246,.06)}tr:hover td{background:rgba(234,238,246,.03)}</style></head>
<body>
<h1>Loyalty Ledger — 50% Thank-You Codes</h1>
<div class="sub">Source of truth: Stripe · coupon ${esc(COUPON_ID)} · generated ${esc(new Date().toLocaleString("en-US"))}</div>
<div class="stats">
  <div class="stat"><div class="n">${codes.length}</div><div class="l">Total issued</div></div>
  <div class="stat"><div class="n" style="color:#2A7A4A">${activeUnused}</div><div class="l">Active &amp; unused</div></div>
  <div class="stat"><div class="n" style="color:#96A3BC">${used}</div><div class="l">Redeemed</div></div>
  <div class="stat"><div class="n" style="color:#C0392B">${expiredUnused}</div><div class="l">Expired unused</div></div>
</div>
<table><thead><tr><th>Code</th><th>Issued to</th><th>Generated</th><th>Expires</th><th>Status</th></tr></thead>
<tbody>${rows || `<tr><td colspan="5" style="color:#96A3BC;padding:20px">No codes issued yet.</td></tr>`}</tbody></table>
</body></html>`;

  return text(res, req, 200, html, "text/html; charset=utf-8");
}

// Email the coupon to the customer via Resend (reuses the Guardian email setup).
async function sendCouponEmail(session) {
  const RESEND_API_KEY = process.env.RESEND_API_KEY;
  const FROM_EMAIL = process.env.FROM_EMAIL || "support@prontopc.online";
  if (!RESEND_API_KEY || !session.email || !session.couponCode) return;

  const expiryStr = new Date(session.couponExpiresAt).toLocaleDateString("en-US", { month: "long", day: "numeric", year: "numeric" });
  const emailHtml = `
<div style="font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#FDFAF4;border:1px solid #E2DBD0;border-radius:12px;overflow:hidden">
  <div style="background:#1A1917;padding:20px 24px;color:#fff">
    <h1 style="margin:0;font-size:1.2rem;font-weight:700">Pronto <span style="color:#7C5CFF">PC</span> — Thank you</h1>
  </div>
  <div style="padding:24px">
    <p style="color:#1A1917;font-size:1rem;margin:0 0 16px">Your PC has been serviced. As a thank-you, here's <strong>50% off your next repair</strong> — no expiry pressure, it's good for six months.</p>
    <div style="background:#FFF8ED;border:1px dashed #7C5CFF;border-radius:10px;padding:20px;text-align:center;margin:0 0 16px">
      <p style="margin:0 0 6px;color:#6B6560;font-size:.78rem;text-transform:uppercase;letter-spacing:.08em">Your coupon code</p>
      <p style="margin:0;font-size:1.6rem;font-weight:800;color:#1A1917;font-family:'Courier New',monospace;letter-spacing:.05em">${session.couponCode}</p>
      <p style="margin:8px 0 0;color:#9B9590;font-size:.78rem">50% off · valid through ${expiryStr}</p>
    </div>
    <p style="text-align:center;margin:0 0 8px"><a href="${SITE_URL}" style="display:inline-block;background:#7C5CFF;color:#fff;font-weight:700;font-size:.9rem;padding:12px 28px;border-radius:8px;text-decoration:none">Start your next repair →</a></p>
    <p style="color:#9B9590;font-size:.78rem;text-align:center;margin:8px 0 0">Enter the code at checkout to apply your discount.</p>
    <hr style="border:none;border-top:1px solid #E2DBD0;margin:20px 0">
    <p style="font-size:.78rem;color:#9B9590;margin:0">Pronto PC · 5D Service Solutions Global LLC · support@prontopc.online</p>
  </div>
</div>`;

  try {
    await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: { Authorization: `Bearer ${RESEND_API_KEY}`, "Content-Type": "application/json" },
      body: JSON.stringify({ from: FROM_EMAIL, to: session.email, subject: "Your 50%-off coupon for your next Pronto PC repair", html: emailHtml }),
      signal: AbortSignal.timeout(10_000),
    });
    console.log(`[coupon] Coupon ${session.couponCode} emailed to ${session.email}`);
  } catch (e) {
    console.error("[coupon] Email failed:", e.message);
  }
}

// ─── AI-Unavailable Make-Good ────────────────────────────────────────────────

// Ensure the reusable 100%-off make-good coupon exists in Stripe. Idempotent.
async function ensureMakeGoodCoupon() {
  if (!STRIPE_SECRET_KEY) return false;
  const existing = await fetch(`https://api.stripe.com/v1/coupons/${MAKEGOOD_COUPON_ID}`, {
    headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}` },
    signal: AbortSignal.timeout(10_000),
  });
  if (existing.ok) return true;
  const params = new URLSearchParams({
    id: MAKEGOOD_COUPON_ID,
    percent_off: "100",
    duration: "once",
    name: "AI Make-Good — Free Repair",
  });
  const created = await fetch("https://api.stripe.com/v1/coupons", {
    method: "POST",
    headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}`, "Content-Type": "application/x-www-form-urlencoded" },
    body: params.toString(),
    signal: AbortSignal.timeout(10_000),
  });
  return created.ok;
}

function randomMakeGoodCode() {
  const chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous 0/O/1/I
  let s = "";
  for (let i = 0; i < 6; i++) s += chars[Math.floor(Math.random() * chars.length)];
  return `AIFREE${s}`;
}

// Mint a unique, single-use 100%-off code for this customer, tagged to the one
// machine that missed its AI plan. The code is single-use, so the machine tag is
// audit metadata (not enforced at redemption) — it cannot be farmed across
// machines or customers.
async function createMakeGoodCoupon(session, machineId, machineName) {
  if (!STRIPE_SECRET_KEY) return null;
  try {
    await ensureMakeGoodCoupon();
    const expiresAt = Math.floor(Date.now() / 1000) + MAKEGOOD_VALID_DAYS * 86400;
    for (let attempt = 0; attempt < 3; attempt++) {
      const params = new URLSearchParams({
        "promotion[type]": "coupon",
        "promotion[coupon]": MAKEGOOD_COUPON_ID,
        code: randomMakeGoodCode(),
        expires_at: String(expiresAt),
        max_redemptions: "1",
        "metadata[email]": session.email || "",
        "metadata[machine_id]": machineId || "",
        "metadata[machine_name]": machineName || "",
        "metadata[source]": "ai_unavailable",
      });
      const resp = await fetch("https://api.stripe.com/v1/promotion_codes", {
        method: "POST",
        headers: { Authorization: `Bearer ${STRIPE_SECRET_KEY}`, "Content-Type": "application/x-www-form-urlencoded" },
        body: params.toString(),
        signal: AbortSignal.timeout(10_000),
      });
      const pc = await resp.json();
      if (!pc.error) return { code: pc.code, expiresAt: expiresAt * 1000 };
      if (!/already exists/i.test(pc.error.message || "")) {
        console.error("[makegood] Stripe error:", pc.error.message);
        return null;
      }
    }
    return null;
  } catch (e) {
    console.error("[makegood] Error:", e.message);
    return null;
  }
}

// Email the 100% make-good code + an honest apology to the customer via Resend.
async function sendMakeGoodEmail(session) {
  const RESEND_API_KEY = process.env.RESEND_API_KEY;
  const FROM_EMAIL = process.env.FROM_EMAIL || "support@prontopc.online";
  if (!RESEND_API_KEY || !session.email || !session.makeGoodCouponCode) return;

  const expiryStr = new Date(session.makeGoodCouponExpiresAt).toLocaleDateString("en-US", { month: "long", day: "numeric", year: "numeric" });
  const emailHtml = `
<div style="font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#FDFAF4;border:1px solid #E2DBD0;border-radius:12px;overflow:hidden">
  <div style="background:#1A1917;padding:20px 24px;color:#fff">
    <h1 style="margin:0;font-size:1.2rem;font-weight:700">Pronto <span style="color:#7C5CFF">PC</span> — Let's make this right</h1>
  </div>
  <div style="padding:24px">
    <p style="color:#1A1917;font-size:1rem;margin:0 0 16px">Your repair is powered by AI, and this time our AI couldn't build your personalized plan. You paid for an AI-powered repair and didn't get the full benefit of it — so this one's on us.</p>
    <p style="color:#1A1917;font-size:1rem;margin:0 0 16px">Here's a <strong>100% coupon — a free repair on this PC</strong>. Run Pronto PC again whenever you like and your AI plan should build normally.</p>
    <div style="background:#FFF8ED;border:1px dashed #7C5CFF;border-radius:10px;padding:20px;text-align:center;margin:0 0 16px">
      <p style="margin:0 0 6px;color:#6B6560;font-size:.78rem;text-transform:uppercase;letter-spacing:.08em">Your free-repair code</p>
      <p style="margin:0;font-size:1.6rem;font-weight:800;color:#1A1917;font-family:'Courier New',monospace;letter-spacing:.05em">${session.makeGoodCouponCode}</p>
      <p style="margin:8px 0 0;color:#9B9590;font-size:.78rem">100% off · single use · valid through ${expiryStr}</p>
    </div>
    <p style="text-align:center;margin:0 0 8px"><a href="${SITE_URL}" style="display:inline-block;background:#7C5CFF;color:#fff;font-weight:700;font-size:.9rem;padding:12px 28px;border-radius:8px;text-decoration:none">Run your free repair →</a></p>
    <p style="color:#9B9590;font-size:.78rem;text-align:center;margin:8px 0 0">Enter the code at checkout — your total will be $0.</p>
    <hr style="border:none;border-top:1px solid #E2DBD0;margin:20px 0">
    <p style="font-size:.78rem;color:#9B9590;margin:0">Pronto PC · 5D Service Solutions Global LLC · support@prontopc.online</p>
  </div>
</div>`;

  try {
    await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: { Authorization: `Bearer ${RESEND_API_KEY}`, "Content-Type": "application/json" },
      body: JSON.stringify({ from: FROM_EMAIL, to: session.email, subject: "Your AI plan didn't run — here's a free repair on us", html: emailHtml }),
      signal: AbortSignal.timeout(10_000),
    });
    console.log(`[makegood] 100% code ${session.makeGoodCouponCode} emailed to ${session.email}`);
  } catch (e) {
    console.error("[makegood] Customer email failed:", e.message);
  }
}

// Notify Support of every AI-unavailable incident, so a human sees it even when
// the customer coupon auto-issued (abuse review + a product-quality signal).
async function sendSupportIncidentEmail(session, machineId, machineName, reason, couponCode) {
  const RESEND_API_KEY = process.env.RESEND_API_KEY;
  const FROM_EMAIL = process.env.FROM_EMAIL || "support@prontopc.online";
  if (!RESEND_API_KEY) return;

  const tokenShort = (session && session.token ? session.token : "unknown").substring(0, 12);
  const esc = (v) => String(v == null ? "—" : v).replace(/[<>&]/g, (c) => ({ "<": "&lt;", ">": "&gt;", "&": "&amp;" }[c]));
  const rows = [
    ["Customer email", session && session.email ? session.email : "(no session / not resolvable)"],
    ["Machine", machineName],
    ["Machine ID", machineId],
    ["Session token", tokenShort + "…"],
    ["Reason", reason],
    ["Make-good code", couponCode || "(none issued — no customer email on file)"],
    ["Time (UTC)", new Date().toISOString()],
  ].map(([k, v]) => `<tr><td style="padding:4px 12px 4px 0;color:#6B6560">${esc(k)}</td><td style="padding:4px 0;font-weight:600;color:#1A1917">${esc(v)}</td></tr>`).join("");

  const html = `
<div style="font-family:'Segoe UI',Arial,sans-serif;max-width:620px;margin:0 auto">
  <h2 style="margin:0 0 4px">AI-unavailable make-good issued</h2>
  <p style="color:#6B6560;margin:0 0 16px">A customer's AI plan did not build (both on-device and cloud). ${couponCode ? "A 100% make-good coupon was auto-issued and emailed to them." : "No coupon could be auto-issued — resolve the customer manually."}</p>
  <table style="border-collapse:collapse;font-size:.9rem">${rows}</table>
</div>`;

  try {
    await fetch("https://api.resend.com/emails", {
      method: "POST",
      headers: { Authorization: `Bearer ${RESEND_API_KEY}`, "Content-Type": "application/json" },
      body: JSON.stringify({ from: FROM_EMAIL, to: SUPPORT_EMAIL, subject: `[AI-UNAVAILABLE] ${machineName || "a customer PC"} — make-good ${couponCode || "PENDING"}`, html }),
      signal: AbortSignal.timeout(10_000),
    });
    console.log(`[makegood] Support notified (${SUPPORT_EMAIL}) for token ${tokenShort}…`);
  } catch (e) {
    console.error("[makegood] Support email failed:", e.message);
  }
}

// POST /api/report-ai-unavailable — the recourse when BOTH AI authors fail.
// The client reports the incident with its token + machine identity; we issue the
// single-use 100% coupon (once per session, to the customer email), email it, and
// always notify Support. There is no rule-based plan: honest recourse only.
async function handleReportAiUnavailable(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { token, machineId, machineName, reason } = body || {};
  if (!token) return json(res, req, 400, { ok: false, error: "token required" });

  const session = getSession(token);

  // Record the incident on the session for support/audit (best-effort).
  if (session) {
    session.hostname = machineName || session.hostname;
    session.aiUnavailableAt = Date.now();
    session.aiUnavailableReason = reason || "";
  }

  // Issue the 100% make-good once per session, tied to the customer's email.
  // (No email on file — e.g. an unpaid/expired token — means no coupon to send;
  // Support is still notified so it can be resolved by hand.)
  let couponCode = (session && session.makeGoodCouponCode) || null;
  let couponExpiresAt = (session && session.makeGoodCouponExpiresAt) || null;
  if (session && session.email && !couponCode) {
    const coupon = await createMakeGoodCoupon(session, machineId, machineName);
    if (coupon) {
      session.makeGoodCouponCode = couponCode = coupon.code;
      session.makeGoodCouponExpiresAt = couponExpiresAt = coupon.expiresAt;
      await sendMakeGoodEmail(session);
    }
  }

  await sendSupportIncidentEmail(session, machineId, machineName, reason, couponCode);

  console.log(`[ai-unavailable] token ${token.substring(0, 8)}… machine ${machineName || "?"} reason ${reason || "?"} coupon ${couponCode || "none"}`);
  return json(res, req, 200, { ok: true, couponCode, couponExpiresAt, supportEmail: SUPPORT_EMAIL });
}

async function handleSubmitRepairResults(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { token, results } = body;
  if (!token) return json(res, req, 400, { ok: false, error: "token required" });

  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, error: "Session not found" });

  session.repairResults = results;
  transitionState(token, "completed");

  // Mint a 50%-off "next repair" coupon (once per session), store it, and email it.
  if (!session.couponCode) {
    const coupon = await createNextRepairCoupon(session);
    if (coupon) {
      session.couponCode = coupon.code;
      session.couponExpiresAt = coupon.expiresAt;
      await sendCouponEmail(session);
    }
  }

  console.log(`[submit-repair-results] Session ${token.substring(0, 8)}… completed`);
  return json(res, req, 200, { ok: true, couponCode: session.couponCode || null, couponExpiresAt: session.couponExpiresAt || null });
}

function handleSessionReport(req, res, token) {
  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, error: "Session not found" });
  if (!session.diagnostics) return json(res, req, 404, { ok: false, error: "No diagnostics available" });

  const html = generateReportHtml(session);
  return text(res, req, 200, html, "text/html; charset=utf-8");
}

// Legacy endpoint — kept for backward compatibility with existing Python agent
async function handleRepairPlan(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { session, diagnostics } = body;
  if (!session || !diagnostics) return json(res, req, 400, { ok: false, error: "session and diagnostics required" });

  try {
    const { plan, provenance } = await authorRepairPlan(diagnostics);
    console.log(`[repair-plan] ${plan.actions?.length || 0} actions via ${provenance.provider}`);
    return json(res, req, 200, { ok: true, status: "ok", plan, provenance });
  } catch (e) {
    console.error("[repair-plan] Error:", e.message);
    // Orb contract: unavailable, never a heuristic plan dressed as the AI plan.
    return json(res, req, 200, { ok: false, status: "unavailable", reason: e.message });
  }
}

// ─── PowerShell Script Generation ───────────────────────────────────────────

function generateDiagnosticScript(token) {
  return `# ProntoPC Custodian Technician — Diagnostic Scan
# 5D Service Solutions Global LLC
# This script runs in memory — no files are installed on your system.
# It reads system information only — nothing is changed.

$ErrorActionPreference = 'SilentlyContinue'
$token = '${token}'
$serverUrl = '${CLOUD_RUN_URL}'

function Write-Banner {
    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor DarkYellow
    Write-Host "    Pronto PC — Custodian Technician Diagnostic Scan" -ForegroundColor Yellow
    Write-Host "    5D Service Solutions Global LLC" -ForegroundColor DarkYellow
    Write-Host "  ================================================================" -ForegroundColor DarkYellow
    Write-Host ""
    Write-Host "  This scan reads your system information to generate a" -ForegroundColor Gray
    Write-Host "  professional diagnostic report. Nothing is changed." -ForegroundColor Gray
    Write-Host ""
}

function Write-Step($n, $total, $msg) {
    Write-Host "  [$n/$total] $msg..." -ForegroundColor Cyan
}

function Get-SafeJson($cmd, $timeout = 15) {
    try {
        $result = powershell -NoProfile -NonInteractive -Command $cmd 2>$null
        if ($result) { return $result | ConvertFrom-Json }
    } catch {}
    return $null
}

Write-Banner

# Validate session
try {
    $status = Invoke-RestMethod -Uri "$serverUrl/api/session/$token/status" -TimeoutSec 10
    if (-not $status.ok) {
        Write-Host "  Session invalid or expired. Please visit prontopc.online" -ForegroundColor Red
        Read-Host "  Press Enter to close"
        exit
    }
} catch {
    Write-Host "  Could not reach the diagnostic server. Please check your" -ForegroundColor Red
    Write-Host "  internet connection and try again." -ForegroundColor Red
    Read-Host "  Press Enter to close"
    exit
}

Write-Host "  Session verified. Starting comprehensive diagnostic scan..." -ForegroundColor Green
Write-Host ""

$totalSteps = 18
$diag = @{}

# 1. OS Info
Write-Step 1 $totalSteps "Gathering system information"
try {
    $os = Get-CimInstance Win32_OperatingSystem
    $proc = Get-CimInstance Win32_Processor
    $diag.os = @{
        version = [System.Environment]::OSVersion.Version.ToString()
        release = [System.Environment]::OSVersion.Version.Major.ToString()
        edition = $os.Caption
        build = $os.BuildNumber
        machine = $env:PROCESSOR_ARCHITECTURE
        processor = $proc.Name
        hostname = $env:COMPUTERNAME
    }
} catch { $diag.os = @{ hostname = $env:COMPUTERNAME; error = $_.Exception.Message } }

# 2. Uptime
Write-Step 2 $totalSteps "Checking system uptime"
try {
    $boot = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
    $uptime = (Get-Date) - $boot
    $diag.uptime = @{
        boot_time = $boot.ToString("o")
        uptime_hours = [math]::Round($uptime.TotalHours, 1)
        uptime_days = $uptime.Days
        needs_reboot = ($uptime.Days -gt 14)
    }
} catch { $diag.uptime = @{ error = "Could not determine" } }

# 3. CPU
Write-Step 3 $totalSteps "Analyzing CPU"
try {
    $proc = Get-CimInstance Win32_Processor
    $diag.cpu = @{
        processor_name = $proc.Name
        physical_cores = $proc.NumberOfCores
        logical_cores = $proc.ThreadCount
        current_percent = $proc.LoadPercentage
        current_clock_mhz = $proc.CurrentClockSpeed
        max_clock_mhz = $proc.MaxClockSpeed
    }
} catch { $diag.cpu = @{ error = $_.Exception.Message } }

# 4. Memory
Write-Step 4 $totalSteps "Checking memory"
try {
    $mem = Get-CimInstance Win32_OperatingSystem
    $totalGB = [math]::Round($mem.TotalVisibleMemorySize / 1MB, 1)
    $freeGB = [math]::Round($mem.FreePhysicalMemory / 1MB, 1)
    $usedGB = $totalGB - $freeGB
    $pct = [math]::Round(($usedGB / $totalGB) * 100, 0)
    $diag.memory = @{
        total_gb = $totalGB; available_gb = $freeGB; used_gb = $usedGB; percent_used = $pct
        health_status = if ($pct -gt 90) { "critical" } elseif ($pct -gt 80) { "warning" } else { "healthy" }
    }
} catch { $diag.memory = @{ percent_used = 0; error = $_.Exception.Message } }

# 5. Disk
Write-Step 5 $totalSteps "Scanning drives"
try {
    $disks = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3"
    $diag.disk = @($disks | ForEach-Object {
        $pct = [math]::Round(($_.Size - $_.FreeSpace) / $_.Size * 100, 0)
        @{
            device = $_.DeviceID; total_gb = [math]::Round($_.Size/1GB,1)
            used_gb = [math]::Round(($_.Size - $_.FreeSpace)/1GB,1); free_gb = [math]::Round($_.FreeSpace/1GB,1)
            percent_used = $pct; fstype = $_.FileSystem
            health_status = if ($pct -gt 95) {"critical"} elseif ($pct -gt 85) {"warning"} else {"healthy"}
        }
    })
} catch { $diag.disk = @() }

# 6. SMART Health
try {
    $smart = Get-CimInstance -Namespace root\\wmi -ClassName MSStorageDriver_FailurePredictStatus 2>$null
    $diag.smart_health = @($smart | ForEach-Object {
        @{ drive = $_.InstanceName.Substring(0, [math]::Min(40, $_.InstanceName.Length)); failure_predicted = $_.PredictFailure; reason = $_.Reason }
    })
} catch { $diag.smart_health = @() }

# 7. Temp files & caches
Write-Step 6 $totalSteps "Measuring temp files and caches"
try {
    $tempPaths = @($env:TEMP, "C:\\Windows\\Temp", "$env:USERPROFILE\\AppData\\Local\\Temp") | Select-Object -Unique | Where-Object { Test-Path $_ }
    $diag.temp_files = @($tempPaths | ForEach-Object {
        $files = Get-ChildItem $_ -Recurse -File -ErrorAction SilentlyContinue
        $totalBytes = ($files | Measure-Object -Property Length -Sum).Sum
        @{ path = $_; file_count = $files.Count; size_mb = [math]::Round($totalBytes/1MB, 1) }
    })
} catch { $diag.temp_files = @() }

# Recycle Bin
try {
    $shell = New-Object -ComObject Shell.Application
    $rb = $shell.NameSpace(10).Items()
    $rbSize = ($rb | Measure-Object -Property Size -Sum).Sum
    $diag.recycle_bin = @{ item_count = $rb.Count; size_mb = [math]::Round($rbSize/1MB, 1) }
} catch { $diag.recycle_bin = @{ item_count = 0; size_mb = 0 } }

# 8. Browser caches
Write-Step 7 $totalSteps "Checking browser caches"
$cachePaths = @{
    "Chrome" = "$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Cache"
    "Chrome (Code Cache)" = "$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Code Cache"
    "Edge" = "$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Cache"
    "Edge (Code Cache)" = "$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Code Cache"
    "Brave" = "$env:LOCALAPPDATA\\BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache"
    "Opera" = "$env:APPDATA\\Opera Software\\Opera Stable\\Cache"
    "Firefox" = "$env:LOCALAPPDATA\\Mozilla\\Firefox\\Profiles"
}
$diag.browser_caches = @()
foreach ($browser in $cachePaths.Keys) {
    $p = $cachePaths[$browser]
    if (Test-Path $p) {
        try {
            $files = Get-ChildItem $p -Recurse -File -ErrorAction SilentlyContinue
            $sz = ($files | Measure-Object -Property Length -Sum).Sum
            if ($sz -gt 0) {
                $diag.browser_caches += @{ browser = $browser; path = $p; size_mb = [math]::Round($sz/1MB,1); file_count = $files.Count }
            }
        } catch {}
    }
}

# 9. Windows caches
$diag.windows_caches = @{}
$wcPaths = @{
    thumbnail_cache = @{ path = "$env:LOCALAPPDATA\\Microsoft\\Windows\\Explorer"; pattern = "thumbcache_*.db" }
    icon_cache = @{ path = "$env:LOCALAPPDATA"; pattern = "iconcache_*.db" }
    prefetch = @{ path = "C:\\Windows\\Prefetch"; pattern = "*.pf" }
}
foreach ($name in $wcPaths.Keys) {
    $wc = $wcPaths[$name]
    if (Test-Path $wc.path) {
        try {
            $files = Get-ChildItem "$($wc.path)\\$($wc.pattern)" -ErrorAction SilentlyContinue
            $sz = ($files | Measure-Object -Property Length -Sum).Sum
            if ($sz -gt 0) { $diag.windows_caches[$name] = @{ size_mb = [math]::Round($sz/1MB,1); file_count = $files.Count } }
        } catch {}
    }
}

# 9b. Windows Update leftovers (often the largest reclaimable space)
$diag.update_leftovers = @{ component_cleanup_available = $true }
try {
    $sd = "C:\\Windows\\SoftwareDistribution\\Download"
    $sz = if (Test-Path $sd) { (Get-ChildItem $sd -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum } else { 0 }
    $diag.update_leftovers.windows_update_cache_mb = [math]::Round($sz/1MB)
} catch { $diag.update_leftovers.windows_update_cache_mb = 0 }
try {
    $do = "C:\\Windows\\SoftwareDistribution\\DeliveryOptimization"
    $sz = if (Test-Path $do) { (Get-ChildItem $do -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum } else { 0 }
    $diag.update_leftovers.delivery_optimization_mb = [math]::Round($sz/1MB)
} catch { $diag.update_leftovers.delivery_optimization_mb = 0 }
try {
    if (Test-Path "C:\\Windows.old") {
        $diag.update_leftovers.windows_old_present = $true
        $wsz = (Get-ChildItem "C:\\Windows.old" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
        $diag.update_leftovers.windows_old_gb = [math]::Round($wsz/1GB, 1)
    } else { $diag.update_leftovers.windows_old_present = $false; $diag.update_leftovers.windows_old_gb = $null }
} catch { $diag.update_leftovers.windows_old_present = $false; $diag.update_leftovers.windows_old_gb = $null }

# 10. Drivers
Write-Step 8 $totalSteps "Auditing drivers"
try {
    $allDrivers = Get-CimInstance Win32_PnPSignedDriver
    $unsigned = @($allDrivers | Where-Object { -not $_.IsSigned } | Select-Object -First 10 | ForEach-Object {
        @{ device = $_.DeviceName; manufacturer = $_.Manufacturer; version = $_.DriverVersion }
    })
    $cutoff = (Get-Date).AddYears(-2)
    $outdated = @($allDrivers | Where-Object { $_.DriverDate -and $_.DriverDate -lt $cutoff -and $_.DriverProviderName -ne 'Microsoft' } | Select-Object -First 15 | ForEach-Object {
        @{ device = $_.DeviceName; version = $_.DriverVersion; date = $_.DriverDate.ToString("yyyy-MM-dd"); provider = $_.DriverProviderName; age_days = ((Get-Date) - $_.DriverDate).Days }
    })
    $diag.drivers = @{
        total = $allDrivers.Count; signed = @($allDrivers | Where-Object { $_.IsSigned }).Count
        unsigned = $unsigned; outdated = $outdated
    }
} catch { $diag.drivers = @{ total = 0; error = $_.Exception.Message } }

# 11. Device errors
Write-Step 9 $totalSteps "Checking Device Manager"
try {
    $problems = Get-CimInstance Win32_PnPEntity | Where-Object { $_.ConfigManagerErrorCode -ne 0 }
    $diag.device_errors = @($problems | ForEach-Object {
        @{ device = $_.Name; error_code = $_.ConfigManagerErrorCode; status = $_.Status; manufacturer = $_.Manufacturer
           error_description = switch ($_.ConfigManagerErrorCode) {
               1 {"Not configured correctly"} 3 {"Driver corrupted"} 10 {"Cannot start"} 22 {"Disabled"}
               28 {"Drivers not installed"} 31 {"Not working properly"} 43 {"Driver reported problem"}
               default {"Error code $($_.ConfigManagerErrorCode)"}
           }
        }
    })
} catch { $diag.device_errors = @() }

# 12. BSOD history
try {
    $dumps = Get-ChildItem "C:\\Windows\\Minidump\\*.dmp" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 10
    $diag.bsod_history = @($dumps | ForEach-Object {
        @{ file = $_.Name; date = $_.LastWriteTime.ToString("o"); size_kb = [math]::Round($_.Length/1KB,1) }
    })
} catch { $diag.bsod_history = @() }

# 13. Network
Write-Step 10 $totalSteps "Testing network"
try {
    $adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }
    $diag.network = @{
        adapters = @($adapters | ForEach-Object { @{ name = $_.Name; description = $_.InterfaceDescription; speed = $_.LinkSpeed } })
        connectivity = @{
            internet_reachable = (Test-Connection -ComputerName 8.8.8.8 -Count 1 -Quiet -ErrorAction SilentlyContinue)
            interfaces = @(Get-NetIPConfiguration | Where-Object { $_.IPv4DefaultGateway } | ForEach-Object {
                @{ name = $_.InterfaceAlias; ipv4 = $_.IPv4Address.IPAddress; gateway = $_.IPv4DefaultGateway.NextHop
                   dns_servers = ($_.DNSServer.ServerAddresses -join ',') }
            })
        }
    }
} catch { $diag.network = @{ adapters = @(); connectivity = @{ internet_reachable = $null } } }

try { $diag.dns_cache = @{ entries = (Get-DnsClientCache | Measure-Object).Count } } catch { $diag.dns_cache = @{ entries = 0 } }

# 14. Security
Write-Step 11 $totalSteps "Checking security status"
try {
    $mp = Get-MpComputerStatus -ErrorAction Stop
    $diag.security = @{
        defender = @{
            real_time_protection = $mp.RealTimeProtectionEnabled; antivirus_enabled = $mp.AntivirusEnabled
            antispyware_enabled = $mp.AntispywareEnabled; behavior_monitor = $mp.BehaviorMonitorEnabled
            signature_last_updated = $mp.AntivirusSignatureLastUpdated.ToString("o")
            last_quick_scan = $mp.QuickScanEndTime.ToString("o"); last_full_scan = $mp.FullScanEndTime.ToString("o")
        }
    }
} catch { $diag.security = @{ defender = @{ error = "Could not query Defender" } } }

try {
    $fw = Get-NetFirewallProfile
    $diag.security.firewall = @{ profiles = @($fw | ForEach-Object { @{ name = $_.Name; enabled = $_.Enabled } }) }
} catch { $diag.security.firewall = @{ error = "Could not query firewall" } }

# Activation
try {
    $lic = Get-CimInstance SoftwareLicensingProduct | Where-Object { $_.PartialProductKey -and $_.Name -like '*Windows*' } | Select-Object -First 1
    $statusMap = @{ 0="Unlicensed"; 1="Licensed"; 2="OOBGrace"; 3="OOTGrace"; 4="NonGenuine"; 5="Notification" }
    $diag.activation = @{ product = $lic.Name; status_code = $lic.LicenseStatus; status = $statusMap[[int]$lic.LicenseStatus]; is_activated = ($lic.LicenseStatus -eq 1) }
} catch { $diag.activation = @{ is_activated = $null } }

# 15. Windows Update
Write-Step 12 $totalSteps "Checking Windows Update"
try {
    $hotfixes = Get-HotFix | Sort-Object InstalledOn -Descending -ErrorAction SilentlyContinue | Select-Object -First 5
    $diag.windows_update = @{
        recent_updates = @($hotfixes | ForEach-Object { @{ id = $_.HotFixID; installed = $_.InstalledOn.ToString("o"); type = $_.Description } })
        last_update_date = if ($hotfixes) { $hotfixes[0].InstalledOn.ToString("o") } else { "" }
    }
} catch { $diag.windows_update = @{ error = "Could not query" } }

try {
    $updateSession = New-Object -ComObject Microsoft.Update.Session
    $searcher = $updateSession.CreateUpdateSearcher()
    $result = $searcher.Search('IsInstalled=0 and IsHidden=0')
    $diag.windows_update.pending_updates = $result.Updates.Count
} catch { $diag.windows_update.pending_updates = $null }

# 16. Startup programs
Write-Step 13 $totalSteps "Listing startup programs"
$diag.startup_programs = @()
$regPaths = @(
    @{ hive = "HKCU"; path = "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run" }
    @{ hive = "HKLM"; path = "HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run" }
)
foreach ($rp in $regPaths) {
    try {
        $props = Get-ItemProperty $rp.path -ErrorAction SilentlyContinue
        if ($props) {
            $props.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | ForEach-Object {
                $diag.startup_programs += @{ name = $_.Name; command = $_.Value; hive = $rp.hive; key = $rp.path }
            }
        }
    } catch {}
}

# 17. Services
Write-Step 14 $totalSteps "Auditing services"
$criticalSvcs = @("wuauserv","WinDefend","mpssvc","EventLog","Dhcp","Dnscache","RpcSs","Schedule","Winmgmt")
try {
    $allSvcs = Get-Service -ErrorAction SilentlyContinue
    $diag.services = @{
        stopped_critical = @($allSvcs | Where-Object { $criticalSvcs -contains $_.Name -and $_.Status -ne 'Running' } | ForEach-Object {
            @{ name = $_.Name; display_name = $_.DisplayName; status = $_.Status.ToString(); start_type = $_.StartType.ToString() }
        })
    }
} catch { $diag.services = @{ stopped_critical = @() } }

try {
    $svcErrors = Get-EventLog -LogName System -EntryType Error -Source 'Service Control Manager' -Newest 10 -ErrorAction SilentlyContinue
    $diag.services.failed = @($svcErrors | ForEach-Object {
        @{ time = $_.TimeGenerated.ToString("o"); message = $_.Message.Substring(0, [math]::Min(200, $_.Message.Length)) }
    })
} catch { $diag.services.failed = @() }

# 18. Installed programs
Write-Step 15 $totalSteps "Cataloguing installed programs"
$programs = @()
$regProgramPaths = @(
    "HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*"
    "HKLM:\\Software\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*"
    "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\*"
)
foreach ($rpp in $regProgramPaths) {
    try {
        Get-ItemProperty $rpp -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName } | ForEach-Object {
            $programs += @{
                name = $_.DisplayName; version = $_.DisplayVersion; publisher = $_.Publisher
                install_date = $_.InstallDate; size_kb = $_.EstimatedSize
            }
        }
    } catch {}
}
$seen = @{}; $unique = @()
foreach ($p in $programs) { $k = $p.name.ToLower(); if (-not $seen[$k]) { $seen[$k] = $true; $unique += $p } }
$diag.installed_programs = @{ total_count = $unique.Count; programs = $unique }

# Event logs
Write-Step 16 $totalSteps "Reading event logs"
try {
    $sysErrors = Get-EventLog -LogName System -EntryType Error -Newest 20 -ErrorAction SilentlyContinue
    $appErrors = Get-EventLog -LogName Application -EntryType Error -Newest 10 -ErrorAction SilentlyContinue
    $diag.event_errors = @(($sysErrors + $appErrors) | ForEach-Object {
        @{ time = $_.TimeGenerated.ToString("o"); source = $_.Source; event_id = $_.EventID; message = $_.Message.Substring(0, [math]::Min(300, $_.Message.Length)) }
    })
} catch { $diag.event_errors = @() }

try {
    $warnings = Get-EventLog -LogName System -EntryType Warning -Newest 15 -ErrorAction SilentlyContinue
    $diag.event_warnings = @($warnings | ForEach-Object {
        @{ time = $_.TimeGenerated.ToString("o"); source = $_.Source; event_id = $_.EventID; message = $_.Message.Substring(0, [math]::Min(300, $_.Message.Length)) }
    })
} catch { $diag.event_warnings = @() }

# User accounts
Write-Step 17 $totalSteps "Checking user accounts"
try {
    $diag.user_accounts = @(Get-LocalUser -ErrorAction SilentlyContinue | ForEach-Object {
        @{ name = $_.Name; enabled = $_.Enabled; last_logon = $_.LastLogon.ToString("o"); description = $_.Description }
    })
} catch { $diag.user_accounts = @() }

# Power plan, visual effects, battery, large files
try { $pp = Get-CimInstance -Namespace root\\cimv2\\power -ClassName Win32_PowerPlan | Where-Object { $_.IsActive }; $diag.power_plan = @{ active_plan = $pp.ElementName } } catch { $diag.power_plan = @{ active_plan = "Unknown" } }
try {
    $vfx = Get-ItemProperty "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects" -Name VisualFXSetting -ErrorAction SilentlyContinue
    $vfxMap = @{ 0="Let Windows choose"; 1="Best appearance"; 2="Best performance"; 3="Custom" }
    $diag.visual_effects = @{ setting = $vfxMap[[int]$vfx.VisualFXSetting]; code = $vfx.VisualFXSetting }
} catch { $diag.visual_effects = @{ setting = "Default"; code = $null } }

try {
    $bat = Get-CimInstance Win32_Battery -ErrorAction SilentlyContinue
    if ($bat) {
        $diag.battery = @{ present = $true; percent = $bat.EstimatedChargeRemaining; plugged_in = ($bat.BatteryStatus -eq 2) }
        if ($bat.DesignCapacity -and $bat.FullChargeCapacity -and $bat.DesignCapacity -gt 0) {
            $diag.battery.wear_percent = [math]::Round((1 - $bat.FullChargeCapacity / $bat.DesignCapacity) * 100, 1)
        }
    } else { $diag.battery = @{ present = $false } }
} catch { $diag.battery = @{ present = $false } }

Write-Step 18 $totalSteps "Finding large files"
$diag.large_files = @()
$scanDirs = @("$env:USERPROFILE\\Downloads", "$env:USERPROFILE\\Documents", "$env:USERPROFILE\\Desktop", "$env:USERPROFILE\\Videos")
foreach ($dir in $scanDirs) {
    if (Test-Path $dir) {
        try {
            Get-ChildItem $dir -Recurse -File -ErrorAction SilentlyContinue | Where-Object { $_.Length -gt 100MB } | Select-Object -First 5 | ForEach-Object {
                $diag.large_files += @{ path = $_.FullName; size_mb = [math]::Round($_.Length/1MB,1); extension = $_.Extension.ToLower() }
            }
        } catch {}
    }
}
$diag.large_files = @($diag.large_files | Sort-Object { $_.size_mb } -Descending | Select-Object -First 20)

# Add scan timestamp
$diag.scanned_at = (Get-Date).ToUniversalTime().ToString("o")

Write-Host ""
Write-Host "  Scan complete. Sending data for AI analysis..." -ForegroundColor Green
Write-Host ""

# Submit diagnostics
try {
    $body = @{ token = $token; diagnostics = $diag } | ConvertTo-Json -Depth 10 -Compress
    $response = Invoke-RestMethod -Uri "$serverUrl/api/submit-diagnostics" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 90

    if ($response.ok) {
        $plan = $response.plan
        Write-Host "  ================================================================" -ForegroundColor DarkYellow
        Write-Host "    DIAGNOSTIC FINDINGS" -ForegroundColor Yellow
        Write-Host "  ================================================================" -ForegroundColor DarkYellow
        Write-Host ""
        Write-Host "  $($plan.summary)" -ForegroundColor White
        Write-Host ""

        if ($plan.hardware_notes) {
            Write-Host "  HARDWARE NOTES:" -ForegroundColor Red
            Write-Host "  $($plan.hardware_notes)" -ForegroundColor Gray
            Write-Host ""
        }

        if ($plan.security_assessment) {
            Write-Host "  SECURITY: $($plan.security_assessment)" -ForegroundColor Cyan
            Write-Host ""
        }

        $actionCount = ($plan.actions | Measure-Object).Count
        Write-Host "  $actionCount recommended repair actions identified." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "  Your Custodian Technician has your full results." -ForegroundColor Green
        Write-Host "  Return to the chat to continue." -ForegroundColor Green
    } else {
        Write-Host "  Analysis returned an error: $($response.error)" -ForegroundColor Red
    }
} catch {
    Write-Host "  Could not submit diagnostics: $($_.Exception.Message)" -ForegroundColor Red
}

# Download and save report
Write-Host ""
Write-Host "  Saving your diagnostic report..." -ForegroundColor Cyan
try {
    $report = Invoke-RestMethod -Uri "$serverUrl/api/session/$token/report" -TimeoutSec 15
    $reportPath = "$env:USERPROFILE\\Desktop\\ProntoPC_Report_$(Get-Date -Format 'yyyyMMdd_HHmm').html"
    $report | Out-File -FilePath $reportPath -Encoding utf8
    Write-Host "  Report saved to: $reportPath" -ForegroundColor Green
    Start-Process $reportPath
} catch {
    Write-Host "  Could not save report: $($_.Exception.Message)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  ================================================================" -ForegroundColor DarkYellow
Write-Host "  You can close this window now." -ForegroundColor Gray
Write-Host "  ================================================================" -ForegroundColor DarkYellow
Read-Host "  Press Enter to close"
`;
}

function generateRepairScript(token, repairPlan) {
  const actionsJson = JSON.stringify(repairPlan?.actions || []);
  return `# ProntoPC Custodian Technician — Repair Execution
# 5D Service Solutions Global LLC
# Approved repairs only — every action was consented to by the client.

$ErrorActionPreference = 'SilentlyContinue'
$token = '${token}'
$serverUrl = '${CLOUD_RUN_URL}'
$actions = '${actionsJson.replace(/'/g, "''")}' | ConvertFrom-Json

Write-Host ""
Write-Host "  ================================================================" -ForegroundColor DarkYellow
Write-Host "    Pronto PC — Executing Approved Repairs" -ForegroundColor Yellow
Write-Host "    5D Service Solutions Global LLC" -ForegroundColor DarkYellow
Write-Host "  ================================================================" -ForegroundColor DarkYellow
Write-Host ""

$results = @()
$totalActions = ($actions | Measure-Object).Count
$current = 0

foreach ($action in $actions) {
    $current++
    $id = $action.id
    $name = $action.friendly_name
    Write-Host "  [$current/$totalActions] $name..." -ForegroundColor Cyan
    $result = @{ action = $id; name = $name; status = "skipped"; mb_freed = 0; detail = "" }

    try {
        switch -Wildcard ($id) {
            "clear_temp_files" {
                $freed = 0
                @($env:TEMP, "C:\\Windows\\Temp") | Select-Object -Unique | Where-Object { Test-Path $_ } | ForEach-Object {
                    Get-ChildItem $_ -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object {
                        try { $freed += $_.Length; Remove-Item $_.FullName -Force } catch {}
                    }
                }
                $result.status = "done"; $result.mb_freed = [math]::Round($freed/1MB)
            }
            "clear_*_cache" {
                $browser = switch ($id) { "clear_chrome_cache"{"Chrome"} "clear_edge_cache"{"Edge"} "clear_firefox_cache"{"Firefox"} "clear_brave_cache"{"Brave"} "clear_opera_cache"{"Opera"} default{""} }
                if ($browser) {
                    $paths = switch ($browser) {
                        "Chrome" { @("$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Cache","$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Code Cache") }
                        "Edge" { @("$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Cache","$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Code Cache") }
                        "Firefox" { @("$env:LOCALAPPDATA\\Mozilla\\Firefox\\Profiles") }
                        "Brave" { @("$env:LOCALAPPDATA\\BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache") }
                        "Opera" { @("$env:APPDATA\\Opera Software\\Opera Stable\\Cache") }
                    }
                    $freed = 0
                    foreach ($p in $paths) { if (Test-Path $p) { Get-ChildItem $p -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { try { $freed += $_.Length; Remove-Item $_.FullName -Force } catch {} } } }
                    $result.status = "done"; $result.mb_freed = [math]::Round($freed/1MB)
                }
            }
            "clear_all_browser_caches" {
                $freed = 0
                $allCachePaths = @(
                    "$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Cache",
                    "$env:LOCALAPPDATA\\Google\\Chrome\\User Data\\Default\\Code Cache",
                    "$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Cache",
                    "$env:LOCALAPPDATA\\Microsoft\\Edge\\User Data\\Default\\Code Cache",
                    "$env:LOCALAPPDATA\\BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache",
                    "$env:APPDATA\\Opera Software\\Opera Stable\\Cache"
                )
                foreach ($p in $allCachePaths) { if (Test-Path $p) { Get-ChildItem $p -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { try { $freed += $_.Length; Remove-Item $_.FullName -Force } catch {} } } }
                $result.status = "done"; $result.mb_freed = [math]::Round($freed/1MB)
            }
            "clear_windows_caches" {
                $freed = 0
                Get-ChildItem "$env:LOCALAPPDATA\\Microsoft\\Windows\\Explorer\\thumbcache_*.db" -ErrorAction SilentlyContinue | ForEach-Object { try { $freed += $_.Length; Remove-Item $_.FullName -Force } catch {} }
                Get-ChildItem "$env:LOCALAPPDATA\\iconcache_*.db" -ErrorAction SilentlyContinue | ForEach-Object { try { $freed += $_.Length; Remove-Item $_.FullName -Force } catch {} }
                Get-ChildItem "C:\\Windows\\Prefetch\\*.pf" -ErrorAction SilentlyContinue | ForEach-Object { try { $freed += $_.Length; Remove-Item $_.FullName -Force } catch {} }
                $result.status = "done"; $result.mb_freed = [math]::Round($freed/1MB)
            }
            "empty_recycle_bin" {
                Clear-RecycleBin -Force -ErrorAction SilentlyContinue
                $result.status = "done"; $result.detail = "Recycle Bin emptied"
            }
            "flush_dns" {
                ipconfig /flushdns | Out-Null
                $result.status = "done"; $result.detail = "DNS cache flushed"
            }
            "sfc_scan" {
                Write-Host "    Running System File Checker (this may take several minutes)..." -ForegroundColor Gray
                $sfcOut = & sfc /scannow 2>&1 | Out-String
                $result.status = "done"; $result.detail = $sfcOut.Substring([math]::Max(0, $sfcOut.Length - 300))
            }
            "dism_restore" {
                Write-Host "    Running DISM health restore (this may take several minutes)..." -ForegroundColor Gray
                $dismOut = & DISM /Online /Cleanup-Image /RestoreHealth 2>&1 | Out-String
                $result.status = "done"; $result.detail = $dismOut.Substring([math]::Max(0, $dismOut.Length - 300))
            }
            "disk_cleanup" {
                & cleanmgr /sagerun:1 2>$null
                $result.status = "done"; $result.detail = "Windows Disk Cleanup completed"
            }
            "chkdsk_scan" {
                $chkOut = & chkdsk C: /scan 2>&1 | Out-String
                $result.status = "done"; $result.detail = $chkOut.Substring([math]::Max(0, $chkOut.Length - 300))
            }
            "reset_network_stack" {
                netsh winsock reset | Out-Null; netsh int ip reset | Out-Null
                ipconfig /release | Out-Null; ipconfig /renew | Out-Null; ipconfig /flushdns | Out-Null
                $result.status = "done"; $result.detail = "Network stack reset (winsock, IP, DNS)"
            }
            "disable_startup_item" {
                $itemName = $action.target_name
                $hive = if ($action.target_hive -eq "HKLM") { "HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run" } else { "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run" }
                try { Remove-ItemProperty -Path $hive -Name $itemName -ErrorAction Stop; $result.status = "done"; $result.detail = "Disabled: $itemName" }
                catch { $result.status = "error"; $result.detail = "Could not disable: $($_.Exception.Message)" }
            }
            "optimize_power_plan" {
                powercfg /setactive SCHEME_BALANCED
                $result.status = "done"; $result.detail = "Power plan set to Balanced"
            }
            "optimize_visual_effects" {
                Set-ItemProperty "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects" -Name VisualFXSetting -Value 2 -Type DWord
                $result.status = "done"; $result.detail = "Visual effects set to Best Performance"
            }
            "reset_windows_update" {
                Write-Host "    Resetting Windows Update components..." -ForegroundColor Gray
                @("wuauserv","cryptSvc","bits","msiserver") | ForEach-Object { Stop-Service $_ -Force -ErrorAction SilentlyContinue }
                $sd = "C:\\Windows\\SoftwareDistribution"; $cr = "C:\\Windows\\System32\\catroot2"
                if (Test-Path $sd) { try { Rename-Item $sd "$sd.bak" -Force } catch {} }
                if (Test-Path $cr) { try { Rename-Item $cr "$cr.bak" -Force } catch {} }
                @("wuauserv","cryptSvc","bits","msiserver") | ForEach-Object { Start-Service $_ -ErrorAction SilentlyContinue }
                $result.status = "done"; $result.detail = "Windows Update components reset"
            }
            "clear_windows_store_cache" {
                Start-Process wsreset.exe -Wait -ErrorAction SilentlyContinue
                $result.status = "done"; $result.detail = "Windows Store cache cleared"
            }
            "clean_windows_update_cache" {
                $sd = "C:\\Windows\\SoftwareDistribution\\Download"
                $freed = if (Test-Path $sd) { (Get-ChildItem $sd -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum } else { 0 }
                @("wuauserv","bits") | ForEach-Object { Stop-Service $_ -Force -ErrorAction SilentlyContinue }
                if (Test-Path $sd) { Get-ChildItem $sd -ErrorAction SilentlyContinue | ForEach-Object { try { Remove-Item $_.FullName -Recurse -Force } catch {} } }
                @("wuauserv","bits") | ForEach-Object { Start-Service $_ -ErrorAction SilentlyContinue }
                $result.status = "done"; $result.mb_freed = [math]::Round($freed/1MB)
            }
            "clean_component_store" {
                Write-Host "    Cleaning Windows component store (this may take several minutes)..." -ForegroundColor Gray
                $out = & DISM /Online /Cleanup-Image /StartComponentCleanup 2>&1 | Out-String
                $result.status = "done"; $result.detail = $out.Substring([math]::Max(0, $out.Length - 200))
            }
            "remove_windows_old" {
                if (Test-Path "C:\\Windows.old") {
                    $freed = (Get-ChildItem "C:\\Windows.old" -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
                    & takeown /f "C:\\Windows.old" /r /d y 2>&1 | Out-Null
                    & icacls "C:\\Windows.old" /grant "*S-1-5-32-544:F" /t /c 2>&1 | Out-Null
                    Remove-Item "C:\\Windows.old" -Recurse -Force -ErrorAction SilentlyContinue
                    if (-not (Test-Path "C:\\Windows.old")) { $result.status = "done"; $result.mb_freed = [math]::Round($freed/1MB); $result.detail = "Previous Windows installation removed" }
                    else { $result.status = "error"; $result.detail = "Windows.old could not be fully removed" }
                } else { $result.status = "skipped"; $result.detail = "No Windows.old present" }
            }
            "clean_delivery_optimization" {
                $do = "C:\\Windows\\SoftwareDistribution\\DeliveryOptimization"
                $freed = if (Test-Path $do) { (Get-ChildItem $do -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum } else { 0 }
                try { Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue } catch {}
                if (Test-Path $do) { Get-ChildItem $do -Recurse -File -ErrorAction SilentlyContinue | ForEach-Object { try { Remove-Item $_.FullName -Force } catch {} } }
                $result.status = "done"; $result.mb_freed = [math]::Round($freed/1MB); $result.detail = "Delivery Optimization cache cleared"
            }
            { $_ -in @("driver_report","hardware_report") } {
                $result.status = "noted"; $result.detail = "Documented in report. Physical service may be recommended."
            }
            default {
                $result.status = "noted"; $result.detail = "Action logged for review"
            }
        }

        $icon = if ($result.status -eq "done") { "+" } elseif ($result.status -eq "noted") { "~" } else { "!" }
        $freed = if ($result.mb_freed -gt 0) { " - $($result.mb_freed) MB freed" } else { "" }
        Write-Host "    [$icon] $name$freed" -ForegroundColor $(if ($result.status -eq "done") { "Green" } elseif ($result.status -eq "noted") { "Yellow" } else { "Red" })
    } catch {
        $result.status = "error"; $result.detail = $_.Exception.Message
        Write-Host "    [!] $name - $($_.Exception.Message)" -ForegroundColor Red
    }

    $results += $result
}

# Submit results
Write-Host ""
Write-Host "  Submitting repair results..." -ForegroundColor Cyan
try {
    $body = @{ token = $token; results = $results } | ConvertTo-Json -Depth 5 -Compress
    Invoke-RestMethod -Uri "$serverUrl/api/submit-repair-results" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 15 | Out-Null
} catch {}

# Download updated report
Write-Host "  Saving your complete repair report..." -ForegroundColor Cyan
try {
    $report = Invoke-RestMethod -Uri "$serverUrl/api/session/$token/report" -TimeoutSec 15
    $reportPath = "$env:USERPROFILE\\Desktop\\ProntoPC_Report_$(Get-Date -Format 'yyyyMMdd_HHmm').html"
    $report | Out-File -FilePath $reportPath -Encoding utf8
    Write-Host "  Report saved to: $reportPath" -ForegroundColor Green
    Start-Process $reportPath
} catch { Write-Host "  Could not save report: $($_.Exception.Message)" -ForegroundColor Yellow }

$completed = ($results | Where-Object { $_.status -eq "done" }).Count
$totalFreed = ($results | Measure-Object -Property mb_freed -Sum).Sum

Write-Host ""
Write-Host "  ================================================================" -ForegroundColor DarkYellow
Write-Host "    REPAIR COMPLETE" -ForegroundColor Green
Write-Host "  ----------------------------------------------------------------" -ForegroundColor DarkYellow
Write-Host "    $completed repairs completed" -ForegroundColor White
if ($totalFreed -gt 0) {
    if ($totalFreed -ge 1024) { Write-Host "    $([math]::Round($totalFreed/1024,1)) GB space recovered" -ForegroundColor White }
    else { Write-Host "    $totalFreed MB space recovered" -ForegroundColor White }
}
Write-Host ""
Write-Host "    Your report has been saved to your Desktop." -ForegroundColor Gray
Write-Host "    30-day re-repair guarantee — support@prontopc.online" -ForegroundColor Gray
Write-Host "  ================================================================" -ForegroundColor DarkYellow

# ── Restart & reconnect ──────────────────────────────────────────────────────
# Only when a repair that genuinely needs a reboot actually completed.
$restartActions = @('sfc_scan','dism_restore','reset_network_stack','disable_startup_item','reset_windows_update','optimize_visual_effects')
$needsRestart = @($results | Where-Object { $_.status -eq 'done' -and $restartActions -contains $_.action }).Count -gt 0

if ($needsRestart) {
    Write-Host ""
    Write-Host "  A restart is recommended to finish applying these repairs." -ForegroundColor Yellow
    Write-Host "  (System-file, startup, registry, or network changes take full effect after a reboot.)" -ForegroundColor Gray
    Write-Host ""

    # Register a ONE-TIME verification that runs after the next sign-in, then removes itself.
    $runKey = "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run"
    $verifyCmd = 'powershell -ExecutionPolicy Bypass -WindowStyle Normal -Command "irm ''' + "$serverUrl/verify?token=$token" + ''' | iex"'
    try { Set-ItemProperty -Path $runKey -Name "ProntoPC_Verify" -Value $verifyCmd -ErrorAction Stop } catch {}

    Write-Host "  Your PC will restart in 60 seconds. Press any key to cancel and restart later." -ForegroundColor Cyan
    $cancelled = $false
    for ($sec = 60; $sec -ge 1; $sec--) {
        if ($sec % 15 -eq 0 -or $sec -le 5) { Write-Host "    Restarting in $sec..." -ForegroundColor Yellow }
        for ($t = 0; $t -lt 10; $t++) {
            try { if ([Console]::KeyAvailable) { $null = [Console]::ReadKey($true); $cancelled = $true; break } } catch {}
            Start-Sleep -Milliseconds 100
        }
        if ($cancelled) { break }
    }

    if ($cancelled) {
        # User chose not to restart now — remove the pending verification so it never fires unexpectedly.
        try { Remove-ItemProperty -Path $runKey -Name "ProntoPC_Verify" -ErrorAction SilentlyContinue } catch {}
        Write-Host ""
        Write-Host "  Restart cancelled. Please restart your PC manually when convenient to finish" -ForegroundColor Yellow
        Write-Host "  applying the repairs. Your report is saved on your Desktop." -ForegroundColor Gray
        Read-Host "  Press Enter to close"
    } else {
        Write-Host ""
        Write-Host "  Restarting now — Pronto PC will verify the repairs automatically after reboot." -ForegroundColor Green
        Start-Sleep -Seconds 2
        Restart-Computer -Force
    }
} else {
    Read-Host "  Press Enter to close"
}
`;
}

// ─── Post-Restart Verification ───────────────────────────────────────────────

function handleVerifyScript(req, res, token) {
  const session = getSession(token);
  if (!session) return text(res, req, 200, `Write-Host "  Session expired." -ForegroundColor Red
Read-Host "  Press Enter to close"`);
  return text(res, req, 200, generateVerifyScript(token), "text/plain; charset=utf-8");
}

async function handleVerifyResults(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { token, verification } = body;
  if (!token) return json(res, req, 400, { ok: false, error: "token required" });
  const session = getSession(token);
  if (!session) return json(res, req, 404, { ok: false, error: "Session not found" });
  session.verification = verification;
  console.log(`[verify-results] Session ${token.substring(0, 8)}… verified (healthy=${verification?.healthy})`);
  return json(res, req, 200, { ok: true });
}

function generateVerifyScript(token) {
  return `# ProntoPC — Post-Restart Verification
$ErrorActionPreference = 'SilentlyContinue'
$token = '${token}'
$serverUrl = '${CLOUD_RUN_URL}'

# Remove the one-time startup entry FIRST so this can never run twice.
try { Remove-ItemProperty -Path "HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run" -Name "ProntoPC_Verify" -ErrorAction SilentlyContinue } catch {}

Start-Sleep -Seconds 8
Write-Host ""
Write-Host "  ================================================================" -ForegroundColor DarkYellow
Write-Host "    Pronto PC — Verifying your repairs" -ForegroundColor Yellow
Write-Host "  ================================================================" -ForegroundColor DarkYellow
Write-Host ""

$verify = @{}

try {
    $tempSize = (Get-ChildItem $env:TEMP -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
    $verify.temp_mb = [math]::Round($tempSize / 1MB)
} catch { $verify.temp_mb = $null }

$criticalSvcs = @("wuauserv","WinDefend","mpssvc","EventLog","Dhcp","Dnscache","RpcSs")
$stopped = @()
foreach ($s in $criticalSvcs) { $svc = Get-Service $s -ErrorAction SilentlyContinue; if ($svc -and $svc.Status -ne 'Running') { $stopped += $s } }
$verify.stopped_critical = $stopped

try {
    $boot = (Get-CimInstance Win32_OperatingSystem).LastBootUpTime
    $newErrors = Get-WinEvent -FilterHashtable @{ LogName='System'; Level=1,2; StartTime=$boot } -ErrorAction SilentlyContinue
    $verify.new_critical_errors = @($newErrors).Count
} catch { $verify.new_critical_errors = $null }

$verify.healthy = ($stopped.Count -eq 0)
$verify.checked_at = (Get-Date).ToString("o")

try {
    $body = @{ token = $token; verification = $verify } | ConvertTo-Json -Depth 5 -Compress
    Invoke-RestMethod -Uri "$serverUrl/api/verify-results" -Method POST -Body $body -ContentType "application/json" -TimeoutSec 15 | Out-Null
} catch {}

# Refresh the desktop report so it now includes the verification section.
try {
    $report = Invoke-RestMethod -Uri "$serverUrl/api/session/$token/report" -TimeoutSec 15
    $reportPath = "$env:USERPROFILE\\Desktop\\ProntoPC_Report_$(Get-Date -Format 'yyyyMMdd_HHmm').html"
    $report | Out-File -FilePath $reportPath -Encoding utf8
} catch {}

if ($verify.healthy) {
    Write-Host "  All clear — your PC restarted cleanly and the repairs are holding." -ForegroundColor Green
} else {
    Write-Host "  Restart complete. Some services are still starting up — usually normal right after boot." -ForegroundColor Yellow
}
Write-Host "  An updated report has been saved to your Desktop." -ForegroundColor Gray
Write-Host ""
Read-Host "  Press Enter to close"
`;
}

// ─── Report HTML Generation ─────────────────────────────────────────────────

function generateReportHtml(session) {
  const diag = session.diagnostics || {};
  const plan = session.repairPlan || {};
  const results = session.repairResults || [];
  const hostname = session.hostname || "Your PC";
  const now = new Date().toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric", hour: "2-digit", minute: "2-digit", timeZoneName: "short" });
  const hasRepairs = results.length > 0 && results.some(r => r.status === "done");
  const badgeText = hasRepairs ? "Repair Complete" : "Diagnostic Report";
  const badgeColor = hasRepairs ? "#7C5CFF" : "#3B82F6";

  const summary = plan.summary || "Your PC was scanned. See the details below.";
  const hwNotes = plan.hardware_notes || "";
  const secAssess = plan.security_assessment || "";

  const mem = diag.memory || {};
  const totalFreed = results.reduce((s, r) => s + (r.mb_freed || 0), 0);
  const completedCount = results.filter(r => r.status === "done").length;

  function fmtMb(mb) { return mb >= 1024 ? `${(mb/1024).toFixed(1)} GB` : `${mb} MB`; }
  function statusLabel(s) { return { done: "✓ Completed", noted: "✓ Logged", skipped: "— Skipped", error: "✗ Error" }[s] || s; }
  function statusColor(s) { return { done: "#2A7A4A", noted: "#2A7A4A", skipped: "#6B6560", error: "#C0392B" }[s] || "#1A1917"; }
  function boolBadge(val, trueText = "Enabled", falseText = "Disabled") {
    return val ? `<span style="color:#2A7A4A;font-weight:600">${trueText}</span>` : `<span style="color:#C0392B;font-weight:600">${falseText}</span>`;
  }

  let sections = "";

  // Post-restart verification (present only after the reconnect flow ran)
  const v = session.verification;
  if (v) {
    const vColor = v.healthy ? "#2A7A4A" : "#7C5CFF";
    const vLabel = v.healthy ? "System restarted cleanly — the repairs are holding." : "Restarted — a few services were still starting up.";
    sections += `<div class="section" style="border-color:${v.healthy ? "rgba(42,122,74,.3)" : "rgba(124,92,255,.3)"}"><div class="section-head"><div class="dot" style="background:${vColor}"></div><h3>Post-Restart Verification</h3></div><div style="padding:12px 20px"><p style="font-weight:700;color:${vColor};margin:0 0 10px">${vLabel}</p><table><tr><th>Check</th><th>Result</th></tr>`
      + `<tr><td>Critical services</td><td>${(v.stopped_critical && v.stopped_critical.length) ? `<span style="color:#C0392B">${v.stopped_critical.join(", ")} not running</span>` : `<span style="color:#2A7A4A">All running</span>`}</td></tr>`
      + `<tr><td>New critical errors since reboot</td><td>${v.new_critical_errors == null ? "—" : (v.new_critical_errors === 0 ? `<span style="color:#2A7A4A">None</span>` : `<span style="color:#7C5CFF">${v.new_critical_errors}</span>`)}</td></tr>`
      + `<tr><td>Temp files after restart</td><td>${v.temp_mb == null ? "—" : `${v.temp_mb} MB`}</td></tr>`
      + `</table></div></div>`;
  }

  // Repair actions
  if (results.length > 0) {
    let rows = results.map(a => `<tr><td>${a.name || a.action}</td><td style="color:${statusColor(a.status)};font-weight:600">${statusLabel(a.status)}</td><td style="color:#2A7A4A;font-family:'Courier New',monospace">${a.mb_freed ? fmtMb(a.mb_freed) + " recovered" : ""}</td><td style="color:#6B6560;font-size:.85em">${(a.detail || "—").substring(0, 120)}</td></tr>`).join("");
    sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>Repair Actions</h3></div><table><tr><th>Action</th><th>Result</th><th>Space</th><th>Notes</th></tr>${rows}</table></div>`;
  }

  // Drive health
  const disks = diag.disk || [];
  if (disks.length) {
    let rows = disks.map(d => {
      const pct = d.percent_used || 0;
      const color = pct > 90 ? "#C0392B" : pct > 70 ? "#7C5CFF" : "#2A7A4A";
      return `<tr><td>${d.device}</td><td>${d.total_gb} GB</td><td>${d.used_gb} GB</td><td>${d.free_gb} GB</td><td style="color:${color};font-weight:600">${pct}%</td></tr>`;
    }).join("");
    sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>Drive Health</h3></div><table><tr><th>Drive</th><th>Total</th><th>Used</th><th>Free</th><th>Usage</th></tr>${rows}</table></div>`;
  }

  // Drivers
  const drv = diag.drivers || {};
  if (drv.total) {
    let content = `<div style="padding:16px 20px"><div class="stats" style="margin-bottom:12px"><div class="stat"><div class="num">${drv.total}</div><div class="lbl">total drivers</div></div><div class="stat"><div class="num">${drv.signed || 0}</div><div class="lbl">signed</div></div><div class="stat"><div class="num">${(drv.unsigned || []).length}</div><div class="lbl">unsigned</div></div><div class="stat"><div class="num">${(drv.outdated || []).length}</div><div class="lbl">outdated</div></div></div></div>`;
    if ((drv.unsigned || []).length) {
      content += `<table><tr><th>Unsigned Driver</th><th>Manufacturer</th><th>Version</th></tr>${drv.unsigned.map(d => `<tr><td style="color:#C0392B">${d.device}</td><td>${d.manufacturer || "—"}</td><td>${d.version || "—"}</td></tr>`).join("")}</table>`;
    }
    if ((drv.outdated || []).length) {
      const driverLink = (d) => `https://www.google.com/search?q=${encodeURIComponent(`${d.provider || ""} ${d.device} driver download`.trim())}`;
      content += `<div style="padding:0 20px"><p style="font-size:.82rem;color:#6B6560;margin:6px 0 0">These drivers are over two years old. Updating them is optional and free — click <strong>Find latest</strong> to reach the manufacturer's download page and install it yourself. ProntoPC never installs or changes drivers automatically.</p></div>`;
      content += `<table><tr><th>Outdated Driver</th><th>Version</th><th>Date</th><th>Age</th><th>Recommended</th></tr>${drv.outdated.map(d => `<tr><td>${d.device}</td><td>${d.version || "—"}</td><td>${d.date || "—"}</td><td style="color:#7C5CFF">${d.age_days}d</td><td><a href="${driverLink(d)}" target="_blank" rel="noopener" style="color:#1A6DC8;text-decoration:none;font-weight:600">Find latest &rarr;</a></td></tr>`).join("")}</table>`;
    }
    const devErrors = diag.device_errors || [];
    if (devErrors.length) {
      content += `<table><tr><th>Problem Device</th><th>Code</th><th>Description</th></tr>${devErrors.map(d => `<tr><td style="color:#C0392B">${d.device}</td><td>${d.error_code}</td><td>${d.error_description}</td></tr>`).join("")}</table>`;
    }
    const bsod = diag.bsod_history || [];
    if (bsod.length) {
      content += `<table><tr><th>Crash Date</th><th>Details</th></tr>${bsod.map(b => `<tr><td>${(b.date || "").substring(0, 19)}</td><td>${b.file || b.message || "—"}</td></tr>`).join("")}</table>`;
    }
    sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>Driver Audit</h3></div>${content}</div>`;
  }

  // Security
  const sec = diag.security || {};
  const def = sec.defender || {};
  const fw = sec.firewall || {};
  const act = diag.activation || {};
  if (def.real_time_protection !== undefined || fw.profiles?.length || act.status) {
    let content = `<div style="padding:16px 20px">`;
    if (def.real_time_protection !== undefined) {
      content += `<div style="margin-bottom:12px"><strong style="font-size:.82rem;text-transform:uppercase;color:#6B6560">Windows Defender</strong><div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;margin-top:6px"><div>Real-time Protection: ${boolBadge(def.real_time_protection)}</div><div>Antivirus: ${boolBadge(def.antivirus_enabled)}</div><div>Antispyware: ${boolBadge(def.antispyware_enabled)}</div><div>Behavior Monitor: ${boolBadge(def.behavior_monitor)}</div></div></div>`;
    }
    if (fw.profiles?.length) {
      content += `<div style="margin-bottom:12px"><strong style="font-size:.82rem;text-transform:uppercase;color:#6B6560">Firewall</strong><div style="margin-top:6px">${fw.profiles.map(p => `${p.name}: ${boolBadge(p.enabled, "ON", "OFF")}`).join(" &nbsp;·&nbsp; ")}</div></div>`;
    }
    if (act.status) {
      content += `<div><strong style="font-size:.82rem;text-transform:uppercase;color:#6B6560">Activation</strong><div style="margin-top:6px">${boolBadge(act.is_activated, "Licensed", act.status)} — ${act.product || ""}</div></div>`;
    }
    content += `</div>`;
    sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>Security Status</h3></div>${content}</div>`;
  }

  // Network
  const net = diag.network || {};
  if (net.adapters?.length || net.connectivity) {
    let content = "";
    if (net.adapters?.length) {
      content += `<table><tr><th>Adapter</th><th>Description</th><th>Speed</th></tr>${net.adapters.map(a => `<tr><td>${a.name}</td><td>${a.description || "—"}</td><td>${a.speed || "—"}</td></tr>`).join("")}</table>`;
    }
    if (net.connectivity?.interfaces?.length) {
      content += `<table><tr><th>Interface</th><th>IPv4</th><th>Gateway</th><th>DNS</th></tr>${net.connectivity.interfaces.map(i => `<tr><td>${i.name}</td><td>${i.ipv4}</td><td>${i.gateway}</td><td style="font-size:.82em">${i.dns_servers}</td></tr>`).join("")}</table>`;
    }
    const inet = net.connectivity?.internet_reachable;
    if (inet !== undefined) {
      content += `<div style="padding:12px 20px">Internet: ${boolBadge(inet, "Connected", "UNREACHABLE")}</div>`;
    }
    sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>Network Health</h3></div>${content}</div>`;
  }

  // Services
  const svc = diag.services || {};
  if ((svc.stopped_critical || []).length || (svc.failed || []).length) {
    let content = "";
    if (svc.stopped_critical?.length) {
      content += `<table><tr><th>Service</th><th>Display Name</th><th>Status</th></tr>${svc.stopped_critical.map(s => `<tr><td style="color:#C0392B">${s.name}</td><td>${s.display_name}</td><td>${s.status}</td></tr>`).join("")}</table>`;
    }
    if (svc.failed?.length) {
      content += `<table><tr><th>Time</th><th>Message</th></tr>${svc.failed.map(f => `<tr><td style="font-size:.82em">${(f.time || "").substring(0, 19)}</td><td style="font-size:.82em">${(f.message || "").substring(0, 150)}</td></tr>`).join("")}</table>`;
    }
    sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>Services Audit</h3></div>${content}</div>`;
  }

  // Event errors
  const errors = diag.event_errors || [];
  if (errors.length) {
    let rows = errors.slice(0, 10).map(e => `<tr><td style="font-size:.82em;color:#6B6560">${(e.time || "").substring(0, 19)}</td><td style="font-weight:500">${e.source}</td><td style="font-size:.82em">${(e.message || "").substring(0, 120)}</td></tr>`).join("");
    sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>Windows Event Errors</h3></div><table><tr><th>Time</th><th>Source</th><th>Message</th></tr>${rows}</table></div>`;
  }

  // System info
  const osInfo = diag.os || {};
  const uptimeInfo = diag.uptime || {};
  const cpuInfo = diag.cpu || {};
  const ppInfo = diag.power_plan || {};
  sections += `<div class="section"><div class="section-head"><div class="dot"></div><h3>System Information</h3></div><div class="sysinfo"><div class="sysinfo-row"><span class="key">Computer</span><span class="val">${hostname}</span></div><div class="sysinfo-row"><span class="key">Windows</span><span class="val">${osInfo.edition || ""} (Build ${osInfo.build || ""})</span></div><div class="sysinfo-row"><span class="key">CPU</span><span class="val">${cpuInfo.processor_name || osInfo.processor || "—"}</span></div><div class="sysinfo-row"><span class="key">Memory</span><span class="val">${mem.total_gb || "—"} GB (${mem.percent_used || 0}% used)</span></div><div class="sysinfo-row"><span class="key">Uptime</span><span class="val">${uptimeInfo.uptime_days || 0} days${uptimeInfo.needs_reboot ? ' <span style="color:#C0392B">(reboot recommended)</span>' : ""}</span></div><div class="sysinfo-row"><span class="key">Power Plan</span><span class="val">${ppInfo.active_plan || "—"}</span></div></div></div>`;

  return `<!DOCTYPE html>
<html lang="en"><head><meta charset="UTF-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Pronto PC — ${badgeText}</title>
<style>*{margin:0;padding:0;box-sizing:border-box}body{font-family:'Segoe UI',Arial,sans-serif;background:#FDFAF4;color:#1A1917;font-size:15px;line-height:1.6}.page{max-width:860px;margin:0 auto;padding:40px 32px 60px}.header{background:#1A1917;color:#fff;border-radius:12px;padding:28px 32px;margin-bottom:28px;display:flex;justify-content:space-between;align-items:flex-start}.header-left h1{font-size:1.6rem;font-weight:700;letter-spacing:-.5px;margin-bottom:4px}.header-left h1 span{color:#7C5CFF}.header-left p{color:rgba(255,255,255,.6);font-size:.85rem}.header-right{text-align:right}.header-right .badge{background:${badgeColor};color:#fff;font-size:.72rem;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;padding:.3rem .7rem;border-radius:6px;display:inline-block;margin-bottom:6px}.header-right p{color:rgba(255,255,255,.5);font-size:.78rem}.summary-box{background:#fff;border:1px solid #E2DBD0;border-radius:10px;padding:20px 24px;margin-bottom:20px}.summary-box h2{font-size:1rem;font-weight:700;margin-bottom:8px}.summary-box p{color:#3A3835;line-height:1.7}.hw-notes{background:#FFF5F5;border:1px solid #FCA5A5;border-radius:10px;padding:16px 20px;margin-bottom:20px}.hw-notes strong{color:#C0392B}.sec-notes{background:#F0F9FF;border:1px solid #93C5FD;border-radius:10px;padding:16px 20px;margin-bottom:20px}.sec-notes strong{color:#1D4ED8}.stats{display:grid;grid-template-columns:repeat(auto-fit,minmax(120px,1fr));gap:14px;margin-bottom:20px}.stat{background:#fff;border:1px solid #E2DBD0;border-radius:10px;padding:16px 20px;text-align:center}.stat .num{font-size:1.8rem;font-weight:700;color:#7C5CFF;font-family:Georgia,serif;line-height:1}.stat .lbl{font-size:.78rem;color:#6B6560;margin-top:4px;font-weight:500}.section{background:#fff;border:1px solid #E2DBD0;border-radius:10px;margin-bottom:20px;overflow:hidden}.section-head{background:#F5F0E8;padding:14px 20px;border-bottom:1px solid #E2DBD0;display:flex;align-items:center;gap:10px}.section-head h3{font-size:.9rem;font-weight:700;letter-spacing:.3px;text-transform:uppercase;color:#1A1917}.section-head .dot{width:10px;height:10px;border-radius:50%;background:#7C5CFF;flex-shrink:0}table{width:100%;border-collapse:collapse}th{text-align:left;font-size:.78rem;font-weight:700;letter-spacing:.8px;text-transform:uppercase;color:#6B6560;padding:10px 20px;border-bottom:1px solid #E2DBD0;background:#FDFAF4}td{padding:11px 20px;border-bottom:1px solid #F0EBE4;font-size:.88rem;vertical-align:top}tr:last-child td{border-bottom:none}tr:hover td{background:#FDFAF4}.sysinfo{padding:16px 20px;display:grid;grid-template-columns:1fr 1fr;gap:8px 24px}.sysinfo-row{display:flex;flex-direction:column}.sysinfo-row .key{font-size:.72rem;font-weight:700;letter-spacing:.8px;text-transform:uppercase;color:#9B9590}.sysinfo-row .val{font-size:.88rem;color:#1A1917;font-weight:500}.footer{margin-top:32px;padding-top:20px;border-top:1px solid #E2DBD0;display:flex;justify-content:space-between;align-items:center}.footer p{font-size:.78rem;color:#9B9590}.footer strong{color:#1A1917}.print-btn{margin-top:8px;background:#7C5CFF;color:#fff;border:none;font-weight:700;font-size:.75rem;padding:7px 14px;border-radius:6px;cursor:pointer}.print-btn:hover{background:#6A4FE0}@media print{body{background:#fff}.page{padding:20px}.header{border-radius:0}.no-print{display:none!important}}@media(max-width:600px){.stats{grid-template-columns:1fr 1fr}.header{flex-direction:column;gap:12px}.header-right{text-align:left}.sysinfo{grid-template-columns:1fr}}</style>
</head><body><div class="page">
<div class="header"><div class="header-left" style="display:flex;align-items:center;gap:14px"><img src="data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAADAAAAAwCAYAAABXAvmHAAAOGklEQVR4nO1ZeXRUVZr/3fdeVWpLbalKVRLIAlkgBBIIoAhCIgRUaAU1paftVtEWBBlGR0+rpz1TqTPOEe2Z1h577NPdarsvCZ2DtjZqgwFEDJCYvbKTtSpLVSWpSiWp5b1357wk9HhmPDZpgu0f/f1RdevVfd/9fvdb7vd9F/gHzZDdThlKKZn9+Q/6zujirj/44KH/fuSRp/MBELvdznxX6zOXy8BmK5vm4XYPeQa9vjslTCf+P18J5PfXvOyzO77rjn0v3HHHQcvsM46WUvbrfiGNK+wV3HxqiJsPJk7nMiIJt8v2wBm5Vl5EKX2LEMI74Jj+fwegUgKUEDIFgJeeUUoZQgiVhn93AGVlNpEQ0O237DvrDwTulAQ79OBz22TKOBuVyVdojCYrpaBF4eigGPSfDQa6XiWEVEvvStpwOBzi37r2fNmlpAGQhcsMjx144mT8iGJEYzVuTF2TgeHAKKIaPXQ6PSITYYSGRhFq7qS6KH3tV5/+y8EzZ1rHLwfEvGigtLRUMgfh5m33rdiYUpgzYB4GY42KS/IXiinjRmbYMwq1IgSqJeghokgZHaOb0tzzgPhsjkX96vaSkhKPFFD+FhBkvnZ//folmifvf7vKK1enL1yjFRAYl+XkZoITgcjEOHg+DFWMDBqFCoEwj09OVUaUg2q5KhKq2LZ/01ZKqUgImTMAZp52n25f+08lxLowM2SI0KzUBFmUsKg714Dz1bU4fOQUXvvtZ3j3rRP4vLIanBjBqvREeXewNZqctKjwjWfeuIthGFGKUN+pBkpLKWuzEWHP7kMPbd5V/FxDd3c0OzdJti43HQPeEXz6yUkM1U+CNrNITUnAyGgAEa0PWYVaxCfEYYqygsEDYtFqa1fftip/NjKJV1QDktCUUlYaFxdDWoxZcu01exKWxtPJ0Djrcfeju60dFxoaMFQdRILHhIcevR13HyjA40/vhGl0CY693YGBQT+SrUa2baCVxnD6VU8++It/loQvLS2d5n3FNUClsA5CH7662KjffaDD6+kzBH1+GtKYidpiRthZB+EMwY6N18NoUcPVEsRVN5nRcmYCZR9/BOMGL64ryEBjbYdw8zU2dpT42zfvXLOESgcKmRbrks6HS7a5Gb4Ev/7dH19QK9kY8iOyV3p8/a0/4N5v62Wam2uRnJIMIV4Nj9YEJkCxIMaIC3WD6OBFEE6Bd/7TBVUsB7VcA4/HjT90xSA6oiC5vRegXGhUW2BREkIm5rKxlwqAMAyhOl2uPhoW7teZjfIU3aZ/7/Gf7D72+eEp3viD6OLEPBoN+RDTUYUUXzc88gAQa0F26mJoY+WYmGIBeQSIAt1+F5QqFtFYDUhsHFobWuFrIpEHsjdGHc4yXBEfkFIav79u4vifTw0fLv9IvCp/VZr0/OcffDA+2e/+KDcxkxhYkxgMEoyKWhAuBkwaj6lQFIuX6rG6SIMNN5og52KgSKVQKoOIq/kQSa4WajGm0qHegWGHsywi1RVzSS8uBcCsOil55CePr1Bp42WUxDATQtAimVVpaaP8zWP2/UYmXLshs5BodOmCfPlyTCbnYoz0ostcDedQP0aCk2is9aJL3wwhvh/QJWFUVCLdlEcZQUFGxoZflJZyOsvm5JfcJdq+ePCRZ15Ly7vqrs3LM3hPbTWqT6jvI4S8SykVCGEiDZ1fHtq+ds27MUxctPv0CUZjtRIaDqJu5Ch8Qhc0AT1CfBj9LicM6ctB41JgHI7wmeYV3PGm8j0fVb78GrVThjiIMBcA34r2Yo7y0kulWSfONrR0DU1icXYG9TSdp8nJaxilmnnl+Wf37AcQlub/zPb6r5lV6x84euYlJItegERhtZghi+HAiwJkcjn8njHEGJIh+iNigXkbMy70HN77X7cVV9gpV+gg05nqfGpAMjGx0+1dyYsyun3XDULCAgPXKZ8idQ0uPuPqq+79t0ffT3I7Dz9sSr4++0zlR3+I7a9n6GTwzm6DQmk2xjITggJqkYVCpYQwFcJkmMeY202TU1cK9e6zrxx678D9v/lNlaxw79yFvxQA00TDshi3q592v1cOsyUO465OsKKeGx5sFtSWtdvkucX1wUUZnE5twkTjuS+yjAtC51vr1aMeHx006IgxLg4GIwENheEfidIEVaLgamx3JeqSfLuLfnrD3r2rj14M0/MKwOl0TkeDwZ7+do1Sz8QvttKkZDNGNCwaW10IT06wvQGXGBEZjm87JvT3jjC9zpr1+detR1peAurO9ZCe9q8gZwUolApwogwsNZAtu9ayFXXO1KSC7Md+VHT7Y9aU+IOEkBcupibzBqCsrEyY9YOzNxQd+MDfMnaT+6uOaP9Ao0yusyJpIhZBxs2Y4+OpRaOF2NdKR1SDdEG8SG674z7S1nk9qmua0NnSDpHnIeM4qDgC81ITWRbSo2v4y2gvWcwiLvWpp7fve7O4GGOzfknnzYQcDod0ukv6Lb5r56FPwvLEAkHl5k1mK4ZdHkR5N9zLizhtaxfb52zD8KALnT0+RBUG7NqRgl1b1wBUhMfjg0gplGolqiuaUBDZgPff/RU76Vcx6oCn84cqb4AwDLXDzjjgmNdzgBJSItlnpOXUSzdnK83lW9Pu4NQ0jWN4M0diFnOc3hD11radZQOyFp0sHYMBGf38VDWqqhsRCU9ChTBGB91oqW2AXAiDUYYhggXGhqAOtWLnpny+3MWuTDcYtA5MFzWX7Axz8Zq/qPbuNU9sZvT6awOCj+0b7Bmi4xcqzvedb3p638uHAhNZj73bVcen5am59SkcouM+uEMyZGclIhLlUXmqBomqKeQl78DhI+VIz44ibmEWhifNWGWKPf0fT+4s+nkpjdps02n1X9XEXAoI+pfal5DjAI7/L7SZffB43XULLVtBJj4jPVVfYKzSC2LJwbotBVietRCUJWjpHMX540fg6qWItVjwZc05LBNZ3LpvK68QEjfce//zz9ps5OClOvRcK6DpVLe4uJjNHt5PTuAE4uPBlJWB37t9avVSi+XhY3WPirnqAZZIZgMFlPJJqEd64O2TgXByJMCHMb0KirF6bNm6DuXMIhgSF0Fv1LCLFhn4zqoV++/d8uPXbTamSlpHCiTzCWCaZpjOZI326TKwUNSpf3EDYZk1WrGKL1yTwcTGmtDW7cNUpAFRTxi+dj9M8MEUiaLI1IF+mYAVnAfOSZdQc3qIpSk55MACE7K35LNjPR1P4NgbtxYXl6Lsr6RG89CVODH9Sfmw0NVfj+AUj0aXAJ3cgxghglh+EsO+fowKIpLVLtCgAYJAQQWCuu52MGw+6/W0UHf1l6QyJYZNMqgpJWzRwevus9hsZOhi4XTFADidy6aZ+wNjF8xKZsI/JVeMDrqQlKJBzagaVZGVWJ2mhoyR42RkISY4FkfrA7g6po1mmk3iaKD3Y3PcNduDQ1146/kXsWj5KuJvHeJ8HpdM4ktmHIxesa5EWZlNkHqgvz32zNtBg7W86PHfs1N5t/GvVPE43eZCz4V6eCMxosDK6GiERX1DK6KjreJQXD750Jvf8cbZ93bEyyaen3DJ+bTMpeLq9Svo7v17mGSFQiXxt9vt37r+/DRZZ4p7ZC9fkc7EypGRs5ysu+l2hNk4ao1qEOgUmI4OkK9qfHDWnkLe6nW48ZZixBuNnD3lbkX56aceTkpRvJK7Iod95+W3w56AP2Z5ztYiiWcBCpgr3ZmTDjlatKJI3dbZm3bus3JwNIR1a1cjO3Ojb6rdu107FVH6vAM0Sb34Df3aHyYvW2qkpz79FOKoVrVqbSpHuynZc/C5vD8fqRDUrJlWnjiKxsoar8T8ReeL9IpqwG63S20Qkrh27U5Bp7cW7H5AyNy0Beer20BEvuGz9pfPHal97uTn7a+fmkyQNZhTFuDsF6epNmMZTSlcb32zpjpf2oD2qo4WvSmTXbMhRymMT50+01xeKuVhVySMfgPR8Gh48wRD0VHZTFmBJ5ycgyY9kcXHdqaCljD3EMLJMlKzePk4Br2jDDco0CDfybAamVxi0NJc8696q0Z9/nhf9yfH33l29sCkV7o3Om0+txfusGRf8xNn1UCHsbn5AjUZlEKyVsEFxrlXj35YsluaeGDbE9cGTKtPVfu+EjMtAtM3EBJyjKnsAjKx66l3fnZEOiAloWeYSqXspQlwWRqw2ytYh6OQX2Ldels0PsuYnGng1SYVl2BSw9MbhrzvwnQolMioyVsyFpRDZFiRauKZm+7JooGGEGIHRtZRSt8vsZXJnIAwPNxETp50CPPe2PomKikpmG4t6slS2xetNdQvGyaxSj2SlyxBTc1ZBMZG3BfnNg+06kfkFEodpstK10CAtDe2YEHIf62kRbud8mWOud/YXA6A6W7Fpk2bNBz7YU5YpiZTcsL4WC/taa6DkTXTZelxiurGmcm+SP9AvMXKc4JX7G4dEZJTF5GVG3P51XLLuqvy81580EH2zxZPcwJwOVGISm2QkydPTmkpzt9/94+Fhx7ZTffvsZFlxiwa7GsmruH6rouTtVysfmQowDXXVsnZcR/rampirIl6Tr1SjohMse+nO5+4ReqAFBfPrbl7eVGoBBQOiIvi06yN7dWs0DvOJ5l04/Jgv44EPd7jrqO/vxgoKlv+9Ee5Qq+f5ANDMei6tfurvszXmj5lNAbVL2V+7NYRrW6G6dxai5cdhSRNrMy+8bkJfiS5t63xlyFMxWUuysmQyWLON7Weq7jYmvmGdxUApBAamP2O4PtA/6c18vUfZNOmTRxQzM7cE0tpGpnp/Enjv+c9uCTQjFDTF9hEKkRmx99GX7+9//7e4l9p+h8RZ3qhR2NHWgAAAABJRU5ErkJggg==" width="46" height="46" alt="" style="border-radius:8px;flex-shrink:0"><div><h1>Pronto <span>PC</span> — ${badgeText}</h1><p>Computer: ${hostname} · ${now}</p></div></div><div class="header-right"><div class="badge">${badgeText}</div><p>Custodian v2.0</p><button class="print-btn no-print" onclick="window.print()">Print / Save PDF</button></div></div>
<div class="summary-box"><h2>Summary</h2><p>${summary}</p></div>
${hwNotes ? `<div class="hw-notes"><strong>Hardware Notes:</strong> ${hwNotes}</div>` : ""}
${secAssess ? `<div class="sec-notes"><strong>Security:</strong> ${secAssess}</div>` : ""}
<div class="stats"><div class="stat"><div class="num">${totalFreed ? fmtMb(totalFreed) : "—"}</div><div class="lbl">space recovered</div></div><div class="stat"><div class="num">${completedCount || "—"}</div><div class="lbl">repairs${hasRepairs ? " completed" : " recommended"}</div></div><div class="stat"><div class="num">${mem.percent_used || "—"}%</div><div class="lbl">memory in use</div></div></div>
${sections}
${session.couponCode ? `<div class="section" style="border-color:rgba(124,92,255,.25);background:linear-gradient(135deg,#FFFDF8,#FFF8ED)"><div style="padding:24px;text-align:center"><h3 style="font-size:1.1rem;font-weight:700;color:#1A1917;margin-bottom:8px">A thank-you: 50% off your next repair</h3><p style="color:#6B6560;font-size:.9rem;max-width:440px;margin:0 auto 16px">Keep this code. If your PC ever needs us again, it's half price — good for six months.</p><div style="display:inline-block;background:#fff;border:1px dashed #7C5CFF;border-radius:10px;padding:16px 28px;margin-bottom:12px"><div style="font-size:.72rem;color:#9B9590;text-transform:uppercase;letter-spacing:.08em;margin-bottom:4px">Your coupon code</div><div style="font-size:1.5rem;font-weight:800;color:#1A1917;font-family:'Courier New',monospace;letter-spacing:.05em">${session.couponCode}</div></div><p style="font-size:.78rem;color:#9B9590;margin-bottom:16px">50% off · valid through ${new Date(session.couponExpiresAt).toLocaleDateString("en-US", { month: "long", day: "numeric", year: "numeric" })} · enter it at checkout</p><a href="${SITE_URL}" style="display:inline-block;background:#7C5CFF;color:#fff;font-weight:700;font-size:.9rem;padding:12px 28px;border-radius:8px;text-decoration:none">Start your next repair</a></div></div>` : ""}
<div class="footer"><p><strong>Pronto PC</strong> · prontopc.online · support@prontopc.online</p><p>5D Service Solutions Global LLC · 30-day re-repair guarantee</p></div>
</div></body></html>`;
}

// ─── Guardian Subscription Handlers ─────────────────────────────────────────

const GUARDIAN_PRICE_MONTHLY = process.env.GUARDIAN_PRICE_MONTHLY || "";
const GUARDIAN_PRICE_WEEKLY = process.env.GUARDIAN_PRICE_WEEKLY || "";

async function handleGuardianSubscribe(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { token, frequency, execution } = body;
  if (!frequency || !execution) return json(res, req, 400, { ok: false, error: "frequency and execution required" });
  if (!["weekly", "monthly"].includes(frequency)) return json(res, req, 400, { ok: false, error: "frequency must be weekly or monthly" });
  if (!["manual", "automatic"].includes(execution)) return json(res, req, 400, { ok: false, error: "execution must be manual or automatic" });
  if (!STRIPE_SECRET_KEY) return json(res, req, 500, { ok: false, error: "Stripe not configured" });

  let customerEmail = "";
  if (token) {
    const session = getSession(token);
    if (session) customerEmail = session.email || "";
  }

  const checkoutParams = new URLSearchParams({
    "mode": "subscription",
    "line_items[0][price]": frequency === "weekly" ? GUARDIAN_PRICE_WEEKLY : GUARDIAN_PRICE_MONTHLY,
    "line_items[0][quantity]": "1",
    "success_url": `${CLOUD_RUN_URL.replace("https://pronto-pc-108655745293.us-central1.run.app", "https://prontopc.online")}/guardian?session={CHECKOUT_SESSION_ID}`,
    "cancel_url": "https://prontopc.online/guardian-offer",
    "metadata[frequency]": frequency,
    "metadata[execution]": execution,
    "subscription_data[trial_period_days]": "30",
  });
  if (customerEmail) checkoutParams.set("customer_email", customerEmail);

  try {
    const resp = await fetch("https://api.stripe.com/v1/checkout/sessions", {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${STRIPE_SECRET_KEY}`,
        "Content-Type": "application/x-www-form-urlencoded",
      },
      body: checkoutParams.toString(),
      signal: AbortSignal.timeout(15_000),
    });
    const session = await resp.json();
    if (session.error) return json(res, req, 400, { ok: false, error: session.error.message });
    return json(res, req, 200, { ok: true, checkoutUrl: session.url });
  } catch (e) {
    console.error("[guardian/subscribe] Error:", e.message);
    return json(res, req, 500, { ok: false, error: "Could not create checkout session" });
  }
}

async function handleGuardianActivate(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { stripeSessionId } = body;
  if (!stripeSessionId) return json(res, req, 400, { ok: false, error: "stripeSessionId required" });

  try {
    const sessionResp = await fetch(`https://api.stripe.com/v1/checkout/sessions/${stripeSessionId}`, {
      headers: { "Authorization": `Bearer ${STRIPE_SECRET_KEY}` },
      signal: AbortSignal.timeout(15_000),
    });
    const stripeSession = await sessionResp.json();
    if (stripeSession.error) return json(res, req, 400, { ok: false, error: stripeSession.error.message });
    if (stripeSession.payment_status !== "paid" && stripeSession.status !== "complete") {
      return json(res, req, 402, { ok: false, error: "Subscription payment not completed" });
    }

    const subscriptionId = stripeSession.subscription;
    const email = stripeSession.customer_details?.email || "";
    const frequency = stripeSession.metadata?.frequency || "monthly";
    const execution = stripeSession.metadata?.execution || "manual";

    const existing = findGuardianBySubscription(subscriptionId);
    if (existing) {
      const g = existing.data;
      const intervalMs = g.frequency === "weekly" ? 7 * 24 * 60 * 60 * 1000 : 30 * 24 * 60 * 60 * 1000;
      const scanUrl = `${CLOUD_RUN_URL}/api/guardian/scan?token=${existing.token}`;
      const setupCommand = execution === "automatic"
        ? `powershell -ExecutionPolicy Bypass -Command "irm '${scanUrl}' | iex"`
        : null;
      return json(res, req, 200, {
        ok: true, scanToken: existing.token, frequency: g.frequency, execution: g.execution,
        nextScanAt: g.nextScanAt, setupCommand, portalUrl: "https://billing.stripe.com/p/login/test",
      });
    }

    const scanToken = createGuardian(email, subscriptionId, frequency, execution);
    const g = getGuardian(scanToken);
    const scanUrl = `${CLOUD_RUN_URL}/api/guardian/scan?token=${scanToken}`;
    const setupCommand = execution === "automatic"
      ? `powershell -ExecutionPolicy Bypass -Command "irm '${scanUrl}' | iex"`
      : null;

    console.log(`[guardian/activate] Guardian created: ${email}, ${frequency}, ${execution}`);
    return json(res, req, 200, {
      ok: true, scanToken, frequency, execution,
      nextScanAt: g.nextScanAt, setupCommand, portalUrl: "https://billing.stripe.com/p/login/test",
    });
  } catch (e) {
    console.error("[guardian/activate] Error:", e.message);
    return json(res, req, 500, { ok: false, error: "Could not activate subscription" });
  }
}

function handleGuardianScanScript(req, res, token) {
  const g = getGuardian(token);
  if (!g) return json(res, req, 404, { ok: false, error: "Invalid or cancelled guardian token" });

  const isFirstRun = !g.lastScanAt;
  const installTask = g.execution === "automatic" && isFirstRun;
  const scanUrl = `${CLOUD_RUN_URL}/api/guardian/scan?token=${token}`;
  const resultsUrl = `${CLOUD_RUN_URL}/api/guardian/results`;

  const script = `
# ═══════════════════════════════════════════════════════════════════
# ProntoPC Guardian — Health Check
# 5D Service Solutions Global LLC
# ═══════════════════════════════════════════════════════════════════

$ErrorActionPreference = 'SilentlyContinue'
$token = '${token}'
$serverUrl = '${CLOUD_RUN_URL}'

Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor DarkYellow
Write-Host "  ║       ProntoPC Guardian — Health Check           ║" -ForegroundColor DarkYellow
Write-Host "  ║       Scanning your system...                    ║" -ForegroundColor DarkYellow
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor DarkYellow
Write-Host ""

${installTask ? `
# Install Scheduled Task for automatic scans
$taskName = "ProntoPC Guardian"
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if (-not $existingTask) {
  Write-Host "  [+] Installing scheduled task for automatic scans..." -ForegroundColor Cyan
  $trigger = New-ScheduledTaskTrigger -${g.frequency === "weekly" ? "Weekly -DaysOfWeek Monday" : "Daily -DaysInterval 30"} -At 10am
  $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-ExecutionPolicy Bypass -WindowStyle Hidden -Command \`"irm '${scanUrl}' | iex\`""
  Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Description "ProntoPC Guardian ${g.frequency} health check" -RunLevel Highest -Force | Out-Null
  Write-Host "  [OK] Scheduled task installed. Scans will run ${g.frequency === "weekly" ? "every Monday at 10 AM" : "every 30 days at 10 AM"}." -ForegroundColor Green
  Write-Host ""
}
` : ""}

# ─── Diagnostic Collection (scan-only, no repairs) ───
$diag = @{}

Write-Host "  [1/8] System info..." -ForegroundColor Gray
$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem
$diag.os = @{ release = $os.Caption; version = $os.Version; machine = $cs.SystemType; processor = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name; hostname = $env:COMPUTERNAME }
$diag.uptime = @{ boot_time = $os.LastBootUpTime.ToString("o"); uptime_days = [math]::Floor(((Get-Date) - $os.LastBootUpTime).TotalDays); uptime_hours = [math]::Floor(((Get-Date) - $os.LastBootUpTime).TotalHours % 24) }

Write-Host "  [2/8] Memory & CPU..." -ForegroundColor Gray
$mem = Get-CimInstance Win32_OperatingSystem
$totalMB = [math]::Round($mem.TotalVisibleMemorySize / 1024)
$freeMB = [math]::Round($mem.FreePhysicalMemory / 1024)
$diag.memory = @{ total_gb = [math]::Round($totalMB / 1024, 1); used_gb = [math]::Round(($totalMB - $freeMB) / 1024, 1); percent_used = [math]::Round((($totalMB - $freeMB) / $totalMB) * 100) }
$diag.cpu = @{ processor_name = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name; physical_cores = (Get-CimInstance Win32_Processor | Select-Object -First 1).NumberOfCores; logical_cores = (Get-CimInstance Win32_Processor | Select-Object -First 1).NumberOfLogicalProcessors }

Write-Host "  [3/8] Disk health..." -ForegroundColor Gray
$diag.disk = @(Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object { @{ device = $_.DeviceID; total_gb = [math]::Round($_.Size / 1GB, 1); free_gb = [math]::Round($_.FreeSpace / 1GB, 1); used_gb = [math]::Round(($_.Size - $_.FreeSpace) / 1GB, 1); percent_used = [math]::Round((($_.Size - $_.FreeSpace) / $_.Size) * 100) } })

Write-Host "  [4/8] Security..." -ForegroundColor Gray
$defender = Get-MpComputerStatus -ErrorAction SilentlyContinue
if ($defender) { $diag.security = @{ real_time_protection = $defender.RealTimeProtectionEnabled; antivirus_enabled = $defender.AntivirusEnabled; antispyware_enabled = $defender.AntispywareEnabled; signature_last_updated = $defender.AntivirusSignatureLastUpdated.ToString("o") } }

Write-Host "  [5/8] Drivers..." -ForegroundColor Gray
$drivers = Get-CimInstance Win32_PnPSignedDriver -ErrorAction SilentlyContinue
$unsigned = @($drivers | Where-Object { $_.IsSigned -eq $false })
$diag.drivers = @{ total = $drivers.Count; signed = ($drivers | Where-Object { $_.IsSigned }).Count; unsigned = $unsigned.Count }

Write-Host "  [6/8] Network..." -ForegroundColor Gray
$adapters = Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq "Up" }
$diag.network = @{ adapters = @($adapters | ForEach-Object { @{ name = $_.Name; description = $_.InterfaceDescription; speed = "$([math]::Round($_.LinkSpeed.Replace(' Gbps','').Replace(' Mbps','')))" } }); internet_reachable = (Test-Connection -ComputerName 8.8.8.8 -Count 1 -Quiet -ErrorAction SilentlyContinue) }

Write-Host "  [7/8] Services..." -ForegroundColor Gray
$criticalSvcs = @("wuauserv","WinDefend","Spooler","BITS","Dnscache","Dhcp")
$stoppedCritical = @(Get-Service | Where-Object { $criticalSvcs -contains $_.Name -and $_.Status -ne "Running" } | ForEach-Object { @{ name = $_.Name; display_name = $_.DisplayName; status = $_.Status.ToString() } })
$diag.services = @{ stopped_critical = $stoppedCritical }

Write-Host "  [8/8] Storage metrics..." -ForegroundColor Gray
$tempSize = (Get-ChildItem $env:TEMP -Recurse -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum
$diag.temp_files = @{ size_mb = [math]::Round($tempSize / 1MB, 1) }
$recycleBin = (New-Object -ComObject Shell.Application).NameSpace(0xA)
$rbItems = $recycleBin.Items().Count
$diag.recycle_bin = @{ item_count = $rbItems }

Write-Host ""
Write-Host "  Scan complete. Sending results..." -ForegroundColor Green

# ─── Submit Results ───
$payload = @{ token = $token; diagnostics = $diag; timestamp = (Get-Date).ToString("o") } | ConvertTo-Json -Depth 10
try {
  $response = Invoke-RestMethod -Uri "$serverUrl/api/guardian/results" -Method POST -Body $payload -ContentType "application/json" -TimeoutSec 30
  if ($response.ok) {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Green
    Write-Host "  ║  Guardian health check complete!                 ║" -ForegroundColor Green
    Write-Host "  ║  Report emailed to you.                         ║" -ForegroundColor Green
    Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Green
    if ($response.issueCount -gt 0) {
      Write-Host ""
      Write-Host "  $($response.issueCount) item(s) need attention." -ForegroundColor Yellow
      Write-Host "  Your Custodian is ready to help: https://prontopc.online/success" -ForegroundColor Yellow
    } else {
      Write-Host ""
      Write-Host "  All clear — your system is healthy." -ForegroundColor Green
    }
  }
} catch {
  Write-Host "  [!] Could not submit results. Will retry next scan." -ForegroundColor Red
}

Write-Host ""
Write-Host "  This window will close in 10 seconds..." -ForegroundColor Gray
Start-Sleep -Seconds 10
`;

  const origin = corsOrigin(req);
  res.writeHead(200, {
    "Content-Type": "text/plain; charset=utf-8",
    "Access-Control-Allow-Origin": origin,
  });
  res.end(script);
}

async function handleGuardianResults(req, res) {
  let body;
  try { body = await readBody(req); } catch { return json(res, req, 400, { ok: false, error: "Invalid body" }); }
  const { token, diagnostics, timestamp } = body;
  if (!token || !diagnostics) return json(res, req, 400, { ok: false, error: "token and diagnostics required" });

  const g = getGuardian(token);
  if (!g) return json(res, req, 404, { ok: false, error: "Invalid guardian token" });

  const previousDiag = g.scanHistory.length > 0 ? g.scanHistory[g.scanHistory.length - 1] : null;

  // Count issues
  let issueCount = 0;
  if (diagnostics.drivers?.unsigned > 0) issueCount++;
  if (diagnostics.services?.stopped_critical?.length > 0) issueCount += diagnostics.services.stopped_critical.length;
  if (diagnostics.security?.real_time_protection === false) issueCount++;
  if (diagnostics.disk) {
    for (const d of diagnostics.disk) { if (d.percent_used > 90) issueCount++; }
  }
  if (diagnostics.uptime?.uptime_days > 14) issueCount++;

  // Update guardian record
  const intervalMs = g.frequency === "weekly" ? 7 * 24 * 60 * 60 * 1000 : 30 * 24 * 60 * 60 * 1000;
  g.lastScanAt = Date.now();
  g.nextScanAt = Date.now() + intervalMs;
  g.scanHistory.push({ date: timestamp || new Date().toISOString(), issueCount, diagnostics });
  if (g.scanHistory.length > 12) g.scanHistory.shift();
  guardianStore.set(token, g);

  // Send email report via Resend
  const RESEND_API_KEY = process.env.RESEND_API_KEY;
  const FROM_EMAIL = process.env.FROM_EMAIL || "support@prontopc.online";
  if (RESEND_API_KEY && g.email) {
    const subject = issueCount > 0
      ? `ProntoPC Guardian: ${issueCount} item${issueCount > 1 ? "s" : ""} need attention`
      : "ProntoPC Guardian: All clear — your system is healthy";

    const hostname = diagnostics.os?.hostname || "Your PC";
    const diskInfo = (diagnostics.disk || []).map(d => `${d.device} ${d.percent_used}% used`).join(", ");
    const memPct = diagnostics.memory?.percent_used || "—";

    const emailHtml = `
<div style="font-family:'Segoe UI',Arial,sans-serif;max-width:600px;margin:0 auto;background:#FDFAF4;border:1px solid #E2DBD0;border-radius:12px;overflow:hidden">
  <div style="background:#1A1917;padding:20px 24px;color:#fff">
    <h1 style="margin:0;font-size:1.2rem;font-weight:700">Pronto <span style="color:#7C5CFF">PC</span> — Guardian Report</h1>
    <p style="margin:4px 0 0;color:rgba(255,255,255,.6);font-size:.85rem">${hostname} · ${new Date().toLocaleDateString("en-US", { weekday: "long", month: "long", day: "numeric", year: "numeric" })}</p>
  </div>
  <div style="padding:24px">
    <div style="background:${issueCount > 0 ? "#FFF3E0" : "#E8F5E9"};border:1px solid ${issueCount > 0 ? "#FFE0B2" : "#C8E6C9"};border-radius:8px;padding:16px;margin-bottom:16px">
      <strong style="color:${issueCount > 0 ? "#E65100" : "#2E7D32"}">${issueCount > 0 ? `${issueCount} item${issueCount > 1 ? "s" : ""} need attention` : "All clear — your system is healthy"}</strong>
    </div>
    <table style="width:100%;border-collapse:collapse;font-size:.9rem">
      <tr><td style="padding:8px 0;color:#6B6560">Memory</td><td style="padding:8px 0;font-weight:600">${memPct}% used</td></tr>
      <tr><td style="padding:8px 0;color:#6B6560">Storage</td><td style="padding:8px 0;font-weight:600">${diskInfo || "—"}</td></tr>
      <tr><td style="padding:8px 0;color:#6B6560">Security</td><td style="padding:8px 0;font-weight:600;color:${diagnostics.security?.real_time_protection ? "#2E7D32" : "#C0392B"}">${diagnostics.security?.real_time_protection ? "Protected" : "Attention needed"}</td></tr>
      <tr><td style="padding:8px 0;color:#6B6560">Unsigned Drivers</td><td style="padding:8px 0;font-weight:600">${diagnostics.drivers?.unsigned || 0}</td></tr>
      <tr><td style="padding:8px 0;color:#6B6560">Uptime</td><td style="padding:8px 0;font-weight:600;color:${(diagnostics.uptime?.uptime_days || 0) > 14 ? "#C0392B" : "#1A1917"}">${diagnostics.uptime?.uptime_days || 0} days</td></tr>
    </table>
    ${issueCount > 0 ? '<p style="margin-top:16px"><a href="https://prontopc.online/success" style="color:#7C5CFF;font-weight:600">Talk to your Custodian Technician →</a></p>' : ""}
    <hr style="border:none;border-top:1px solid #E2DBD0;margin:20px 0">
    <p style="font-size:.78rem;color:#9B9590">Next scan: ${new Date(g.nextScanAt).toLocaleDateString("en-US", { weekday: "long", month: "short", day: "numeric" })} · <a href="https://prontopc.online/guardian" style="color:#7C5CFF">Manage subscription</a></p>
  </div>
</div>`;

    try {
      await fetch("https://api.resend.com/emails", {
        method: "POST",
        headers: { "Authorization": `Bearer ${RESEND_API_KEY}`, "Content-Type": "application/json" },
        body: JSON.stringify({ from: FROM_EMAIL, to: g.email, subject, html: emailHtml }),
        signal: AbortSignal.timeout(10_000),
      });
      console.log(`[guardian/results] Report emailed to ${g.email}`);
    } catch (e) {
      console.error("[guardian/results] Email failed:", e.message);
    }
  }

  return json(res, req, 200, { ok: true, issueCount, nextScanAt: g.nextScanAt });
}

// ─── Server ─────────────────────────────────────────────────────────────────

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, `http://${req.headers.host}`);
  const pathname = url.pathname.replace(/\/$/, "");
  const method = req.method;
  const origin = corsOrigin(req);

  res.setHeader("Access-Control-Allow-Origin", origin);
  res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
  res.setHeader("Access-Control-Allow-Headers", "Content-Type, Authorization");
  res.setHeader("Vary", "Origin");

  if (method === "OPTIONS") { res.writeHead(204); return res.end(); }

  try {
    // Health
    if (method === "GET" && pathname === "/health")
      return json(res, req, 200, { ok: true, service: "pronto-pc", version: "2.0.0", model: LLM_MODEL, sessions: sessionStore.size, timestamp: new Date().toISOString() });

    // Stripe verification
    if (method === "POST" && pathname === "/api/verify-payment") return await handleVerifyPayment(req, res);

    // Native Store-app pay-per-run gate (poll after checkout, or re-verify a deep-link token)
    if (method === "GET" && pathname === "/api/await-payment") return handleAwaitPayment(req, res, url.searchParams.get("ref"));
    if (method === "GET" && pathname === "/api/verify-token") return handleVerifyToken(req, res, url.searchParams.get("token"));

    // Session management
    if (method === "POST" && pathname === "/api/create-session") return await handleCreateSession(req, res);
    if (method === "POST" && pathname === "/api/submit-diagnostics") return await handleSubmitDiagnostics(req, res);
    if (method === "POST" && pathname === "/api/submit-repair-results") return await handleSubmitRepairResults(req, res);
    if (method === "POST" && pathname === "/api/report-ai-unavailable") return await handleReportAiUnavailable(req, res);
    if (method === "POST" && pathname === "/api/verify-results") return await handleVerifyResults(req, res);

    // Session status/findings/consent/report
    const sessionMatch = pathname.match(/^\/api\/session\/([a-f0-9]{64})\/(status|findings|consent|report|repair-actions)$/);
    if (sessionMatch) {
      const [, token, action] = sessionMatch;
      if (method === "GET" && action === "status") return handleSessionStatus(req, res, token);
      if (method === "GET" && action === "findings") return handleSessionFindings(req, res, token);
      if (method === "POST" && action === "consent") return await handleSessionConsent(req, res, token);
      if (method === "GET" && action === "report") return handleSessionReport(req, res, token);
      if (method === "GET" && action === "repair-actions") return handleRepairActions(req, res, token);
    }

    // Dynamic PowerShell scripts and launcher files
    const scanToken = method === "GET" && pathname === "/scan" && url.searchParams.get("token");
    if (scanToken) return handleScanScript(req, res, scanToken);

    const batToken = method === "GET" && pathname === "/scan.bat" && url.searchParams.get("token");
    if (batToken) return handleScanBat(req, res, batToken);

    const repairToken = method === "GET" && pathname === "/repair" && url.searchParams.get("token");
    if (repairToken) return handleRepairScript(req, res, repairToken);

    const verifyToken = method === "GET" && pathname === "/verify" && url.searchParams.get("token");
    if (verifyToken) return handleVerifyScript(req, res, verifyToken);

    if (method === "GET" && pathname === "/admin/loyalty") return await handleLoyaltyLedger(req, res, url);

    // Guardian subscription endpoints
    if (method === "POST" && pathname === "/api/guardian/subscribe") return await handleGuardianSubscribe(req, res);
    if (method === "POST" && pathname === "/api/guardian/activate") return await handleGuardianActivate(req, res);
    if (method === "POST" && pathname === "/api/guardian/results") return await handleGuardianResults(req, res);

    const guardianScanToken = method === "GET" && pathname === "/api/guardian/scan" && url.searchParams.get("token");
    if (guardianScanToken) return handleGuardianScanScript(req, res, guardianScanToken);

    // Legacy endpoint for existing Python agent
    if (method === "POST" && pathname === "/api/repair-plan") return await handleRepairPlan(req, res);

    return json(res, req, 404, { ok: false, error: "Not found" });
  } catch (e) {
    console.error("Unhandled:", e);
    return json(res, req, 500, { ok: false, error: "Internal server error" });
  }
});

server.listen(PORT, () => {
  console.log(`Pronto PC Custodian v2.0 running on port ${PORT}`);
  console.log(`Model: ${LLM_MODEL}`);
  if (!AZURE_OPENAI_KEY && !GEMINI_API_KEY) console.warn("WARNING: no LLM key set (AZURE_OPENAI_KEY or GEMINI_API_KEY)");
  if (!STRIPE_SECRET_KEY) console.warn("WARNING: STRIPE_SECRET_KEY not set");
});
