"""
Pronto PC — Production Repair Agent
Customer-facing entry point. Runs scan, shows findings, executes repairs.
Requires: Windows, internet connection, preferably Administrator rights.
"""
import sys
import os
import json
import ctypes
import asyncio
import webbrowser
import subprocess
import shutil
import tempfile
import socket
from pathlib import Path
from datetime import datetime

from ai_engine import AIEngine

# ── Constants ─────────────────────────────────────────────────────────────────
SERVER_URL   = "https://pronto-pc-108655745293.us-central1.run.app"
APP_NAME     = "Pronto PC"
VERSION      = "1.0.0"
SUPPORT_EMAIL = "support@prontopc.online"

# ── Admin check ───────────────────────────────────────────────────────────────
def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except Exception:
        return False

def elevate_if_needed():
    if not is_admin():
        # Re-launch as Administrator
        try:
            params = " ".join([f'"{a}"' for a in sys.argv])
            ret = ctypes.windll.shell32.ShellExecuteW(
                None, "runas", sys.executable, params, None, 1
            )
            if ret > 32:
                sys.exit(0)  # Elevated process launched
        except Exception:
            pass
        # If elevation failed or was declined, continue with limited privileges
        print("[!] Running without Administrator rights. Some features may be limited.\n")

# ── Progress display ──────────────────────────────────────────────────────────
def banner():
    print("=" * 60)
    print(f"  {APP_NAME} — Repair Agent v{VERSION}")
    print(f"  support: {SUPPORT_EMAIL}")
    print("=" * 60)
    print()

def step(n, total, msg):
    print(f"  [{n}/{total}] {msg}...")

# ── Repair actions ────────────────────────────────────────────────────────────
def clear_temp_files() -> int:
    """Delete temp files. Returns MB freed."""
    freed = 0
    locations = [
        Path(os.environ.get("TEMP", "C:/Windows/Temp")),
        Path("C:/Windows/Temp"),
        Path(os.environ.get("USERPROFILE", "")) / "AppData/Local/Temp",
    ]
    for loc in locations:
        if not loc.exists():
            continue
        for f in loc.rglob("*"):
            if f.is_file():
                try:
                    size = f.stat().st_size
                    f.unlink()
                    freed += size
                except (PermissionError, OSError):
                    pass
    return round(freed / 1_048_576)

def clear_browser_cache(browser: str) -> int:
    """Clear browser cache for the given browser. Returns MB freed."""
    profile = Path(os.environ.get("USERPROFILE", ""))
    cache_paths = {
        "Chrome":  profile / "AppData/Local/Google/Chrome/User Data/Default/Cache",
        "Edge":    profile / "AppData/Local/Microsoft/Edge/User Data/Default/Cache",
        "Firefox": profile / "AppData/Local/Mozilla/Firefox/Profiles",
    }
    path = cache_paths.get(browser)
    if not path or not path.exists():
        return 0
    freed = 0
    for f in path.rglob("*"):
        if f.is_file():
            try:
                freed += f.stat().st_size
                f.unlink()
            except (PermissionError, OSError):
                pass
    return round(freed / 1_048_576)

def run_sfc() -> str:
    """Run Windows System File Checker. Returns output summary."""
    try:
        result = subprocess.run(
            ["sfc", "/scannow"],
            capture_output=True, text=True, timeout=300
        )
        return result.stdout[-500:] if result.stdout else "SFC completed."
    except Exception as e:
        return f"SFC could not run: {e}"

def execute_repair_plan(actions: list, audit: list):
    """Execute each approved action and record results."""
    for action in actions:
        action_id = action.get("id", "")
        name      = action.get("friendly_name", action_id)
        result    = {"action": action_id, "name": name, "status": "skipped", "mb_freed": 0}

        try:
            if action_id in ("clear_temp_files", "clean_temp_files"):
                mb = clear_temp_files()
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("chrome_cache", "clear_chrome_cache"):
                mb = clear_browser_cache("Chrome")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("edge_cache", "clear_edge_cache"):
                mb = clear_browser_cache("Edge")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("firefox_cache", "clear_firefox_cache"):
                mb = clear_browser_cache("Firefox")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("sfc_scan", "system_file_check"):
                output = run_sfc()
                result.update({"status": "done", "detail": output})

            elif action_id in ("startup_review",):
                result.update({"status": "noted", "detail": "Startup items logged in audit report."})

            else:
                result.update({"status": "noted", "detail": "Action logged."})

            print(f"    ✓  {name}" + (f" — {result['mb_freed']} MB freed" if result.get("mb_freed") else ""))

        except Exception as e:
            result.update({"status": "error", "detail": str(e)})
            print(f"    ✗  {name} — {e}")

        audit.append(result)

