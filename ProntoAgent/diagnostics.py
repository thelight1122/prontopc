"""
Pronto PC — System Diagnostics
Gathers system state before any repairs are made.
All reads only — nothing is modified here.
"""
import os
import subprocess
import winreg
import psutil
import platform
from pathlib import Path
from datetime import datetime


class SystemDiagnostics:

    def run_full_scan(self) -> dict:
        return {
            "scanned_at": datetime.utcnow().isoformat(),
            "os": self._os_info(),
            "temp_files": self._scan_temp_files(),
            "startup_programs": self._scan_startup(),
            "registry": self._scan_registry(),
            "disk": self._disk_health(),
            "browser_caches": self._browser_caches(),
            "memory": self._memory_info(),
            "event_errors": self._recent_event_errors(),
        }

    def _os_info(self) -> dict:
        return {
            "version": platform.version(),
            "release": platform.release(),
            "machine": platform.machine(),
            "processor": platform.processor(),
            "hostname": platform.node(),
        }

    def _scan_temp_files(self) -> list:
        locations = [
            Path(os.environ.get("TEMP", "C:/Windows/Temp")),
            Path("C:/Windows/Temp"),
            Path(os.environ.get("USERPROFILE", "")) / "AppData/Local/Temp",
        ]
        results = []
        for loc in locations:
            if not loc.exists():
                continue
            total_bytes = 0
            file_count = 0
            try:
                for f in loc.rglob("*"):
                    if f.is_file():
                        try:
                            total_bytes += f.stat().st_size
                            file_count += 1
                        except (PermissionError, OSError):
                            pass
            except (PermissionError, OSError):
                pass
            if file_count > 0:
                results.append({
                    "path": str(loc),
                    "file_count": file_count,
                    "size_mb": round(total_bytes / 1_048_576, 1),
                })
        return results

    def _scan_startup(self) -> list:
        items = []
        keys = [
            (winreg.HKEY_CURRENT_USER,
             r"Software\Microsoft\Windows\CurrentVersion\Run"),
            (winreg.HKEY_LOCAL_MACHINE,
             r"Software\Microsoft\Windows\CurrentVersion\Run"),
            (winreg.HKEY_LOCAL_MACHINE,
             r"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
        ]
        for hive, key_path in keys:
            try:
                key = winreg.OpenKey(hive, key_path)
                i = 0
                while True:
                    try:
                        name, value, _ = winreg.EnumValue(key, i)
                        items.append({"name": name, "command": value,
                                      "hive": "HKCU" if hive == winreg.HKEY_CURRENT_USER else "HKLM"})
                        i += 1
                    except OSError:
                        break
                winreg.CloseKey(key)
            except OSError:
                pass
        return items

    def _scan_registry(self) -> dict:
        orphans = []
        mru_keys = [
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs",
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
        ]
        for key_path in mru_keys:
            try:
                key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, key_path)
                i = 0
                while True:
                    try:
                        name, _, _ = winreg.EnumValue(key, i)
                        orphans.append({"key": key_path, "value": name})
                        i += 1
                    except OSError:
                        break
                winreg.CloseKey(key)
            except OSError:
                pass
        return {"mru_entries": len(orphans), "sample": orphans[:5]}

    def _disk_health(self) -> list:
        results = []
        for disk in psutil.disk_partitions():
            if "fixed" not in disk.opts and "cdrom" not in disk.opts:
                continue
            try:
                usage = psutil.disk_usage(disk.mountpoint)
                results.append({
                    "device": disk.device,
                    "mountpoint": disk.mountpoint,
                    "total_gb": round(usage.total / 1_073_741_824, 1),
                    "used_gb": round(usage.used / 1_073_741_824, 1),
                    "free_gb": round(usage.free / 1_073_741_824, 1),
                    "percent_used": usage.percent,
                })
            except (PermissionError, OSError):
                pass
        return results

    def _browser_caches(self) -> list:
        profile = Path(os.environ.get("USERPROFILE", ""))
        cache_paths = {
            "Chrome": profile / "AppData/Local/Google/Chrome/User Data/Default/Cache",
            "Edge": profile / "AppData/Local/Microsoft/Edge/User Data/Default/Cache",
            "Firefox": profile / "AppData/Local/Mozilla/Firefox/Profiles",
        }
        results = []
        for browser, path in cache_paths.items():
            if not path.exists():
                continue
            total = 0
            try:
                for f in path.rglob("*"):
                    if f.is_file():
                        try:
                            total += f.stat().st_size
                        except (PermissionError, OSError):
                            pass
            except (PermissionError, OSError):
                pass
            if total > 0:
                results.append({
                    "browser": browser,
                    "path": str(path),
                    "size_mb": round(total / 1_048_576, 1),
                })
        return results

    def _memory_info(self) -> dict:
        mem = psutil.virtual_memory()
        return {
            "total_gb": round(mem.total / 1_073_741_824, 1),
            "available_gb": round(mem.available / 1_073_741_824, 1),
            "percent_used": mem.percent,
        }

    def _recent_event_errors(self) -> list:
        errors = []
        try:
            result = subprocess.run(
                ["powershell", "-Command",
                 "Get-EventLog -LogName System -EntryType Error -Newest 10 "
                 "| Select-Object TimeGenerated,Source,Message "
                 "| ConvertTo-Json -Compress"],
                capture_output=True, text=True, timeout=15
            )
            if result.returncode == 0 and result.stdout.strip():
                import json
                raw = json.loads(result.stdout)
                if isinstance(raw, dict):
                    raw = [raw]
                for e in raw:
                    errors.append({
                        "time": str(e.get("TimeGenerated", "")),
                        "source": str(e.get("Source", "")),
                        "message": str(e.get("Message", ""))[:200],
                    })
        except Exception:
            pass
        return errors
