using System.Text.RegularExpressions;
namespace Blackveil.Core;
public static class ReleaseNotes
{
    public static string Since(string changelog, string? previous, string current)
    {
        Version.TryParse(previous, out var old); Version newest = Version.Parse(current);
        var sections = Regex.Matches(changelog, @"(?ms)^## \[(\d+\.\d+\.\d+)\][^\r\n]*\r?\n(.*?)(?=^## |\z)");
        return string.Join("\r\n\r\n", sections.Cast<Match>().Where(m =>
        { var version = Version.Parse(m.Groups[1].Value); return version <= newest && (old is null ? version == newest : version > old); })
            .Select(m => "Version " + m.Groups[1].Value + "\r\n" + m.Groups[2].Value.Trim()));
    }
}
