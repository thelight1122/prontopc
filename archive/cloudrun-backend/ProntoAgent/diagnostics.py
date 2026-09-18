"""
Pronto PC — System Diagnostics
Comprehensive, professional-grade system diagnostic scan.
All reads only — nothing is modified here.
"""
import os
import subprocess
import winreg
import psutil
import platform
import json
import glob as glob_mod
from pathlib import Path
from datetime import datetime, timedelta


class SystemDiagnostics:

    def run_full_scan(self) -> dict:
        return {
            "scanned_at": datetime.utcnow().isoformat(),
            "os": self._os_info(),
            "uptime": self._system_uptime(),
            "temp_files": self._scan_temp_files(),
            "recycle_bin": self._recycle_bin_size(),
            "startup_programs": self._scan_startup(),
            "scheduled_tasks": self._scan_scheduled_tasks(),
            "registry": self._scan_registry(),
            "disk": self._disk_health(),
            "smart_health": self._smart_disk_health(),
            "fragmentation": self._fragmentation_status(),
            "browser_caches": self._browser_caches(),
            "windows_caches": self._windows_caches(),
            "memory": self._memory_info(),
            "page_file": self._page_file_info(),
            "cpu": self._cpu_info(),
            "battery": self._battery_health(),
            "drivers": self._driver_audit(),
            "device_errors": self._device_manager_problems(),
            "bsod_history": self._bsod_history(),
            "network": self._network_health(),
            "dns_cache": self._dns_cache_stats(),
            "security": self._security_status(),
            "windows_update": self._windows_update_status(),
            "activation": self._activation_status(),
            "services": self._services_audit(),
            "installed_programs": self._installed_programs_audit(),
            "user_accounts": self._user_accounts(),
            "power_plan": self._power_plan(),
            "visual_effects": self._visual_effects(),
            "event_errors": self._recent_event_errors(),
            "event_warnings": self._recent_event_warnings(),
            "large_files": self._large_file_finder(),
        }

    # ── OS & System ──────────────────────────────────────────────────────────

    def _os_info(self) -> dict:
        info = {
            "version": platform.version(),
            "release": platform.release(),
            "edition": "",
            "build": "",
            "machine": platform.machine(),
            "processor": platform.processor(),
            "hostname": platform.node(),
        }
        try:
            result = self._ps(
                "(Get-CimInstance Win32_OperatingSystem | Select-Object Caption,BuildNumber,OSArchitecture) "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                info["edition"] = data.get("Caption", "")
                info["build"] = data.get("BuildNumber", "")
        except Exception:
            pass
        return info

    def _system_uptime(self) -> dict:
        try:
            boot = datetime.fromtimestamp(psutil.boot_time())
            uptime = datetime.now() - boot
            return {
                "boot_time": boot.isoformat(),
                "uptime_hours": round(uptime.total_seconds() / 3600, 1),
                "uptime_days": uptime.days,
                "needs_reboot": uptime.days > 14,
            }
        except Exception:
            return {"error": "Could not determine uptime"}

    # ── Storage ──────────────────────────────────────────────────────────────

    def _scan_temp_files(self) -> list:
        locations = [
            Path(os.environ.get("TEMP", "C:/Windows/Temp")),
            Path("C:/Windows/Temp"),
            Path(os.environ.get("USERPROFILE", "")) / "AppData/Local/Temp",
        ]
        seen = set()
        results = []
        for loc in locations:
            resolved = str(loc.resolve())
            if resolved in seen or not loc.exists():
                continue
            seen.add(resolved)
            total_bytes = 0
            file_count = 0
            oldest = None
            try:
                for f in loc.rglob("*"):
                    if f.is_file():
                        try:
                            stat = f.stat()
                            total_bytes += stat.st_size
                            file_count += 1
                            mtime = datetime.fromtimestamp(stat.st_mtime)
                            if oldest is None or mtime < oldest:
                                oldest = mtime
                        except (PermissionError, OSError):
                            pass
            except (PermissionError, OSError):
                pass
            if file_count > 0:
                results.append({
                    "path": str(loc),
                    "file_count": file_count,
                    "size_mb": round(total_bytes / 1_048_576, 1),
                    "oldest_file": oldest.isoformat() if oldest else None,
                })
        return results

    def _recycle_bin_size(self) -> dict:
        try:
            result = self._ps(
                "(New-Object -ComObject Shell.Application).NameSpace(10).Items() | "
                "Measure-Object -Property Size -Sum | Select-Object Count,Sum | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                return {
                    "item_count": data.get("Count", 0),
                    "size_mb": round((data.get("Sum", 0) or 0) / 1_048_576, 1),
                }
        except Exception:
            pass
        return {"item_count": 0, "size_mb": 0}

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
                    "fstype": disk.fstype,
                    "total_gb": round(usage.total / 1_073_741_824, 1),
                    "used_gb": round(usage.used / 1_073_741_824, 1),
                    "free_gb": round(usage.free / 1_073_741_824, 1),
                    "percent_used": usage.percent,
                    "health_status": "critical" if usage.percent > 95 else "warning" if usage.percent > 85 else "healthy",
                })
            except (PermissionError, OSError):
                pass
        return results

    def _smart_disk_health(self) -> list:
        try:
            result = self._ps(
                "Get-CimInstance -Namespace root\\wmi -ClassName MSStorageDriver_FailurePredictStatus 2>$null | "
                "Select-Object InstanceName,PredictFailure,Reason | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                return [
                    {
                        "drive": d.get("InstanceName", "")[:40],
                        "failure_predicted": d.get("PredictFailure", False),
                        "reason": d.get("Reason", 0),
                    }
                    for d in data
                ]
        except Exception:
            pass
        return []

    def _fragmentation_status(self) -> list:
        try:
            result = self._ps(
                "Get-Volume | Where-Object {$_.DriveType -eq 'Fixed' -and $_.DriveLetter} | "
                "ForEach-Object { $a = Optimize-Volume -DriveLetter $_.DriveLetter -Analyze -Verbose 4>&1 2>$null; "
                "[PSCustomObject]@{Drive=$_.DriveLetter; SizeGB=[math]::Round($_.Size/1GB,1); "
                "SizeRemaining=[math]::Round($_.SizeRemaining/1GB,1); "
                "MediaType=if($_.FileSystemType){'SSD'}else{'HDD'}; "
                "AnalysisOutput=$a -join ' '} } | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                return data
        except Exception:
            pass
        return []

    def _large_file_finder(self) -> list:
        large_files = []
        user_profile = Path(os.environ.get("USERPROFILE", ""))
        scan_dirs = [
            user_profile / "Downloads",
            user_profile / "Documents",
            user_profile / "Desktop",
            user_profile / "Videos",
        ]
        for scan_dir in scan_dirs:
            if not scan_dir.exists():
                continue
            try:
                for f in scan_dir.rglob("*"):
                    if f.is_file():
                        try:
                            size = f.stat().st_size
                            if size > 100_000_000:  # > 100 MB
                                large_files.append({
                                    "path": str(f),
                                    "size_mb": round(size / 1_048_576, 1),
                                    "extension": f.suffix.lower(),
                                })
                        except (PermissionError, OSError):
                            pass
            except (PermissionError, OSError):
                pass
        large_files.sort(key=lambda x: x["size_mb"], reverse=True)
        return large_files[:20]

    # ── Browser & Windows Caches ─────────────────────────────────────────────

    def _browser_caches(self) -> list:
        profile = Path(os.environ.get("USERPROFILE", ""))
        cache_paths = {
            "Chrome": profile / "AppData/Local/Google/Chrome/User Data/Default/Cache",
            "Chrome (Code Cache)": profile / "AppData/Local/Google/Chrome/User Data/Default/Code Cache",
            "Edge": profile / "AppData/Local/Microsoft/Edge/User Data/Default/Cache",
            "Edge (Code Cache)": profile / "AppData/Local/Microsoft/Edge/User Data/Default/Code Cache",
            "Firefox": profile / "AppData/Local/Mozilla/Firefox/Profiles",
            "Brave": profile / "AppData/Local/BraveSoftware/Brave-Browser/User Data/Default/Cache",
            "Opera": profile / "AppData/Roaming/Opera Software/Opera Stable/Cache",
        }
        results = []
        for browser, path in cache_paths.items():
            if not path.exists():
                continue
            total = 0
            file_count = 0
            try:
                for f in path.rglob("*"):
                    if f.is_file():
                        try:
                            total += f.stat().st_size
                            file_count += 1
                        except (PermissionError, OSError):
                            pass
            except (PermissionError, OSError):
                pass
            if total > 0:
                results.append({
                    "browser": browser,
                    "path": str(path),
                    "size_mb": round(total / 1_048_576, 1),
                    "file_count": file_count,
                })
        return results

    def _windows_caches(self) -> dict:
        caches = {}
        profile = Path(os.environ.get("USERPROFILE", ""))
        cache_locations = {
            "thumbnail_cache": profile / "AppData/Local/Microsoft/Windows/Explorer",
            "icon_cache": profile / "AppData/Local",
            "font_cache": Path("C:/Windows/ServiceProfiles/LocalService/AppData/Local/FontCache"),
            "windows_store_cache": profile / "AppData/Local/Packages",
            "prefetch": Path("C:/Windows/Prefetch"),
        }
        for name, path in cache_locations.items():
            if not path.exists():
                continue
            total = 0
            count = 0
            try:
                pattern = "thumbcache_*.db" if name == "thumbnail_cache" else \
                          "iconcache_*.db" if name == "icon_cache" else \
                          "*.pf" if name == "prefetch" else "*"
                for f in path.glob(pattern):
                    if f.is_file():
                        try:
                            total += f.stat().st_size
                            count += 1
                        except (PermissionError, OSError):
                            pass
            except (PermissionError, OSError):
                pass
            if total > 0:
                caches[name] = {"size_mb": round(total / 1_048_576, 1), "file_count": count}
        return caches

    # ── Memory & CPU ─────────────────────────────────────────────────────────

    def _memory_info(self) -> dict:
        mem = psutil.virtual_memory()
        return {
            "total_gb": round(mem.total / 1_073_741_824, 1),
            "available_gb": round(mem.available / 1_073_741_824, 1),
            "used_gb": round(mem.used / 1_073_741_824, 1),
            "percent_used": mem.percent,
            "health_status": "critical" if mem.percent > 90 else "warning" if mem.percent > 80 else "healthy",
        }

    def _page_file_info(self) -> dict:
        try:
            result = self._ps(
                "Get-CimInstance Win32_PageFileUsage | Select-Object Name,CurrentUsage,AllocatedBaseSize,PeakUsage "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                return {
                    "files": [
                        {
                            "path": pf.get("Name", ""),
                            "allocated_mb": pf.get("AllocatedBaseSize", 0),
                            "current_usage_mb": pf.get("CurrentUsage", 0),
                            "peak_usage_mb": pf.get("PeakUsage", 0),
                        }
                        for pf in data
                    ]
                }
        except Exception:
            pass
        return {"files": []}

    def _cpu_info(self) -> dict:
        info = {
            "physical_cores": psutil.cpu_count(logical=False),
            "logical_cores": psutil.cpu_count(logical=True),
            "current_percent": psutil.cpu_percent(interval=2),
            "per_core_percent": psutil.cpu_percent(interval=0, percpu=True),
            "frequency_mhz": None,
        }
        try:
            freq = psutil.cpu_freq()
            if freq:
                info["frequency_mhz"] = {
                    "current": round(freq.current, 0),
                    "min": round(freq.min, 0),
                    "max": round(freq.max, 0),
                }
        except Exception:
            pass
        try:
            result = self._ps(
                "Get-CimInstance Win32_Processor | Select-Object Name,CurrentClockSpeed,MaxClockSpeed,"
                "LoadPercentage,NumberOfCores,ThreadCount | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    info["processor_name"] = data.get("Name", "")
                    info["current_clock_mhz"] = data.get("CurrentClockSpeed", 0)
                    info["max_clock_mhz"] = data.get("MaxClockSpeed", 0)
        except Exception:
            pass
        return info

    def _battery_health(self) -> dict:
        battery = psutil.sensors_battery()
        if battery is None:
            return {"present": False}
        info = {
            "present": True,
            "percent": battery.percent,
            "plugged_in": battery.power_plugged,
            "seconds_remaining": battery.secsleft if battery.secsleft > 0 else None,
        }
        try:
            result = self._ps(
                "Get-CimInstance Win32_Battery | Select-Object EstimatedChargeRemaining,BatteryStatus,"
                "DesignCapacity,FullChargeCapacity,Status | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    design = data.get("DesignCapacity", 0)
                    full = data.get("FullChargeCapacity", 0)
                    if design and full and design > 0:
                        info["design_capacity"] = design
                        info["full_charge_capacity"] = full
                        info["wear_percent"] = round((1 - full / design) * 100, 1)
                    info["wmi_status"] = data.get("Status", "")
        except Exception:
            pass
        return info

    # ── Drivers ──────────────────────────────────────────────────────────────

    def _driver_audit(self) -> dict:
        drivers = {
            "total": 0,
            "signed": 0,
            "unsigned": [],
            "problem_drivers": [],
            "outdated": [],
            "third_party": [],
            "recently_updated": [],
        }
        try:
            result = self._ps(
                "Get-CimInstance Win32_PnPSignedDriver | "
                "Select-Object DeviceName,DriverVersion,DriverDate,Manufacturer,IsSigned,DriverProviderName,InfName "
                "| ConvertTo-Json -Compress", timeout=30
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                drivers["total"] = len(data)
                cutoff_outdated = datetime.now() - timedelta(days=730)  # 2 years
                cutoff_recent = datetime.now() - timedelta(days=30)
                for d in data:
                    name = d.get("DeviceName", "") or ""
                    manufacturer = d.get("Manufacturer", "") or ""
                    provider = d.get("DriverProviderName", "") or ""
                    version = d.get("DriverVersion", "") or ""
                    is_signed = d.get("IsSigned", True)
                    driver_date_raw = d.get("DriverDate", "")

                    if is_signed:
                        drivers["signed"] += 1
                    else:
                        drivers["unsigned"].append({
                            "device": name,
                            "manufacturer": manufacturer,
                            "version": version,
                        })

                    driver_date = self._parse_wmi_date(driver_date_raw)

                    if driver_date:
                        if driver_date < cutoff_outdated and provider.lower() not in ("microsoft",):
                            drivers["outdated"].append({
                                "device": name,
                                "version": version,
                                "date": driver_date.strftime("%Y-%m-%d"),
                                "provider": provider,
                                "age_days": (datetime.now() - driver_date).days,
                            })
                        if driver_date > cutoff_recent:
                            drivers["recently_updated"].append({
                                "device": name,
                                "version": version,
                                "date": driver_date.strftime("%Y-%m-%d"),
                                "provider": provider,
                            })

                    if provider.lower() not in ("microsoft", "microsoft corporation", ""):
                        drivers["third_party"].append({
                            "device": name,
                            "provider": provider,
                            "version": version,
                        })

                drivers["outdated"] = drivers["outdated"][:15]
                drivers["third_party"] = drivers["third_party"][:20]
                drivers["unsigned"] = drivers["unsigned"][:10]
        except Exception as e:
            drivers["error"] = str(e)
        return drivers

    def _device_manager_problems(self) -> list:
        problems = []
        try:
            result = self._ps(
                "Get-CimInstance Win32_PnPEntity | Where-Object {$_.ConfigManagerErrorCode -ne 0} | "
                "Select-Object Name,DeviceID,ConfigManagerErrorCode,Status,Manufacturer "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                error_codes = {
                    1: "Not configured correctly",
                    3: "Driver may be corrupted",
                    10: "Device cannot start",
                    12: "Cannot find enough free resources",
                    14: "Cannot work properly until restart",
                    16: "Cannot identify all resources",
                    18: "Reinstall drivers",
                    19: "Registry may be corrupted",
                    21: "Windows is removing this device",
                    22: "Device is disabled",
                    24: "Device not present or not working",
                    28: "Drivers not installed",
                    29: "Device disabled — firmware did not provide resources",
                    31: "Device not working properly",
                    32: "Driver for this device has been disabled",
                    33: "Cannot determine required resources",
                    34: "Cannot determine settings",
                    35: "System firmware lacks information for configuration",
                    36: "Device requesting PCI interrupt",
                    37: "Cannot initialize the driver",
                    38: "Cannot load — previous instance still in memory",
                    39: "Cannot load — corrupted or missing",
                    40: "Registry service key info is missing or incorrectly recorded",
                    41: "Successfully loaded but cannot find hardware",
                    42: "Cannot load — duplicate device already running",
                    43: "Driver has reported a problem",
                    44: "Application or service shut down the device",
                    45: "Device not currently connected",
                    46: "Cannot gain access — OS shutting down",
                    47: "Cannot use — prepared for safe removal",
                    48: "Software for device blocked from starting",
                    49: "System hive too large",
                    52: "Driver not signed",
                }
                for d in data:
                    code = d.get("ConfigManagerErrorCode", 0)
                    problems.append({
                        "device": d.get("Name", ""),
                        "device_id": (d.get("DeviceID", "") or "")[:60],
                        "error_code": code,
                        "error_description": error_codes.get(code, f"Unknown error code {code}"),
                        "status": d.get("Status", ""),
                        "manufacturer": d.get("Manufacturer", ""),
                    })
        except Exception:
            pass
        return problems

    def _bsod_history(self) -> list:
        crashes = []
        minidump_dir = Path("C:/Windows/Minidump")
        if minidump_dir.exists():
            try:
                dumps = sorted(minidump_dir.glob("*.dmp"), key=lambda f: f.stat().st_mtime, reverse=True)
                for dump in dumps[:10]:
                    try:
                        stat = dump.stat()
                        crashes.append({
                            "file": dump.name,
                            "date": datetime.fromtimestamp(stat.st_mtime).isoformat(),
                            "size_kb": round(stat.st_size / 1024, 1),
                        })
                    except (PermissionError, OSError):
                        pass
            except (PermissionError, OSError):
                pass
        try:
            result = self._ps(
                "Get-EventLog -LogName System -EntryType Error -Source 'BugCheck','Microsoft-Windows-WER-SystemErrorReporting' "
                "-Newest 10 2>$null | Select-Object TimeGenerated,Source,Message | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                for e in data:
                    crashes.append({
                        "source": "EventLog",
                        "date": str(e.get("TimeGenerated", "")),
                        "message": str(e.get("Message", ""))[:200],
                    })
        except Exception:
            pass
        return crashes

    # ── Network ──────────────────────────────────────────────────────────────

    def _network_health(self) -> dict:
        info = {"adapters": [], "connectivity": {}}
        try:
            result = self._ps(
                "Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | "
                "Select-Object Name,InterfaceDescription,MacAddress,LinkSpeed,Status "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                info["adapters"] = [
                    {
                        "name": a.get("Name", ""),
                        "description": a.get("InterfaceDescription", ""),
                        "speed": a.get("LinkSpeed", ""),
                        "status": a.get("Status", ""),
                    }
                    for a in data
                ]
        except Exception:
            pass
        try:
            result = self._ps(
                "Get-NetIPConfiguration | Where-Object {$_.IPv4DefaultGateway} | "
                "Select-Object InterfaceAlias,@{n='IPv4';e={$_.IPv4Address.IPAddress}},"
                "@{n='Gateway';e={$_.IPv4DefaultGateway.NextHop}},"
                "@{n='DNS';e={($_.DNSServer.ServerAddresses) -join ','}} "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                info["connectivity"] = {
                    "interfaces": [
                        {
                            "name": c.get("InterfaceAlias", ""),
                            "ipv4": c.get("IPv4", ""),
                            "gateway": c.get("Gateway", ""),
                            "dns_servers": c.get("DNS", ""),
                        }
                        for c in data
                    ]
                }
        except Exception:
            pass
        try:
            result = self._ps("Test-Connection -ComputerName 8.8.8.8 -Count 2 -Quiet")
            info["connectivity"]["internet_reachable"] = result.strip().lower() == "true" if result else False
        except Exception:
            info["connectivity"]["internet_reachable"] = None
        return info

    def _dns_cache_stats(self) -> dict:
        try:
            result = self._ps(
                "(Get-DnsClientCache | Measure-Object).Count"
            )
            if result:
                return {"entries": int(result.strip())}
        except Exception:
            pass
        return {"entries": 0}

    # ── Security ─────────────────────────────────────────────────────────────

    def _security_status(self) -> dict:
        security = {
            "defender": {},
            "firewall": {},
        }
        try:
            result = self._ps(
                "Get-MpComputerStatus | Select-Object RealTimeProtectionEnabled,AntivirusEnabled,"
                "AntispywareEnabled,BehaviorMonitorEnabled,IoavProtectionEnabled,NISEnabled,"
                "OnAccessProtectionEnabled,AntivirusSignatureLastUpdated,QuickScanEndTime,"
                "FullScanEndTime | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                security["defender"] = {
                    "real_time_protection": data.get("RealTimeProtectionEnabled", False),
                    "antivirus_enabled": data.get("AntivirusEnabled", False),
                    "antispyware_enabled": data.get("AntispywareEnabled", False),
                    "behavior_monitor": data.get("BehaviorMonitorEnabled", False),
                    "signature_last_updated": str(data.get("AntivirusSignatureLastUpdated", "")),
                    "last_quick_scan": str(data.get("QuickScanEndTime", "")),
                    "last_full_scan": str(data.get("FullScanEndTime", "")),
                }
        except Exception:
            security["defender"]["error"] = "Could not query Defender status"
        try:
            result = self._ps(
                "Get-NetFirewallProfile | Select-Object Name,Enabled | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                security["firewall"] = {
                    "profiles": [
                        {"name": p.get("Name", ""), "enabled": p.get("Enabled", False)}
                        for p in data
                    ]
                }
        except Exception:
            security["firewall"]["error"] = "Could not query firewall status"
        return security

    def _windows_update_status(self) -> dict:
        info = {}
        try:
            result = self._ps(
                "Get-HotFix | Sort-Object InstalledOn -Descending -ErrorAction SilentlyContinue | "
                "Select-Object -First 5 HotFixID,InstalledOn,Description | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                info["recent_updates"] = [
                    {
                        "id": u.get("HotFixID", ""),
                        "installed": str(u.get("InstalledOn", "")),
                        "type": u.get("Description", ""),
                    }
                    for u in data
                ]
                if data:
                    last_date_str = str(data[0].get("InstalledOn", ""))
                    info["last_update_date"] = last_date_str
        except Exception:
            info["error"] = "Could not query update history"
        try:
            result = self._ps(
                "$s = New-Object -ComObject Microsoft.Update.Session; "
                "$u = $s.CreateUpdateSearcher(); "
                "$r = $u.Search('IsInstalled=0 and IsHidden=0'); "
                "$r.Updates.Count"
            )
            if result:
                info["pending_updates"] = int(result.strip())
        except Exception:
            info["pending_updates"] = None
        return info

    def _activation_status(self) -> dict:
        try:
            result = self._ps(
                "(Get-CimInstance SoftwareLicensingProduct | "
                "Where-Object {$_.PartialProductKey -and $_.Name -like '*Windows*'} | "
                "Select-Object Name,LicenseStatus)[0] | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                status_map = {0: "Unlicensed", 1: "Licensed", 2: "OOBGrace", 3: "OOTGrace",
                              4: "NonGenuineGrace", 5: "Notification", 6: "ExtendedGrace"}
                status_code = data.get("LicenseStatus", -1)
                return {
                    "product": data.get("Name", ""),
                    "status_code": status_code,
                    "status": status_map.get(status_code, "Unknown"),
                    "is_activated": status_code == 1,
                }
        except Exception:
            pass
        return {"is_activated": None, "error": "Could not determine activation status"}

    # ── Startup & Scheduled Tasks ────────────────────────────────────────────

    def _scan_startup(self) -> list:
        items = []
        keys = [
            (winreg.HKEY_CURRENT_USER,
             r"Software\Microsoft\Windows\CurrentVersion\Run"),
            (winreg.HKEY_LOCAL_MACHINE,
             r"Software\Microsoft\Windows\CurrentVersion\Run"),
            (winreg.HKEY_LOCAL_MACHINE,
             r"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"),
            (winreg.HKEY_CURRENT_USER,
             r"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
            (winreg.HKEY_LOCAL_MACHINE,
             r"Software\Microsoft\Windows\CurrentVersion\RunOnce"),
        ]
        for hive, key_path in keys:
            try:
                key = winreg.OpenKey(hive, key_path)
                i = 0
                while True:
                    try:
                        name, value, _ = winreg.EnumValue(key, i)
                        items.append({
                            "name": name,
                            "command": value,
                            "hive": "HKCU" if hive == winreg.HKEY_CURRENT_USER else "HKLM",
                            "key": key_path,
                        })
                        i += 1
                    except OSError:
                        break
                winreg.CloseKey(key)
            except OSError:
                pass
        try:
            result = self._ps(
                "Get-CimInstance Win32_StartupCommand | Select-Object Name,Command,Location,User "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                existing_names = {i["name"].lower() for i in items}
                for s in data:
                    name = s.get("Name", "")
                    if name.lower() not in existing_names:
                        items.append({
                            "name": name,
                            "command": s.get("Command", ""),
                            "hive": s.get("Location", ""),
                            "key": "WMI",
                        })
        except Exception:
            pass
        return items

    def _scan_scheduled_tasks(self) -> list:
        tasks = []
        try:
            result = self._ps(
                "Get-ScheduledTask | Where-Object {$_.State -ne 'Disabled' -and $_.TaskPath -notlike '\\Microsoft\\*'} | "
                "Select-Object TaskName,TaskPath,State,@{n='Actions';e={($_.Actions.Execute) -join ';'}},"
                "@{n='Author';e={$_.Principal.UserId}} "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                for t in data:
                    tasks.append({
                        "name": t.get("TaskName", ""),
                        "path": t.get("TaskPath", ""),
                        "state": t.get("State", ""),
                        "actions": t.get("Actions", ""),
                        "author": t.get("Author", ""),
                    })
        except Exception:
            pass
        return tasks

    # ── Registry ─────────────────────────────────────────────────────────────

    def _scan_registry(self) -> dict:
        orphans = []
        mru_keys = [
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs",
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\RunMRU",
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU",
            r"Software\Microsoft\Windows\CurrentVersion\Explorer\TypedPaths",
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
        return {"mru_entries": len(orphans), "keys_checked": len(mru_keys), "sample": orphans[:10]}

    # ── Services ─────────────────────────────────────────────────────────────

    def _services_audit(self) -> dict:
        audit = {"stopped_critical": [], "unnecessary_running": [], "failed": []}
        critical_services = [
            "wuauserv", "WinDefend", "mpssvc", "EventLog", "Dhcp", "Dnscache",
            "LanmanWorkstation", "LanmanServer", "RpcSs", "Schedule", "SENS",
            "Winmgmt", "W32Time",
        ]
        try:
            result = self._ps(
                "Get-Service | Select-Object Name,DisplayName,Status,StartType | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                for svc in data:
                    name = svc.get("Name", "")
                    status = str(svc.get("Status", ""))
                    start_type = str(svc.get("StartType", ""))
                    if name in critical_services and status != "4":  # 4 = Running
                        audit["stopped_critical"].append({
                            "name": name,
                            "display_name": svc.get("DisplayName", ""),
                            "status": status,
                            "start_type": start_type,
                        })
        except Exception:
            pass
        try:
            result = self._ps(
                "Get-EventLog -LogName System -EntryType Error -Source 'Service Control Manager' -Newest 10 2>$null | "
                "Select-Object TimeGenerated,Message | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                audit["failed"] = [
                    {
                        "time": str(e.get("TimeGenerated", "")),
                        "message": str(e.get("Message", ""))[:200],
                    }
                    for e in data
                ]
        except Exception:
            pass
        return audit

    # ── Installed Programs ───────────────────────────────────────────────────

    def _installed_programs_audit(self) -> dict:
        programs = []
        reg_paths = [
            (winreg.HKEY_LOCAL_MACHINE, r"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
            (winreg.HKEY_LOCAL_MACHINE, r"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (winreg.HKEY_CURRENT_USER, r"Software\Microsoft\Windows\CurrentVersion\Uninstall"),
        ]
        for hive, key_path in reg_paths:
            try:
                key = winreg.OpenKey(hive, key_path)
                i = 0
                while True:
                    try:
                        subkey_name = winreg.EnumKey(key, i)
                        subkey = winreg.OpenKey(key, subkey_name)
                        try:
                            name = winreg.QueryValueEx(subkey, "DisplayName")[0]
                            version = ""
                            publisher = ""
                            install_date = ""
                            size = 0
                            try:
                                version = winreg.QueryValueEx(subkey, "DisplayVersion")[0]
                            except OSError:
                                pass
                            try:
                                publisher = winreg.QueryValueEx(subkey, "Publisher")[0]
                            except OSError:
                                pass
                            try:
                                install_date = winreg.QueryValueEx(subkey, "InstallDate")[0]
                            except OSError:
                                pass
                            try:
                                size = winreg.QueryValueEx(subkey, "EstimatedSize")[0]
                            except OSError:
                                pass
                            programs.append({
                                "name": name,
                                "version": version,
                                "publisher": publisher,
                                "install_date": install_date,
                                "size_kb": size,
                            })
                        except OSError:
                            pass
                        winreg.CloseKey(subkey)
                        i += 1
                    except OSError:
                        break
                winreg.CloseKey(key)
            except OSError:
                pass
        seen = set()
        unique = []
        for p in programs:
            key = p["name"].lower()
            if key not in seen:
                seen.add(key)
                unique.append(p)
        unique.sort(key=lambda x: x["name"].lower())
        return {
            "total_count": len(unique),
            "programs": unique,
        }

    # ── User Accounts ────────────────────────────────────────────────────────

    def _user_accounts(self) -> list:
        accounts = []
        try:
            result = self._ps(
                "Get-LocalUser | Select-Object Name,Enabled,LastLogon,PasswordLastSet,Description "
                "| ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                if isinstance(data, dict):
                    data = [data]
                for u in data:
                    accounts.append({
                        "name": u.get("Name", ""),
                        "enabled": u.get("Enabled", False),
                        "last_logon": str(u.get("LastLogon", "")),
                        "description": u.get("Description", ""),
                    })
        except Exception:
            pass
        return accounts

    # ── Power & Visual Effects ───────────────────────────────────────────────

    def _power_plan(self) -> dict:
        try:
            result = self._ps(
                "Get-CimInstance -Namespace root\\cimv2\\power -ClassName Win32_PowerPlan | "
                "Where-Object {$_.IsActive} | Select-Object ElementName | ConvertTo-Json -Compress"
            )
            if result:
                data = json.loads(result)
                return {"active_plan": data.get("ElementName", "Unknown")}
        except Exception:
            pass
        try:
            result = self._ps("powercfg /getactivescheme")
            if result:
                return {"active_plan": result.strip()}
        except Exception:
            pass
        return {"active_plan": "Unknown"}

    def _visual_effects(self) -> dict:
        try:
            key = winreg.OpenKey(
                winreg.HKEY_CURRENT_USER,
                r"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects"
            )
            val, _ = winreg.QueryValueEx(key, "VisualFXSetting")
            winreg.CloseKey(key)
            settings = {0: "Let Windows choose", 1: "Best appearance", 2: "Best performance", 3: "Custom"}
            return {"setting": settings.get(val, f"Unknown ({val})"), "code": val}
        except OSError:
            return {"setting": "Default (not configured)", "code": None}

    # ── Event Logs ───────────────────────────────────────────────────────────

    def _recent_event_errors(self) -> list:
        errors = []
        try:
            result = self._ps(
                "Get-EventLog -LogName System -EntryType Error -Newest 20 "
                "| Select-Object TimeGenerated,Source,EventID,Message "
                "| ConvertTo-Json -Compress"
            )
            if result:
                raw = json.loads(result)
                if isinstance(raw, dict):
                    raw = [raw]
                for e in raw:
                    errors.append({
                        "time": str(e.get("TimeGenerated", "")),
                        "source": str(e.get("Source", "")),
                        "event_id": e.get("EventID", 0),
                        "message": str(e.get("Message", ""))[:300],
                    })
        except Exception:
            pass
        try:
            result = self._ps(
                "Get-EventLog -LogName Application -EntryType Error -Newest 10 "
                "| Select-Object TimeGenerated,Source,EventID,Message "
                "| ConvertTo-Json -Compress"
            )
            if result:
                raw = json.loads(result)
                if isinstance(raw, dict):
                    raw = [raw]
                for e in raw:
                    errors.append({
                        "time": str(e.get("TimeGenerated", "")),
                        "source": str(e.get("Source", "")),
                        "event_id": e.get("EventID", 0),
                        "message": str(e.get("Message", ""))[:300],
                        "log": "Application",
                    })
        except Exception:
            pass
        return errors

    def _recent_event_warnings(self) -> list:
        warnings = []
        try:
            result = self._ps(
                "Get-EventLog -LogName System -EntryType Warning -Newest 15 "
                "| Select-Object TimeGenerated,Source,EventID,Message "
                "| ConvertTo-Json -Compress"
            )
            if result:
                raw = json.loads(result)
                if isinstance(raw, dict):
                    raw = [raw]
                for e in raw:
                    warnings.append({
                        "time": str(e.get("TimeGenerated", "")),
                        "source": str(e.get("Source", "")),
                        "event_id": e.get("EventID", 0),
                        "message": str(e.get("Message", ""))[:300],
                    })
        except Exception:
            pass
        return warnings

    # ── Helpers ───────────────────────────────────────────────────────────────

    def _ps(self, command: str, timeout: int = 15) -> str:
        try:
            result = subprocess.run(
                ["powershell", "-NoProfile", "-NonInteractive", "-Command", command],
                capture_output=True, text=True, timeout=timeout
            )
            if result.returncode == 0 and result.stdout.strip():
                return result.stdout.strip()
        except Exception:
            pass
        return ""

    def _parse_wmi_date(self, date_str) -> datetime | None:
        if not date_str:
            return None
        s = str(date_str)
        try:
            if "Date(" in s:
                ms = int(s.split("(")[1].split(")")[0].split("+")[0].split("-")[0])
                return datetime.fromtimestamp(ms / 1000)
            if len(s) >= 14 and s[:8].isdigit():
                return datetime.strptime(s[:14], "%Y%m%d%H%M%S")
            return datetime.fromisoformat(s)
        except Exception:
            return None
