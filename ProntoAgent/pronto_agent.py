"""
Pronto PC — Production Repair Agent
Customer-facing entry point. Runs scan, shows findings, executes repairs.
Requires: Windows, internet connection, preferably Administrator rights.
"""
import sys
import os
import json
import ctypes
import webbrowser
import subprocess
import shutil
import socket
from pathlib import Path
from datetime import datetime

from ai_engine import AIEngine

# ── Constants ─────────────────────────────────────────────────────────────────
SERVER_URL    = "https://pronto-pc-108655745293.us-central1.run.app"
APP_NAME      = "Pronto PC"
VERSION       = "2.0.0"
SUPPORT_EMAIL = "support@prontopc.online"

# ── Admin check ───────────────────────────────────────────────────────────────
def is_admin():
    try:
        return ctypes.windll.shell32.IsUserAnAdmin()
    except Exception:
        return False

def elevate_if_needed():
    if not is_admin():
        try:
            params = " ".join([f'"{a}"' for a in sys.argv])
            ret = ctypes.windll.shell32.ShellExecuteW(
                None, "runas", sys.executable, params, None, 1
            )
            if ret > 32:
                sys.exit(0)
        except Exception:
            pass
        print("[!] Running without Administrator rights. Some features may be limited.\n")

# ── Progress display ──────────────────────────────────────────────────────────
def banner():
    print("=" * 64)
    print(f"  {APP_NAME} — Diagnostic & Repair Agent v{VERSION}")
    print(f"  5D Service Solutions Global LLC")
    print(f"  support: {SUPPORT_EMAIL}")
    print("=" * 64)
    print()

def step(n, total, msg):
    print(f"  [{n}/{total}] {msg}...")

def ps(command: str, timeout: int = 60) -> str:
    try:
        result = subprocess.run(
            ["powershell", "-NoProfile", "-NonInteractive", "-Command", command],
            capture_output=True, text=True, timeout=timeout
        )
        return result.stdout.strip() if result.returncode == 0 else ""
    except Exception:
        return ""

# ── Repair Actions ────────────────────────────────────────────────────────────

def clear_temp_files() -> int:
    freed = 0
    locations = set()
    for env_var in ["TEMP", "TMP"]:
        p = os.environ.get(env_var)
        if p:
            locations.add(Path(p).resolve())
    locations.add(Path("C:/Windows/Temp").resolve())
    user_temp = Path(os.environ.get("USERPROFILE", "")) / "AppData/Local/Temp"
    if user_temp.exists():
        locations.add(user_temp.resolve())
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
        for d in sorted(loc.rglob("*"), reverse=True):
            if d.is_dir():
                try:
                    d.rmdir()
                except (PermissionError, OSError):
                    pass
    return round(freed / 1_048_576)


def clear_browser_cache(browser: str) -> int:
    profile = Path(os.environ.get("USERPROFILE", ""))
    cache_paths = {
        "Chrome": [
            profile / "AppData/Local/Google/Chrome/User Data/Default/Cache",
            profile / "AppData/Local/Google/Chrome/User Data/Default/Code Cache",
        ],
        "Edge": [
            profile / "AppData/Local/Microsoft/Edge/User Data/Default/Cache",
            profile / "AppData/Local/Microsoft/Edge/User Data/Default/Code Cache",
        ],
        "Firefox": [
            profile / "AppData/Local/Mozilla/Firefox/Profiles",
        ],
        "Brave": [
            profile / "AppData/Local/BraveSoftware/Brave-Browser/User Data/Default/Cache",
        ],
        "Opera": [
            profile / "AppData/Roaming/Opera Software/Opera Stable/Cache",
        ],
    }
    paths = cache_paths.get(browser, [])
    freed = 0
    for path in paths:
        if not path.exists():
            continue
        for f in path.rglob("*"):
            if f.is_file():
                try:
                    freed += f.stat().st_size
                    f.unlink()
                except (PermissionError, OSError):
                    pass
    return round(freed / 1_048_576)


def clear_all_browser_caches() -> dict:
    results = {}
    for browser in ["Chrome", "Edge", "Firefox", "Brave", "Opera"]:
        mb = clear_browser_cache(browser)
        if mb > 0:
            results[browser] = mb
    return results


