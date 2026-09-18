def generate_html_report(report: dict) -> str:
    """Generate a readable HTML audit report for the customer."""
    from datetime import datetime

    actions     = report.get("actions", [])
    hostname    = report.get("hostname", "Your PC")
    repaired_at = report.get("repaired_at", "")
    summary     = report.get("summary", "")
    total_freed = report.get("total_mb_freed", 0)
    version     = report.get("version", "1.0.0")

    try:
        dt = datetime.fromisoformat(repaired_at)
        date_str = dt.strftime("%B %d, %Y at %I:%M %p UTC")
    except Exception:
        date_str = repaired_at

    def fmt_mb(mb):
        if not mb:
            return ""
        if mb >= 1024:
            return f"{mb/1024:.1f} GB recovered"
        return f"{mb} MB recovered"

    def status_label(s):
        return {"done": "&#10003; Completed", "noted": "&#10003; Logged", "skipped": "&mdash; Skipped", "error": "&#10007; Error"}.get(s, s)

    def status_color(s):
        return {"done": "#2A7A4A", "noted": "#2A7A4A", "skipped": "#6B6560", "error": "#C0392B"}.get(s, "#1A1917")

    # ---------- Header badge logic ----------
    has_done = any(a.get("status") == "done" for a in actions)
    if not actions or (not has_done and all(a.get("status") == "noted" for a in actions)):
        badge_text = "Diagnostic Report"
        badge_color = "#3B82F6"
    else:
        badge_text = "Repair Complete"
        badge_color = "#C8892A"

    rows = ""
    for a in actions:
        status = a.get("status", "")
        mb     = a.get("mb_freed", 0)
        detail = a.get("detail", "")
        rows += f"""
        <tr>
          <td>{a.get('name', a.get('action', ''))}</td>
          <td style="color:{status_color(status)};font-weight:600">{status_label(status)}</td>
          <td style="color:#2A7A4A;font-family:'Courier New',monospace">{fmt_mb(mb)}</td>
          <td style="color:#6B6560;font-size:.85em">{detail[:120] if detail else '&mdash;'}</td>
        </tr>"""

    diag      = report.get("diagnostics", {})
    disks     = diag.get("disk", [])
    mem       = diag.get("memory", {})
    os_info   = diag.get("os", {})
    errors    = diag.get("event_errors", [])

    disk_rows = ""
    for d in disks:
        pct   = d.get("percent_used", 0)
        color = "#C0392B" if pct > 90 else "#C8892A" if pct > 70 else "#2A7A4A"
        disk_rows += f"""
        <tr>
          <td>{d.get('device','')}</td>
          <td>{d.get('total_gb',0)} GB total</td>
          <td>{d.get('used_gb',0)} GB used</td>
          <td>{d.get('free_gb',0)} GB free</td>
          <td style="color:{color};font-weight:600">{pct}% used</td>
        </tr>"""

    error_rows = ""
    for e in errors[:5]:
        error_rows += f"""
        <tr>
          <td style="font-size:.82em;color:#6B6560">{e.get('time','')[:19]}</td>
          <td style="font-weight:500">{e.get('source','')}</td>
          <td style="font-size:.82em">{e.get('message','')[:120]}</td>
        </tr>"""

    # =====================================================================
    # NEW SECTIONS — built as HTML fragments, inserted between event errors
    # and system information
    # =====================================================================
    new_sections = ""

    # ---- helpers for new sections ----
    def _enabled_cell(val):
        """Return a green 'Enabled' or red 'Disabled' table cell."""
        if val:
            return '<td style="color:#2A7A4A;font-weight:600">Enabled</td>'
        return '<td style="color:#C0392B;font-weight:600">Disabled</td>'

    def _bool_cell(val, true_text="Yes", false_text="No"):
        if val:
            return f'<td style="color:#2A7A4A;font-weight:600">{true_text}</td>'
        return f'<td style="color:#C0392B;font-weight:600">{false_text}</td>'

    def _section_open(title):
        return (
            '<div class="section">'
            '<div class="section-head"><div class="dot"></div>'
            f'<h3>{title}</h3></div>'
        )

    def _stats_bar(items):
        """items: list of (value, label) tuples."""
        html = '<div style="display:flex;gap:14px;padding:14px 20px;flex-wrap:wrap">'
        for val, lbl in items:
            html += (
                '<div style="background:#FDFAF4;border:1px solid #E2DBD0;'
                'border-radius:8px;padding:10px 16px;text-align:center;flex:1;min-width:100px">'
                f'<div style="font-size:1.4rem;font-weight:700;color:#C8892A;font-family:Georgia,serif;line-height:1">{val}</div>'
                f'<div style="font-size:.72rem;color:#6B6560;margin-top:4px;font-weight:500">{lbl}</div>'
                '</div>'
            )
        html += '</div>'
        return html

    # =====================================================================
    # 1. DRIVER AUDIT
    # =====================================================================
    drivers = diag.get("drivers", {})
    device_errors = diag.get("device_errors", [])
    bsod_history = diag.get("bsod_history", [])

    if drivers or device_errors or bsod_history:
        sec = _section_open("Driver Audit")

        # Stats bar
        if drivers:
            total_d   = drivers.get("total", 0)
            signed_d  = drivers.get("signed", 0)
            unsigned_d = drivers.get("unsigned", 0)
            outdated_d = drivers.get("outdated", 0)
            sec += _stats_bar([
                (total_d, "total drivers"),
                (signed_d, "signed"),
                (unsigned_d, "unsigned"),
                (outdated_d, "outdated"),
            ])

        # Unsigned drivers table
        unsigned_list = drivers.get("unsigned_list", []) if drivers else []
        if unsigned_list:
            sec += '<table><tr><th>Device</th><th>Manufacturer</th><th>Version</th></tr>'
            for u in unsigned_list:
                sec += f'<tr><td>{u.get("device","")}</td><td>{u.get("manufacturer","")}</td><td>{u.get("version","")}</td></tr>'
            sec += '</table>'

        # Outdated drivers table (cap 15)
        outdated_list = drivers.get("outdated_list", []) if drivers else []
        if outdated_list:
            sec += '<table><tr><th>Device</th><th>Version</th><th>Date</th><th>Provider</th><th>Age (days)</th></tr>'
            for o in outdated_list[:15]:
                sec += (
                    f'<tr><td>{o.get("device","")}</td><td>{o.get("version","")}</td>'
                    f'<td>{o.get("date","")}</td><td>{o.get("provider","")}</td>'
                    f'<td>{o.get("age_days","")}</td></tr>'
                )
            sec += '</table>'

        # Problem devices
        if device_errors:
            sec += '<table><tr><th>Device</th><th>Error Code</th><th>Description</th><th>Manufacturer</th></tr>'
            for de in device_errors:
                sec += (
                    f'<tr><td>{de.get("device","")}</td><td>{de.get("error_code","")}</td>'
                    f'<td>{de.get("error_description","")}</td><td>{de.get("manufacturer","")}</td></tr>'
                )
            sec += '</table>'

        # BSOD history
        if bsod_history:
            sec += '<div style="padding:14px 20px"><h4 style="font-size:.82rem;font-weight:700;color:#C0392B;text-transform:uppercase;letter-spacing:.5px;margin-bottom:8px">Blue Screen History</h4>'
            sec += '<table><tr><th>Date</th><th>Details</th></tr>'
            for b in bsod_history:
                detail = b.get("file", b.get("message", ""))
                sec += f'<tr><td>{b.get("date","")}</td><td style="font-size:.82em">{detail}</td></tr>'
            sec += '</table></div>'

        sec += '</div>'
        new_sections += sec

    # =====================================================================
    # 2. SECURITY STATUS
    # =====================================================================
    security = diag.get("security", {})
    activation = diag.get("activation", {})

    if security or activation:
        sec = _section_open("Security Status")

        if security:
            sec += '<table><tr><th>Setting</th><th>Status</th></tr>'
            sec += f'<tr><td>Real-Time Protection</td>{_enabled_cell(security.get("real_time_protection"))}</tr>'
            sec += f'<tr><td>Antivirus</td>{_enabled_cell(security.get("antivirus_enabled"))}</tr>'
            sec += f'<tr><td>Antispyware</td>{_enabled_cell(security.get("antispyware_enabled"))}</tr>'
            sec += f'<tr><td>Behavior Monitor</td>{_enabled_cell(security.get("behavior_monitor"))}</tr>'
            sec += '</table>'

            # Signature / scan dates
            sig_updated = security.get("signature_last_updated", "")
            last_quick  = security.get("last_quick_scan", "")
            last_full   = security.get("last_full_scan", "")
            if sig_updated or last_quick or last_full:
                sec += '<div style="padding:14px 20px;display:grid;grid-template-columns:1fr 1fr 1fr;gap:8px">'
                if sig_updated:
                    sec += f'<div class="sysinfo-row"><span class="key">Signatures Updated</span><span class="val">{sig_updated}</span></div>'
                if last_quick:
                    sec += f'<div class="sysinfo-row"><span class="key">Last Quick Scan</span><span class="val">{last_quick}</span></div>'
                if last_full:
                    sec += f'<div class="sysinfo-row"><span class="key">Last Full Scan</span><span class="val">{last_full}</span></div>'
                sec += '</div>'

            # Firewall profiles
            fw_profiles = security.get("firewall_profiles", [])
            if fw_profiles:
                sec += '<table><tr><th>Firewall Profile</th><th>Enabled</th></tr>'
                for fp in fw_profiles:
                    sec += f'<tr><td>{fp.get("name","")}</td>{_enabled_cell(fp.get("enabled"))}</tr>'
                sec += '</table>'

        # Windows activation
        if activation:
            sec += '<table><tr><th>Product</th><th>Status</th><th>Activated</th></tr>'
            sec += (
                f'<tr><td>{activation.get("product","")}</td>'
                f'<td>{activation.get("status","")}</td>'
                f'{_bool_cell(activation.get("is_activated"), "Activated", "Not Activated")}</tr>'
            )
            sec += '</table>'

        sec += '</div>'
        new_sections += sec

    # =====================================================================
    # 3. NETWORK HEALTH
    # =====================================================================
    network = diag.get("network", {})
    dns_cache = diag.get("dns_cache", {})

    if network or dns_cache:
        sec = _section_open("Network Health")

        # Active adapters
        adapters = network.get("adapters", [])
        if adapters:
            sec += '<table><tr><th>Adapter</th><th>Description</th><th>Speed</th></tr>'
            for ad in adapters:
                sec += f'<tr><td>{ad.get("name","")}</td><td>{ad.get("description","")}</td><td>{ad.get("speed","")}</td></tr>'
            sec += '</table>'

        # IP configuration
        connectivity = network.get("connectivity", {})
        interfaces = connectivity.get("interfaces", [])
        if interfaces:
            sec += '<table><tr><th>Interface</th><th>IPv4</th><th>Gateway</th><th>DNS Servers</th></tr>'
            for iface in interfaces:
                dns_list = iface.get("dns_servers", [])
                dns_str = ", ".join(dns_list) if isinstance(dns_list, list) else str(dns_list)
                sec += (
                    f'<tr><td>{iface.get("interface","")}</td><td>{iface.get("ipv4","")}</td>'
                    f'<td>{iface.get("gateway","")}</td><td style="font-size:.82em">{dns_str}</td></tr>'
                )
            sec += '</table>'

        # Internet reachable
        internet = connectivity.get("internet_reachable", network.get("internet_reachable"))
        if internet is not None:
            if internet:
                badge = '<span style="color:#2A7A4A;font-weight:600">&#10003; Internet Reachable</span>'
            else:
                badge = '<span style="color:#C0392B;font-weight:600">&#10007; Internet Unreachable</span>'
            sec += f'<div style="padding:10px 20px">{badge}</div>'

        # DNS cache entries
        if dns_cache:
            count = dns_cache.get("count", dns_cache.get("entries", 0))
            if count:
                sec += f'<div style="padding:10px 20px;font-size:.85em;color:#6B6560">DNS cache entries: <strong style="color:#1A1917">{count}</strong></div>'

        sec += '</div>'
        new_sections += sec

    # =====================================================================
    # 4. SERVICES AUDIT
    # =====================================================================
    services = diag.get("services", {})

    if services:
        stopped = services.get("stopped_critical", [])
        failed  = services.get("failed", [])

        if stopped or failed:
            sec = _section_open("Services Audit")

            if stopped:
                sec += '<table><tr><th>Service</th><th>Display Name</th><th>Status</th><th>Start Type</th></tr>'
                for sv in stopped:
                    sec += (
                        f'<tr><td style="font-family:\'Courier New\',monospace;font-size:.82em">{sv.get("name","")}</td>'
                        f'<td>{sv.get("display_name","")}</td>'
                        f'<td style="color:#C0392B;font-weight:600">{sv.get("status","Stopped")}</td>'
                        f'<td>{sv.get("start_type","")}</td></tr>'
                    )
                sec += '</table>'

            if failed:
                sec += '<table><tr><th>Time</th><th>Message</th></tr>'
                for f_entry in failed:
                    msg = f_entry.get("message", "")
                    sec += (
                        f'<tr><td style="font-size:.82em;color:#6B6560">{f_entry.get("time","")[:19]}</td>'
                        f'<td style="font-size:.82em">{msg[:120]}</td></tr>'
                    )
                sec += '</table>'

            sec += '</div>'
            new_sections += sec

    # =====================================================================
    # 5. INSTALLED PROGRAMS
    # =====================================================================
    installed = diag.get("installed_programs", {})

    if installed:
        programs = installed.get("programs", [])
        total_count = installed.get("total_count", len(programs))

        if total_count or programs:
            sec = _section_open("Installed Programs")
            sec += _stats_bar([(total_count, "programs installed")])

            if programs:
                sec += '<table><tr><th>Name</th><th>Version</th><th>Publisher</th><th>Installed</th><th>Size</th></tr>'
                for p in programs:
                    size_kb = p.get("size_kb", 0)
                    size_str = f"{size_kb/1024:.1f} MB" if size_kb else "&mdash;"
                    sec += (
                        f'<tr><td>{p.get("name","")}</td><td>{p.get("version","")}</td>'
                        f'<td>{p.get("publisher","")}</td><td>{p.get("install_date","")}</td>'
                        f'<td style="font-family:\'Courier New\',monospace;font-size:.82em">{size_str}</td></tr>'
                    )
                sec += '</table>'

            sec += '</div>'
            new_sections += sec

    # =====================================================================
    # 6. SYSTEM PERFORMANCE
    # =====================================================================
    uptime_info = diag.get("uptime", {})
    cpu_info    = diag.get("cpu", {})
    battery     = diag.get("battery", {})
    power_plan  = diag.get("power_plan", {})
    visual_fx   = diag.get("visual_effects", {})

    if uptime_info or cpu_info or battery or power_plan or visual_fx:
        sec = _section_open("System Performance")
        sec += '<div class="sysinfo">'

        # Uptime
        if uptime_info:
            boot = uptime_info.get("boot_time", "")
            up_days = uptime_info.get("uptime_days", 0)
            up_hours = uptime_info.get("uptime_hours", 0)
            needs_reboot = uptime_info.get("needs_reboot", up_days > 14 if up_days else False)
            up_color = "#C0392B" if needs_reboot else "#1A1917"
            reboot_note = ' <span style="color:#C0392B;font-weight:600">(reboot recommended)</span>' if needs_reboot else ""
            sec += f'<div class="sysinfo-row"><span class="key">Boot Time</span><span class="val">{boot}</span></div>'
            sec += f'<div class="sysinfo-row"><span class="key">Uptime</span><span class="val" style="color:{up_color}">{up_days}d {up_hours}h{reboot_note}</span></div>'

        # CPU
        if cpu_info:
            sec += f'<div class="sysinfo-row"><span class="key">Processor</span><span class="val">{cpu_info.get("processor_name","")}</span></div>'
            sec += f'<div class="sysinfo-row"><span class="key">Cores</span><span class="val">{cpu_info.get("physical_cores","")} physical / {cpu_info.get("logical_cores","")} logical</span></div>'
            cpu_pct = cpu_info.get("current_percent", "")
            freq    = cpu_info.get("frequency", "")
            if cpu_pct != "" or freq:
                sec += f'<div class="sysinfo-row"><span class="key">CPU Usage</span><span class="val">{cpu_pct}% @ {freq} MHz</span></div>'

        # Battery
        if battery:
            batt_pct = battery.get("percent", "")
            plugged  = battery.get("plugged_in", None)
            wear     = battery.get("wear_percent", None)
            plug_str = " (plugged in)" if plugged else " (on battery)" if plugged is not None else ""
            sec += f'<div class="sysinfo-row"><span class="key">Battery</span><span class="val">{batt_pct}%{plug_str}</span></div>'
            if wear is not None:
                if wear > 40:
                    wear_color = "#C0392B"
                elif wear > 20:
                    wear_color = "#C8892A"
                else:
                    wear_color = "#2A7A4A"
                sec += f'<div class="sysinfo-row"><span class="key">Battery Wear</span><span class="val" style="color:{wear_color}">{wear}%</span></div>'

        # Power plan
        if power_plan:
            sec += f'<div class="sysinfo-row"><span class="key">Power Plan</span><span class="val">{power_plan.get("active_plan","")}</span></div>'

        # Visual effects
        if visual_fx:
            sec += f'<div class="sysinfo-row"><span class="key">Visual Effects</span><span class="val">{visual_fx.get("setting","")}</span></div>'

        sec += '</div></div>'
        new_sections += sec

    # =====================================================================
    # 7. STORAGE DEEP DIVE
    # =====================================================================
    smart_health  = diag.get("smart_health", [])
    fragmentation = diag.get("fragmentation", [])
    recycle_bin   = diag.get("recycle_bin", {})
    win_caches    = diag.get("windows_caches", {})
    large_files   = diag.get("large_files", [])

    if smart_health or fragmentation or recycle_bin or win_caches or large_files:
        sec = _section_open("Storage Deep Dive")

        # SMART health
        if smart_health:
            sec += '<table><tr><th>Drive</th><th>Health</th></tr>'
            for sh in smart_health:
                predicted = sh.get("failure_predicted", False)
                if predicted:
                    health_cell = '<td style="color:#C0392B;font-weight:600">&#9888; Failure Predicted</td>'
                else:
                    health_cell = '<td style="color:#2A7A4A;font-weight:600">Healthy</td>'
                sec += f'<tr><td>{sh.get("drive","")}</td>{health_cell}</tr>'
            sec += '</table>'

        # Fragmentation
        if fragmentation:
            sec += '<table><tr><th>Drive</th><th>Fragmentation</th></tr>'
            for fg in fragmentation:
                sec += f'<tr><td>{fg.get("drive","")}</td><td>{fg.get("percent","")}%</td></tr>'
            sec += '</table>'

        # Recycle Bin
        if recycle_bin:
            rb_items = recycle_bin.get("item_count", 0)
            rb_mb    = recycle_bin.get("size_mb", 0)
            sec += f'<div style="padding:10px 20px;font-size:.85em;color:#6B6560">Recycle Bin: <strong style="color:#1A1917">{rb_items}</strong> items, <strong style="color:#1A1917">{rb_mb:.1f}</strong> MB</div>'

        # Windows caches
        if win_caches:
            sec += '<table><tr><th>Cache</th><th>Size (MB)</th><th>Files</th></tr>'
            for cache_name, cache_data in win_caches.items():
                if isinstance(cache_data, dict):
                    sec += (
                        f'<tr><td>{cache_name}</td>'
                        f'<td style="font-family:\'Courier New\',monospace">{cache_data.get("size_mb", 0):.1f}</td>'
                        f'<td>{cache_data.get("file_count", 0)}</td></tr>'
                    )
            sec += '</table>'

        # Large files (cap 10)
        if large_files:
            sec += '<table><tr><th>Path</th><th>Size (MB)</th><th>Type</th></tr>'
            for lf in large_files[:10]:
                path = lf.get("path", "")
                if len(path) > 60:
                    path = "..." + path[-57:]
                sec += (
                    f'<tr><td style="font-size:.82em;font-family:\'Courier New\',monospace">{path}</td>'
                    f'<td style="font-family:\'Courier New\',monospace">{lf.get("size_mb", 0):.1f}</td>'
                    f'<td>{lf.get("extension","")}</td></tr>'
                )
            sec += '</table>'

        sec += '</div>'
        new_sections += sec

    # =====================================================================
    # 8. WINDOWS UPDATE
    # =====================================================================
    win_update = diag.get("windows_update", {})

    if win_update:
        sec = _section_open("Windows Update")

        recent = win_update.get("recent", [])
        if recent:
            sec += '<table><tr><th>Update ID</th><th>Installed</th><th>Type</th></tr>'
            for wu in recent:
                sec += f'<tr><td>{wu.get("id","")}</td><td>{wu.get("installed","")}</td><td>{wu.get("type","")}</td></tr>'
            sec += '</table>'

        pending = win_update.get("pending", 0)
        if pending:
            sec += f'<div style="padding:10px 20px;color:#C8892A;font-weight:600">{pending} pending update{"s" if pending != 1 else ""}</div>'

        last_update = win_update.get("last_update", "")
        if last_update:
            # Try to detect stale updates (>30 days)
            stale = False
            try:
                last_dt = datetime.fromisoformat(last_update)
                stale = (datetime.now() - last_dt).days > 30
            except Exception:
                pass
            stale_color = "#C0392B" if stale else "#1A1917"
            stale_note = ' <span style="font-size:.78em;color:#C0392B">(stale &mdash; over 30 days)</span>' if stale else ""
            sec += f'<div style="padding:10px 20px;font-size:.85em;color:#6B6560">Last update: <strong style="color:{stale_color}">{last_update}</strong>{stale_note}</div>'

        sec += '</div>'
        new_sections += sec

    # =====================================================================
    # 9. USER ACCOUNTS
    # =====================================================================
    user_accounts = diag.get("user_accounts", [])

    if user_accounts:
        sec = _section_open("User Accounts")
        sec += '<table><tr><th>Name</th><th>Enabled</th><th>Last Logon</th><th>Description</th></tr>'
        for ua in user_accounts:
            sec += (
                f'<tr><td>{ua.get("name","")}</td>'
                f'{_enabled_cell(ua.get("enabled"))}'
                f'<td style="font-size:.82em">{ua.get("last_logon","")}</td>'
                f'<td style="font-size:.82em;color:#6B6560">{ua.get("description","")}</td></tr>'
            )
        sec += '</table></div>'
        new_sections += sec

    # =====================================================================
    # 10. EVENT WARNINGS
    # =====================================================================
    event_warnings = diag.get("event_warnings", [])

    if event_warnings:
        sec = _section_open("Event Warnings")
        sec += '<table><tr><th>Time</th><th>Source</th><th>Message</th></tr>'
        for ew in event_warnings[:10]:
            sec += f"""
        <tr>
          <td style="font-size:.82em;color:#6B6560">{ew.get('time','')[:19]}</td>
          <td style="font-weight:500;color:#C8892A">{ew.get('source','')}</td>
          <td style="font-size:.82em">{ew.get('message','')[:120]}</td>
        </tr>"""
        sec += '</table></div>'
        new_sections += sec

    # =====================================================================
    # ASSEMBLE FINAL HTML
    # =====================================================================

    html = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Pronto PC — Repair Report</title>
