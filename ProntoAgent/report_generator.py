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
        return {"done": "✓ Completed", "noted": "✓ Logged", "skipped": "— Skipped", "error": "✗ Error"}.get(s, s)

    def status_color(s):
        return {"done": "#2A7A4A", "noted": "#2A7A4A", "skipped": "#6B6560", "error": "#C0392B"}.get(s, "#1A1917")

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
          <td style="color:#6B6560;font-size:.85em">{detail[:120] if detail else '—'}</td>
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
  .header-right .badge{{background:#C8892A;color:#fff;font-size:.72rem;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;padding:.3rem .7rem;border-radius:6px;display:inline-block;margin-bottom:6px}}
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
      <h1>Pronto <span>PC</span> — Repair Report</h1>
      <p>Computer: {hostname} &nbsp;·&nbsp; {date_str}</p>
    </div>
    <div class="header-right">
      <div class="badge">Repair Complete</div>
      <p>Agent v{version}</p>
    </div>
  </div>

  <div class="summary-box">
    <h2>What We Did</h2>
    <p>{summary if summary else "Your PC was scanned and repaired. See the details below."}</p>
  </div>

  <div class="stats">
    <div class="stat">
      <div class="num">{fmt_mb(total_freed) if total_freed else "—"}</div>
      <div class="lbl">space recovered</div>
    </div>
    <div class="stat">
      <div class="num">{len([a for a in actions if a.get('status')=='done'])}</div>
      <div class="lbl">repairs completed</div>
    </div>
    <div class="stat">
      <div class="num">{mem.get('percent_used','—')}%</div>
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

  <div class="section">
    <div class="section-head"><div class="dot"></div><h3>System Information</h3></div>
    <div class="sysinfo">
      <div class="sysinfo-row"><span class="key">Computer Name</span><span class="val">{hostname}</span></div>
      <div class="sysinfo-row"><span class="key">Windows Version</span><span class="val">{os_info.get('release','')} {os_info.get('version','')[:40]}</span></div>
      <div class="sysinfo-row"><span class="key">Processor</span><span class="val">{os_info.get('processor','')[:50] or '—'}</span></div>
      <div class="sysinfo-row"><span class="key">Architecture</span><span class="val">{os_info.get('machine','')}</span></div>
      <div class="sysinfo-row"><span class="key">Total Memory</span><span class="val">{mem.get('total_gb','—')} GB</span></div>
      <div class="sysinfo-row"><span class="key">Repaired At</span><span class="val">{date_str}</span></div>
    </div>
  </div>

  <div class="footer">
    <p><strong>Pronto PC</strong> · prontopc.online · support@prontopc.online</p>
    <p>5D Service Solutions Global LLC · 30-day re-repair guarantee</p>
  </div>

</div>
</body>
</html>"""
    return html