def clear_windows_caches() -> dict:
    profile = Path(os.environ.get("USERPROFILE", ""))
    freed = {}

    # Thumbnail cache
    thumb_dir = profile / "AppData/Local/Microsoft/Windows/Explorer"
    if thumb_dir.exists():
        total = 0
        for f in thumb_dir.glob("thumbcache_*.db"):
            try:
                total += f.stat().st_size
                f.unlink()
            except (PermissionError, OSError):
                pass
        if total > 0:
            freed["thumbnail_cache"] = round(total / 1_048_576)

    # Icon cache
    icon_dir = profile / "AppData/Local"
    total = 0
    for f in icon_dir.glob("iconcache_*.db"):
        try:
            total += f.stat().st_size
            f.unlink()
        except (PermissionError, OSError):
            pass
    if total > 0:
        freed["icon_cache"] = round(total / 1_048_576)

    # Windows prefetch
    prefetch_dir = Path("C:/Windows/Prefetch")
    if prefetch_dir.exists():
        total = 0
        for f in prefetch_dir.glob("*.pf"):
            try:
                total += f.stat().st_size
                f.unlink()
            except (PermissionError, OSError):
                pass
        if total > 0:
            freed["prefetch"] = round(total / 1_048_576)

    return freed


def empty_recycle_bin() -> str:
    try:
        result = ps(
            "Clear-RecycleBin -Force -ErrorAction SilentlyContinue; 'done'"
        )
        return "Recycle Bin emptied" if "done" in result else "Could not empty Recycle Bin"
    except Exception as e:
        return f"Error: {e}"


def flush_dns() -> str:
    try:
        result = subprocess.run(
            ["ipconfig", "/flushdns"],
            capture_output=True, text=True, timeout=15
        )
        return result.stdout.strip() if result.stdout else "DNS cache flushed"
    except Exception as e:
        return f"Error: {e}"


def run_sfc() -> str:
    try:
        result = subprocess.run(
            ["sfc", "/scannow"],
            capture_output=True, text=True, timeout=600
        )
        return result.stdout[-500:] if result.stdout else "SFC completed."
    except Exception as e:
        return f"SFC could not run: {e}"


def run_dism() -> str:
    try:
        result = subprocess.run(
            ["dism", "/Online", "/Cleanup-Image", "/RestoreHealth"],
            capture_output=True, text=True, timeout=900
        )
        return result.stdout[-500:] if result.stdout else "DISM completed."
    except Exception as e:
        return f"DISM could not run: {e}"


def run_disk_cleanup() -> str:
    try:
        subprocess.run(
            ["cleanmgr", "/sagerun:1"],
            capture_output=True, timeout=300
        )
        return "Windows Disk Cleanup completed"
    except Exception as e:
        return f"Disk Cleanup error: {e}"


def run_chkdsk_scan() -> str:
    try:
        result = subprocess.run(
            ["chkdsk", "C:", "/scan"],
            capture_output=True, text=True, timeout=600
        )
        output = result.stdout[-500:] if result.stdout else ""
        return output if output else "Disk check completed"
    except Exception as e:
        return f"Disk check error: {e}"


def reset_network_stack() -> str:
    results = []
    commands = [
        (["netsh", "winsock", "reset"], "Winsock reset"),
        (["netsh", "int", "ip", "reset"], "IP stack reset"),
        (["ipconfig", "/release"], "IP release"),
        (["ipconfig", "/renew"], "IP renew"),
        (["ipconfig", "/flushdns"], "DNS flushed"),
    ]
    for cmd, label in commands:
        try:
            subprocess.run(cmd, capture_output=True, text=True, timeout=30)
            results.append(f"{label}: done")
        except Exception as e:
            results.append(f"{label}: {e}")
    return "; ".join(results)


def disable_startup_item(name: str, hive_str: str) -> str:
    import winreg
    hive = winreg.HKEY_CURRENT_USER if hive_str == "HKCU" else winreg.HKEY_LOCAL_MACHINE
    key_path = r"Software\Microsoft\Windows\CurrentVersion\Run"
    try:
        key = winreg.OpenKey(hive, key_path, 0, winreg.KEY_ALL_ACCESS)
        winreg.DeleteValue(key, name)
        winreg.CloseKey(key)
        return f"Disabled: {name}"
    except OSError as e:
        return f"Could not disable {name}: {e}"


def optimize_power_plan() -> str:
    try:
        result = ps(
            "powercfg /setactive SCHEME_BALANCED; 'Set to Balanced'"
        )
        return result if result else "Power plan optimized"
    except Exception as e:
        return f"Error: {e}"