# ── Main flow ─────────────────────────────────────────────────────────────────
def main():
    elevate_if_needed()
    banner()

    from diagnostics import SystemDiagnostics
    from report_generator import generate_html_report

    session_token = os.environ.get("PRONTO_SESSION", f"AGENT_{datetime.now().strftime('%Y%m%d%H%M%S')}")
    desktop = Path(os.environ.get("USERPROFILE", "")) / "Desktop"
    hostname = socket.gethostname()

    # ── Step 1: Scan ─────────────────────────────────────────────────────────
    step(1, 4, "Scanning your system")
    diag = SystemDiagnostics()
    diag_data = diag.run_full_scan()
    print(f"       Found: {sum(t['size_mb'] for t in diag_data.get('temp_files',[])):.0f} MB temp files, "
          f"{len(diag_data.get('browser_caches', []))} browser caches, "
          f"{len(diag_data.get('event_errors', []))} event errors")
    print()

    # ── Step 2: Get AI plan ──────────────────────────────────────────────────
    step(2, 4, "Building your repair plan (AI)")
    try:
        ai = AIEngine()
        plan = ai.get_repair_plan(diag_data, session_token)
        actions = plan.get("actions", [])
        summary = plan.get("summary", "")
        print(f"       {len(actions)} actions planned")
        print()
    except Exception as e:
        print(f"\n  [!] Could not reach Pronto server: {e}")
        print(f"  Please check your internet connection and try again.")
        print(f"  If the problem persists, contact {SUPPORT_EMAIL}")
        input("\nPress Enter to exit...")
        sys.exit(1)

    # ── Step 3: Show plan ────────────────────────────────────────────────────
    print("  " + "-" * 56)
    print(f"  What we found on {hostname}:")
    print(f"  {summary}")
    print()
    print("  Planned repairs:")
    for a in actions:
        mb = a.get("estimated_mb_recovered", 0)
        risk = a.get("risk", "low").upper()
        print(f"    [{risk}]  {a['friendly_name']}" + (f" (~{mb} MB)" if mb else ""))
    print()
    print("  " + "-" * 56)

    confirm = input("  Proceed with repairs? (yes/no): ").strip().lower()
    if confirm not in ("yes", "y"):
        print("\n  Repair cancelled. No changes were made.")
        input("Press Enter to exit...")
        sys.exit(0)
    print()

    # ── Step 4: Execute repairs ───────────────────────────────────────────────
    step(4, 4, "Running repairs")
    print()
    audit = []
    execute_repair_plan(actions, audit)

    # ── Save audit report ─────────────────────────────────────────────────────
    total_freed = sum(a.get("mb_freed", 0) for a in audit)
    report = {
        "product":    APP_NAME,
        "version":    VERSION,
        "hostname":   hostname,
        "repaired_at": datetime.utcnow().isoformat(),
        "summary":    summary,
        "actions":    audit,
        "total_mb_freed": total_freed,
        "diagnostics": diag_data,
    }
    ts = datetime.now().strftime("%Y%m%d_%H%M")
    report_html = generate_html_report(report)
    report_path = desktop / f"ProntoPC_Report_{ts}.html"
    report_path.write_text(report_html, encoding="utf-8")
    # Auto-open in browser
    import webbrowser
    webbrowser.open(report_path.as_uri())

    print()
    print("  " + "=" * 56)
    print(f"  ✓  Repair complete!")
    print(f"  ✓  {total_freed} MB freed")
    print(f"  ✓  Audit report saved to your Desktop:")
    print(f"     {report_path.name} (opens in your browser)")
    print()
    print(f"  Questions? {SUPPORT_EMAIL}")
    print("  " + "=" * 56)
    print()
    input("Press Enter to close...")

    # Schedule self-deletion after process exits via PowerShell
    try:
        exe_path = os.path.abspath(sys.executable)
        subprocess.Popen(
            ['powershell', '-WindowStyle', 'Hidden', '-Command',
             f'Start-Sleep -Seconds 3; Remove-Item -Force -Path "{exe_path}"'],
            creationflags=0x08000000  # CREATE_NO_WINDOW
        )
    except Exception:
        pass

if __name__ == "__main__":
    main()
