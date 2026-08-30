using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ProntoPC.Governance;

// PIM observes possible force artifacts in either direction. It returns the original
// text unchanged: observation is visible, while the later Prism owns expression shaping.
public static class ForceArtifactScreen
{
    static readonly (ForceArtifactKind Kind, Regex Pattern, string Description)[] Rules =
    {
        (ForceArtifactKind.DirectiveLeakage, new Regex(@"\b(must|need to|required to|have to)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "directive wording"),
        (ForceArtifactKind.AuthorityFraming, new Regex(@"\b(as your authority|i decide|i determine|the canon requires)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "authority framing"),
        (ForceArtifactKind.CoerciveCertainty, new Regex(@"\b(i guarantee|definitely will|no choice|only option)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "coercive certainty"),
        (ForceArtifactKind.EmotionalLeverage, new Regex(@"\b(you'll regret|don't disappoint|everyone else|be a good)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "emotional leverage"),
        (ForceArtifactKind.OptimizationPressure, new Regex(@"\b(act now|immediately|urgent|hurry)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), "optimization pressure")
    };

    public static PimScreenResult Screen(string text, ScreenDirection direction)
    {
        text ??= string.Empty;
        var observations = new List<ForceArtifactObservation>();
        foreach (var rule in Rules)
        {
            Match match = rule.Pattern.Match(text);
            if (match.Success)
                observations.Add(new ForceArtifactObservation(rule.Kind, direction, match.Value, rule.Description));
        }
        return new PimScreenResult(text, direction, observations);
    }
}