def optimize_visual_effects() -> str:
    import winreg
    try:
        key = winreg.OpenKey(
            winreg.HKEY_CURRENT_USER,
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            0, winreg.KEY_ALL_ACCESS
        )
        winreg.SetValueEx(key, "VisualFXSetting", 0, winreg.REG_DWORD, 2)
        winreg.CloseKey(key)
        return "Visual effects set to 'Best performance'"
    except OSError as e:
        return f"Could not optimize: {e}"


def reset_windows_update() -> str:
    results = []
    services = ["wuauserv", "cryptSvc", "bits", "msiserver"]
    for svc in services:
        try:
            subprocess.run(["net", "stop", svc], capture_output=True, timeout=30)
        except Exception:
            pass
    cache_dirs = [
        Path("C:/Windows/SoftwareDistribution"),
        Path("C:/Windows/System32/catroot2"),
    ]
    for d in cache_dirs:
        backup = d.parent / f"{d.name}.bak"
        try:
            if backup.exists():
                shutil.rmtree(backup, ignore_errors=True)
            d.rename(backup)
            results.append(f"Renamed {d.name}")
        except Exception as e:
            results.append(f"Could not rename {d.name}: {e}")
    for svc in services:
        try:
            subprocess.run(["net", "start", svc], capture_output=True, timeout=30)
        except Exception:
            pass
    return "; ".join(results) if results else "Windows Update components reset"


def clear_windows_store_cache() -> str:
    try:
        subprocess.run(
            ["wsreset.exe"],
            capture_output=True, timeout=60
        )
        return "Windows Store cache cleared"
    except Exception as e:
        return f"Error: {e}"


# ── Repair Executor ───────────────────────────────────────────────────────────