<style>
  *{{margin:0;padding:0;box-sizing:border-box}}
  body{{font-family:'Segoe UI',Arial,sans-serif;background:#FDFAF4;color:#1A1917;font-size:15px;line-height:1.6}}
  .page{{max-width:860px;margin:0 auto;padding:40px 32px 60px}}
  /* Header */
  .header{{background:#1A1917;color:#fff;border-radius:12px;padding:28px 32px;margin-bottom:28px;display:flex;justify-content:space-between;align-items:flex-start}}
  .header-left h1{{font-size:1.6rem;font-weight:700;letter-spacing:-.5px;margin-bottom:4px}}
  .header-left h1 span{{color:#C8892A}}
  .header-left p{{color:rgba(255,255,255,.6);font-size:.85rem}}
  .header-right{{text-align:right}}
  .header-right .badge{{color:#fff;font-size:.72rem;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;padding:.3rem .7rem;border-radius:6px;display:inline-block;margin-bottom:6px}}
  .header-right p{{color:rgba(255,255,255,.5);font-size:.78rem}}
  /* Summary box */
  .summary-box{{background:#fff;border:1px solid #E2DBD0;border-radius:10px;padding:20px 24px;margin-bottom:20px}}
  .summary-box h2{{font-size:1rem;font-weight:700;margin-bottom:8px;color:#1A1917}}
  .summary-box p{{color:#3A3835;line-height:1.7}}
  /* Stats */
  .stats{{display:grid;grid-template-columns:repeat(3,1fr);gap:14px;margin-bottom:20px}}
  .stat{{background:#fff;border:1px solid #E2DBD0;border-radius:10px;padding:16px 20px;text-align:center}}
  .stat .num{{font-size:1.8rem;font-weight:700;color:#C8892A;font-family:Georgia,serif;line-height:1}}
  .stat .lbl{{font-size:.78rem;color:#6B6560;margin-top:4px;font-weight:500}}
  /* Sections */
  .section{{background:#fff;border:1px solid #E2DBD0;border-radius:10px;margin-bottom:20px;overflow:hidden}}
  .section-head{{background:#F5F0E8;padding:14px 20px;border-bottom:1px solid #E2DBD0;display:flex;align-items:center;gap:10px}}
  .section-head h3{{font-size:.9rem;font-weight:700;letter-spacing:.3px;text-transform:uppercase;color:#1A1917}}
  .section-head .dot{{width:10px;height:10px;border-radius:50%;background:#C8892A;flex-shrink:0}}
  /* Tables */
  table{{width:100%;border-collapse:collapse}}
  th{{text-align:left;font-size:.78rem;font-weight:700;letter-spacing:.8px;text-transform:uppercase;color:#6B6560;padding:10px 20px;border-bottom:1px solid #E2DBD0;background:#FDFAF4}}
  td{{padding:11px 20px;border-bottom:1px solid #F0EBE4;font-size:.88rem;vertical-align:top}}
  tr:last-child td{{border-bottom:none}}
  tr:hover td{{background:#FDFAF4}}
  /* System info */
  .sysinfo{{padding:16px 20px;display:grid;grid-template-columns:1fr 1fr;gap:8px 24px}}
  .sysinfo-row{{display:flex;flex-direction:column}}
  .sysinfo-row .key{{font-size:.72rem;font-weight:700;letter-spacing:.8px;text-transform:uppercase;color:#9B9590}}
  .sysinfo-row .val{{font-size:.88rem;color:#1A1917;font-weight:500}}
  /* Footer */
  .footer{{margin-top:32px;padding-top:20px;border-top:1px solid #E2DBD0;display:flex;justify-content:space-between;align-items:center}}
  .footer p{{font-size:.78rem;color:#9B9590}}
  .footer strong{{color:#1A1917}}
  /* Print */
  @media print{{body{{background:#fff}}.page{{padding:20px}}.header{{border-radius:0}}}}
  @media(max-width:600px){{.stats{{grid-template-columns:1fr 1fr}}.header{{flex-direction:column;gap:12px}}.header-right{{text-align:left}}.sysinfo{{grid-template-columns:1fr}}}}
</style>
</head>
<body>
<div class="page">

  <div class="header">
    <div class="header-left">
      <h1>Pronto <span>PC</span> &mdash; Repair Report</h1>
      <p>Computer: {hostname} &nbsp;&middot;&nbsp; {date_str}</p>
    </div>
    <div class="header-right">
      <div class="badge" style="background:{badge_color}">{badge_text}</div>
      <p>Agent v{version}</p>
    </div>
  </div>

  <div class="summary-box">
    <h2>What We Did</h2>
    <p>{summary if summary else "Your PC was scanned and repaired. See the details below."}</p>
  </div>

  <div class="stats">
    <div class="stat">
      <div class="num">{fmt_mb(total_freed) if total_freed else "&mdash;"}</div>
      <div class="lbl">space recovered</div>
    </div>
    <div class="stat">
      <div class="num">{len([a for a in actions if a.get('status')=='done'])}</div>
      <div class="lbl">repairs completed</div>
    </div>
    <div class="stat">
      <div class="num">{mem.get('percent_used','&mdash;')}%</div>
      <div class="lbl">memory in use</div>
    </div>
  </div>

  <div class="section">
    <div class="section-head"><div class="dot"></div><h3>Repair Actions</h3></div>
    <table>
      <tr><th>Action</th><th>Result</th><th>Space</th><th>Notes</th></tr>
      {rows}
    </table>
  </div>

  {'<div class="section"><div class="section-head"><div class="dot"></div><h3>Drive Health</h3></div><table><tr><th>Drive</th><th>Total</th><th>Used</th><th>Free</th><th>Usage</th></tr>' + disk_rows + '</table></div>' if disk_rows else ''}

  {'<div class="section"><div class="section-head"><div class="dot"></div><h3>Windows Event Errors (last 10)</h3></div><table><tr><th>Time</th><th>Source</th><th>Message</th></tr>' + error_rows + '</table></div>' if error_rows else ''}

  {new_sections}

  <div class="section">
    <div class="section-head"><div class="dot"></div><h3>System Information</h3></div>
    <div class="sysinfo">
      <div class="sysinfo-row"><span class="key">Computer Name</span><span class="val">{hostname}</span></div>
      <div class="sysinfo-row"><span class="key">Windows Version</span><span class="val">{os_info.get('release','')} {os_info.get('version','')[:40]}</span></div>
      <div class="sysinfo-row"><span class="key">Processor</span><span class="val">{os_info.get('processor','')[:50] or '&mdash;'}</span></div>
      <div class="sysinfo-row"><span class="key">Architecture</span><span class="val">{os_info.get('machine','')}</span></div>
      <div class="sysinfo-row"><span class="key">Total Memory</span><span class="val">{mem.get('total_gb','&mdash;')} GB</span></div>
      <div class="sysinfo-row"><span class="key">Repaired At</span><span class="val">{date_str}</span></div>
    </div>
  </div>

  <div class="section" style="border-color:#C8892A40;background:linear-gradient(135deg,#FFFDF8,#FFF8ED)">
    <div style="padding:24px;text-align:center">
      <h3 style="font-size:1.1rem;font-weight:700;color:#1A1917;margin-bottom:8px">Keep your system in this condition</h3>
      <p style="color:#6B6560;font-size:.9rem;max-width:440px;margin:0 auto 16px">ProntoPC Guardian runs automated health checks at your chosen frequency — weekly or monthly. Full diagnostic report emailed to you. Your Custodian on standby if anything comes up.</p>
      <p style="font-size:1.4rem;font-weight:700;color:#C8892A;margin-bottom:4px">30 days free — then from $5/month</p>
      <p style="font-size:.78rem;color:#9B9590;margin-bottom:16px">Cancel anytime &middot; No contracts &middot; Scan only, never automatic repairs</p>
      <a href="https://prontopc.online/guardian-offer" style="display:inline-block;background:#C8892A;color:#fff;font-weight:700;font-size:.9rem;padding:12px 28px;border-radius:8px;text-decoration:none">Learn More</a>
    </div>
  </div>

  <div class="footer">
    <p><strong>Pronto PC</strong> &middot; prontopc.online &middot; support@prontopc.online</p>
    <p>5D Service Solutions Global LLC &middot; 30-day re-repair guarantee</p>
  </div>

</div>
</body>
</html>"""
    return html
