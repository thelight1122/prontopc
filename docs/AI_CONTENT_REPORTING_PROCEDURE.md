# ProntoPC AI content reporting procedure

Support destination: support@prontopc.online. Subject: ProntoPC AI content report.

The app's persistent footer offers Report inappropriate AI content in every phase.
Generated Technician replies also offer Report this response, which opens a dialog
with that response selected. The user reviews/redacts it, describes the concern,
and opens an email draft, copies the report, or saves a text file. None of these
actions sends a message automatically. Only reviewed text and app version are included.
No diagnostic logs, conversation history, files, credentials, or account identifiers
are collected automatically. Reporting does not depend on the model agreeing to a request.

## Developer review

1. Monitor the support mailbox for AI content concerns and record receipt, app
   version, concern category, and a minimal reference to the report in the support record.
   Restrict access to the support team; do not put personal report content in public Totems.
2. Review the reported output and relevant context. Ask for only the minimum
   additional information necessary; do not request a full private diagnostic dump by default.
3. Identify whether it reflects inappropriate, harmful, misleading, or incorrect
   content, an AEGIS pathway issue, or another product issue. Use a reproducible,
   redacted example when possible and prioritize potential harm.
4. Take appropriate action: clarify, correct model/product behavior, improve the
   governed response path, or disable affected behavior when necessary. Record
   the disposition and verification. Respond to the reporter when contact is available.
5. Add a redacted regression case for confirmed product shortfalls and verify
   the correction before releasing another package.

## Reviewer verification (required before resubmission)

On the new installed package: launch → Report inappropriate AI content; enter
synthetic output and a concern; verify email recipient/subject/body; cancel sending.
Repeat using Copy report and Save report with no mail app. Scan → findings → ask
the Technician a question → Report this response; confirm the exact displayed reply
is editable before sharing. Verify reporting the repair-plan output via the global action.
Confirm Report this repair-plan summary selects the displayed summary for review.
Confirm About AI & AEGIS opens https://prontopc.online/ai-aegis.

Mailbox delivery and developer response are separate operational checks. The owner
must confirm that support@prontopc.online receives reports and is monitored. No
test message was sent by Codex. WACK alone does not validate receipt or review.