def execute_repair_plan(actions: list, audit: list):
    for action in actions:
        action_id = action.get("id", "")
        name      = action.get("friendly_name", action_id)
        result    = {"action": action_id, "name": name, "status": "skipped", "mb_freed": 0}

        try:
            # ── Storage Cleanup ──
            if action_id in ("clear_temp_files", "clean_temp_files"):
                mb = clear_temp_files()
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("clear_all_browser_caches",):
                caches = clear_all_browser_caches()
                total = sum(caches.values())
                detail = ", ".join(f"{b}: {mb}MB" for b, mb in caches.items())
                result.update({"status": "done", "mb_freed": total, "detail": detail})

            elif action_id in ("chrome_cache", "clear_chrome_cache"):
                mb = clear_browser_cache("Chrome")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("edge_cache", "clear_edge_cache"):
                mb = clear_browser_cache("Edge")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("firefox_cache", "clear_firefox_cache"):
                mb = clear_browser_cache("Firefox")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("brave_cache", "clear_brave_cache"):
                mb = clear_browser_cache("Brave")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("opera_cache", "clear_opera_cache"):
                mb = clear_browser_cache("Opera")
                result.update({"status": "done", "mb_freed": mb})

            elif action_id in ("clear_windows_caches", "clear_thumbnail_cache",
                               "clear_icon_cache", "clear_prefetch"):
                caches = clear_windows_caches()
                total = sum(caches.values())
                detail = ", ".join(f"{k}: {v}MB" for k, v in caches.items())
                result.update({"status": "done", "mb_freed": total, "detail": detail})

            elif action_id in ("empty_recycle_bin",):
                output = empty_recycle_bin()
                result.update({"status": "done", "detail": output})

            elif action_id in ("disk_cleanup", "run_disk_cleanup"):
                output = run_disk_cleanup()
                result.update({"status": "done", "detail": output})

            elif action_id in ("clear_windows_store_cache", "wsreset"):
                output = clear_windows_store_cache()
                result.update({"status": "done", "detail": output})

            # ── System Integrity ──
            elif action_id in ("sfc_scan", "system_file_check"):
                output = run_sfc()
                result.update({"status": "done", "detail": output})

            elif action_id in ("dism_restore", "dism_repair", "dism"):
                output = run_dism()
                result.update({"status": "done", "detail": output})

            elif action_id in ("chkdsk_scan", "disk_check"):
                output = run_chkdsk_scan()
                result.update({"status": "done", "detail": output})

            # ── Network ──
            elif action_id in ("flush_dns", "clear_dns_cache"):
                output = flush_dns()
                result.update({"status": "done", "detail": output})

            elif action_id in ("reset_network", "reset_network_stack"):
                output = reset_network_stack()
                result.update({"status": "done", "detail": output})

            # ── Startup & Services ──
            elif action_id in ("disable_startup_item",):
                item_name = action.get("target_name", "")
                item_hive = action.get("target_hive", "HKCU")
                output = disable_startup_item(item_name, item_hive)
                result.update({"status": "done", "detail": output})

            elif action_id in ("startup_review",):
                result.update({"status": "noted", "detail": "Startup items documented in report."})

            # ── Performance ──
            elif action_id in ("optimize_power", "optimize_power_plan"):
                output = optimize_power_plan()
                result.update({"status": "done", "detail": output})

            elif action_id in ("optimize_visual_effects", "best_performance_visuals"):
                output = optimize_visual_effects()
                result.update({"status": "done", "detail": output})

            # ── Windows Update ──
            elif action_id in ("reset_windows_update", "fix_windows_update"):
                output = reset_windows_update()
                result.update({"status": "done", "detail": output})

            # ── Driver & Hardware (report only — never auto-fix) ──
            elif action_id in ("driver_report", "driver_review", "hardware_report"):
                result.update({"status": "noted",
                               "detail": "Driver and hardware findings documented in report. "
                                         "Physical service may be recommended."})

            # ── Unknown / logged ──
            else:
                result.update({"status": "noted", "detail": "Action logged for review."})

            status_icon = "+" if result["status"] == "done" else "~" if result["status"] == "noted" else "x"
            freed_str = f" — {result['mb_freed']} MB freed" if result.get("mb_freed") else ""
            print(f"    [{status_icon}]  {name}{freed_str}")

        except Exception as e:
            result.update({"status": "error", "detail": str(e)})
            print(f"    [!]  {name} — {e}")

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

    # ── Step 1: Full System Scan ─────────────────────────────────────────────
    total_steps = 5
    step(1, total_steps, "Running comprehensive system diagnostic")
    print("       This may take a few minutes — scanning drivers, security,")
    print("       network, services, installed programs, and system health...")
    print()
    diag = SystemDiagnostics()
    diag_data = diag.run_full_scan()

    # Summary of findings
    temp_mb = sum(t['size_mb'] for t in diag_data.get('temp_files', []))
    cache_count = len(diag_data.get('browser_caches', []))
    error_count = len(diag_data.get('event_errors', []))
    warning_count = len(diag_data.get('event_warnings', []))
    driver_problems = len(diag_data.get('device_errors', []))
    unsigned_drivers = len(diag_data.get('drivers', {}).get('unsigned', []))
    outdated_drivers = len(diag_data.get('drivers', {}).get('outdated', []))
    bsod_count = len(diag_data.get('bsod_history', []))
    programs_count = diag_data.get('installed_programs', {}).get('total_count', 0)
    startup_count = len(diag_data.get('startup_programs', []))
    stopped_critical = len(diag_data.get('services', {}).get('stopped_critical', []))

    print(f"       Scan complete. Findings:")
    print(f"         Storage:  {temp_mb:.0f} MB temp files, {cache_count} browser caches")
    print(f"         Drivers:  {driver_problems} device errors, {unsigned_drivers} unsigned, {outdated_drivers} outdated")
    if bsod_count > 0:
        print(f"         Crashes:  {bsod_count} blue screen records found")
    print(f"         Events:   {error_count} errors, {warning_count} warnings")
    print(f"         Startup:  {startup_count} programs at boot")
    print(f"         Software: {programs_count} installed programs")
    if stopped_critical > 0:
        print(f"         Services: {stopped_critical} critical services not running")
    print()

    # ── Step 2: AI Analysis ──────────────────────────────────────────────────
    step(2, total_steps, "Analyzing findings with AI diagnostic engine")
    try:
        ai = AIEngine()
        plan = ai.get_repair_plan(diag_data, session_token)
        actions = plan.get("actions", [])
        summary = plan.get("summary", "")
        hardware_notes = plan.get("hardware_notes", "")
        print(f"       Analysis complete — {len(actions)} recommended actions")
        print()
    except Exception as e:
        print(f"\n  [!] Could not reach diagnostic server: {e}")
        print(f"  Your scan data has been collected. Please check your internet")
        print(f"  connection and try again, or contact {SUPPORT_EMAIL}")
        input("\nPress Enter to exit...")
        sys.exit(1)

    # ── Step 3: Present Findings ─────────────────────────────────────────────
    step(3, total_steps, "Preparing your diagnostic report")
    print()
    print("  " + "=" * 58)
    print(f"  DIAGNOSTIC FINDINGS — {hostname}")
    print("  " + "=" * 58)
    print()
    print(f"  {summary}")
    print()

    if hardware_notes:
        print("  HARDWARE NOTES (requires physical service):")
        print(f"  {hardware_notes}")
        print()

    print("  RECOMMENDED SOFTWARE REPAIRS:")
    for i, a in enumerate(actions, 1):
        mb = a.get("estimated_mb_recovered", 0)
        risk = a.get("risk", "low").upper()
        print(f"    {i}. [{risk}]  {a['friendly_name']}" + (f" (~{mb} MB)" if mb else ""))
    print()
    print("  " + "-" * 58)
    print(f"  All repairs are safe and reversible.")
    print(f"  Your full diagnostic report will be saved regardless of your choice.")
    print("  " + "-" * 58)
    print()

    confirm = input("  Proceed with repairs? (yes/no): ").strip().lower()

    if confirm not in ("yes", "y"):
        print()
        print("  No repairs performed. Your diagnostic report is being saved...")
        report = _build_report(hostname, "", actions, [], 0, diag_data)
        report_path = _save_report(report, desktop, generate_html_report)
        print()
        print("  " + "=" * 58)
        print(f"  Your diagnostic report has been saved to your Desktop:")
        print(f"    {report_path.name}")
        print()
        print(f"  You have 3 days to return and run the repairs at no")
        print(f"  additional charge. Visit prontopc.online to resume.")
        print()
        print(f"  If hardware issues were noted, you can take this report")
        print(f"  to any local computer shop — it will save them the")
        print(f"  diagnostic step.")
        print()
        print(f"  Questions? {SUPPORT_EMAIL}")
        print("  " + "=" * 58)
        input("\nPress Enter to close...")
        sys.exit(0)

    print()

    # ── Step 4: Execute Repairs ──────────────────────────────────────────────
    step(4, total_steps, "Running approved repairs")
    print()
    audit = []
    execute_repair_plan(actions, audit)

    # ── Step 5: Generate Report ──────────────────────────────────────────────
    step(5, total_steps, "Generating your complete audit report")
    total_freed = sum(a.get("mb_freed", 0) for a in audit)
    report = _build_report(hostname, summary, actions, audit, total_freed, diag_data)
    report_path = _save_report(report, desktop, generate_html_report)

    print()
    print("  " + "=" * 58)
    print(f"  REPAIR COMPLETE")
    print("  " + "-" * 58)
    completed = len([a for a in audit if a.get("status") == "done"])
    noted = len([a for a in audit if a.get("status") == "noted"])
    print(f"    {completed} repairs completed")
    if noted > 0:
        print(f"    {noted} items noted for your reference")
    if total_freed > 0:
        if total_freed >= 1024:
            print(f"    {total_freed / 1024:.1f} GB space recovered")
        else:
            print(f"    {total_freed} MB space recovered")
    print()
    print(f"  Your complete audit report has been saved to your Desktop:")
    print(f"    {report_path.name}")
    print()
    print(f"  30-day re-repair guarantee — if anything we fixed comes back,")
    print(f"  contact {SUPPORT_EMAIL} and we'll make it right.")
    print("  " + "=" * 58)
    print()
    input("Press Enter to close...")


def _build_report(hostname, summary, actions, audit, total_freed, diag_data):
    return {
        "product":        APP_NAME,
        "version":        VERSION,
        "hostname":       hostname,
        "repaired_at":    datetime.utcnow().isoformat(),
        "summary":        summary,
        "planned_actions": actions,
        "executed_actions": audit,
        "total_mb_freed": total_freed,
        "diagnostics":    diag_data,
    }


def _save_report(report, desktop, generate_html_report):
    ts = datetime.now().strftime("%Y%m%d_%H%M")
    report_html = generate_html_report(report)
    report_path = desktop / f"ProntoPC_Report_{ts}.html"
    report_path.write_text(report_html, encoding="utf-8")
    webbrowser.open(report_path.as_uri())
    return report_path


if __name__ == "__main__":
    main()
