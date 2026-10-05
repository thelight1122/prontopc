using System;
using ProntoPC;

int failures = 0;
void Check(bool ok, string label)
{
    Console.WriteLine((ok ? "PASS " : "FAIL ") + label);
    if (!ok) failures++;
}
const string response = "Try this & that? #issue + unicode: café\nSecond line";
string body = AiContentReport.Compose("1.2.21.0", response, "My concern");
Check(body.Contains(response) && body.Contains("My concern") && body.Contains("1.2.21.0"), "Reviewed content and version retained");
string? draft = AiContentReport.EmailDraftUri(body);
Check(draft != null && draft.StartsWith("mailto:support@prontopc.online?subject="), "Draft has fixed recipient");
string[] fields = draft!.Substring(draft.IndexOf('?') + 1).Split('&');
Check(fields.Length == 2 && fields[0].StartsWith("subject=") && fields[1].StartsWith("body="), "Content cannot inject additional email headers");
Check(Uri.UnescapeDataString(fields[1].Substring(5)) == body, "Special characters and newlines survive encoding");
string longBody = AiContentReport.Compose("1.2.21.0", new string('x', 5000), "");
Check(AiContentReport.EmailDraftUri(longBody) == null && longBody.Contains(new string('x', 5000)), "Long reports require copy/save without truncation");
Check(!body.Contains(Environment.UserName) && !body.Contains("Session ID:"), "No automatic account or session identifiers");
return failures == 0 ? 0 : 1;
