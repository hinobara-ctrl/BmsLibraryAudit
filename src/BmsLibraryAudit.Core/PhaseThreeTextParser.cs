using System.Globalization;
using System.Text.RegularExpressions;

namespace BmsLibraryAudit.Core;

internal static class PhaseThreeTextParser
{
    private static readonly Regex Event = new("^#([0-9]{3})([0-9A-Za-z]{2}):(.*)$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex UnsupportedBase = new("^\\s*#BASE(?:\\s|$)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | RegexOptions.Multiline | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> Metadata = new("PLAYER GENRE TITLE SUBTITLE ARTIST SUBARTIST MAKER COMMENT BPM PLAYLEVEL RANK DEFEXRANK TOTAL VOLWAV DIFFICULTY LNTYPE LNMODE".Split(' '), StringComparer.Ordinal);
    private static readonly HashSet<string> Conditional = new("RANDOM SETRANDOM IF ELSEIF ELSE ENDIF ENDRANDOM SWITCH SETSWITCH CASE DEF SKIP ENDSW".Split(' '), StringComparer.Ordinal);
    internal static void Parse(ChartSyntaxBuilder b, ChartDecoding decoding)
    {
        using var reader = new StringReader(decoding.Text); var lineNumber = 0; var stack = new List<int>(); var conditionNotice = false;
        var unknownIdBase = UnsupportedBase.IsMatch(decoding.Text);
        string? raw;
        while ((raw = reader.ReadLine()) is not null)
        {
            b.Token.ThrowIfCancellationRequested(); lineNumber++;
            var line = raw.TrimStart(); var source = new ChartSourcePosition(decoding.Id, lineNumber, raw.Length - line.Length + 1, null, raw);
            if (raw.Length > b.Limits.MaximumLineCharacters) { b.Diagnostic("LINE_LIMIT", "UNKNOWN", "Long line is retained in decoded text but not interpreted.", source); continue; }
            if (line.Length == 0 || line.StartsWith('*') || line.StartsWith("//", StringComparison.Ordinal)) continue;
            var match = Event.Match(line);
            if (match.Success) { ReadEvent(b, match.Groups[2].Value.ToUpperInvariant(), match.Groups[3].Value, source, stack.ToArray(), unknownIdBase); continue; }
            if (!line.StartsWith('#')) { b.Unrecognized("TEXT", source); continue; }
            var separator = line.IndexOfAny([' ', '\t']);
            var word = (separator < 0 ? line[1..] : line[1..separator]).ToUpperInvariant();
            var argument = separator < 0 ? "" : line[(separator + 1)..].TrimStart(' ', '\t');
            if (Conditional.Contains(word))
            {
                if (!conditionNotice) { b.Diagnostic("CONDITIONAL_SEMANTICS_DEFERRED", "POTENTIAL", "All syntactically visible branches are retained. Reachability and scope semantics belong to Phase 4.", source); conditionNotice = true; }
                ReadConditional(b, word, argument, source, stack); continue;
            }
            if (word.StartsWith("WAV", StringComparison.Ordinal) || word.StartsWith("BMP", StringComparison.Ordinal) || word.StartsWith("STOP", StringComparison.Ordinal) || word.StartsWith("BPM", StringComparison.Ordinal) && word.Length > 3)
            {
                var prefix = word.StartsWith("STOP", StringComparison.Ordinal) ? "STOP" : word[..3]; var id = word[prefix.Length..];
                var resource = prefix is "WAV" or "BMP";
                var valid = ValidId(id);
                b.Declare(prefix, valid && !unknownIdBase ? id : null, resource ? prefix == "WAV" ? "AUDIO" : "VISUAL" : "TIMING", resource ? argument : null,
                    argument, source, stack.ToArray(), unknownIdBase ? "UNSUPPORTED" : valid ? "OBSERVED" : "UNKNOWN");
                if (!valid || argument.Length == 0) b.Diagnostic("MALFORMED_DECLARATION", "UNKNOWN", "Invalid logical ID or missing declaration value; raw evidence retained.", source);
                continue;
            }
            if (word is "STAGEFILE" or "BANNER" or "BACKBMP" or "PREVIEW")
            {
                b.Declare("HEADER", word, word == "PREVIEW" ? "AUDIO" : "VISUAL", argument, argument, source, stack.ToArray());
                b.Use("HEADER", word, "HEADER_RESOURCE", source, stack.ToArray()); continue;
            }
            if (word == "LNOBJ")
            {
                var id = argument.Trim().ToUpperInvariant();
                b.Use("WAV", ValidId(id) ? id : null, "LN_TERMINATOR_MARKER", source, stack.ToArray(), "AMBIGUOUS");
                b.Diagnostic("LN_SEMANTICS_DEFERRED", "AMBIGUOUS", "LN termination objects are retained without claiming that the indexed sound is played.", source); continue;
            }
            if (!Metadata.Contains(word)) b.Unrecognized("DIRECTIVE", source);
        }
        foreach (var id in stack)
            if (b.Contexts[id].Directive is not ("RANDOM" or "SETRANDOM"))
                b.Diagnostic("UNCLOSED_CONDITIONAL", "UNKNOWN", "Conditional structure is not closed; available branch evidence is retained.", b.Contexts[id].Source);
    }
    private static bool ValidId(string id) => id.Length == 2 && id.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'Z');
    private static void ReadEvent(ChartSyntaxBuilder b, string channel, string data, ChartSourcePosition source, int[] context, bool unknownIdBase)
    {
        string? ns = null; var kind = "UNKNOWN_CHANNEL";
        if (channel == "01") { ns = "WAV"; kind = "BGM_EVENT"; }
        else if (channel is "04" or "06" or "07" or "0A") { ns = "BMP"; kind = "BGA_" + channel; }
        else if (channel is "08" or "09") { ns = channel == "08" ? "BPM" : "STOP"; kind = "TIMING_REFERENCE"; }
        else if (channel[0] is >= '1' and <= '6' && channel[1] is >= '1' and <= '9')
        { ns = "WAV"; kind = channel[0] is '1' or '2' ? "PLAYABLE_EVENT" : channel[0] is '3' or '4' ? "INVISIBLE_EVENT" : "LONG_NOTE_EVENT"; }
        else if (channel[0] is 'D' or 'E' && channel[1] is >= '1' and <= '9') { ns = "WAV"; kind = "LANDMINE_WAV00_POTENTIAL"; }
        else if (channel == "02")
        {
            if (!double.TryParse(data, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var length) || length <= 0) b.Diagnostic("MALFORMED_METER", "UNKNOWN", "Invalid measure-length channel.", source);
            return;
        }
        else if (channel != "03") { b.Unrecognized("CHANNEL_" + channel, source); return; }
        var payload = data.Trim();
        if (payload.Length == 0 || payload.Length % 2 != 0) b.Diagnostic("MALFORMED_EVENT_DATA", "UNKNOWN", "Event payload is empty or has an incomplete object token.", source);
        for (var i = 0; i + 1 < payload.Length; i += 2)
        {
            var id = payload.Substring(i, 2).ToUpperInvariant();
            if (!ValidId(id)) { b.Unrecognized("INVALID_OBJECT_TOKEN", source with { RawText = payload.Substring(i, 2) }); continue; }
            if (channel == "03") { if (id.Any(c => c > 'F')) b.Diagnostic("INVALID_HEX_BPM", "UNKNOWN", "Direct BPM data must be hexadecimal, not WAV/BPM logical IDs.", source); continue; }
            if (id == "00") continue;
            b.Use(ns!, unknownIdBase ? null : kind == "LANDMINE_WAV00_POTENTIAL" ? "00" : id, kind,
                source with { Column = source.Column + 7 + (data.Length - data.TrimStart().Length) + i, RawText = payload.Substring(i, 2) }, context, unknownIdBase ? "UNSUPPORTED" : "POTENTIAL");
        }
    }
    private static void ReadConditional(ChartSyntaxBuilder b, string word, string argument, ChartSourcePosition source, List<int> stack)
    {
        b.Fact(); var parent = stack.Count == 0 ? (int?)null : stack[^1];
        if (word is "RANDOM" or "SETRANDOM" or "IF" or "ELSEIF" or "SWITCH" or "SETSWITCH" or "CASE")
            if (!int.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out var value) || value <= 0)
                b.Diagnostic("INVALID_CONDITIONAL_ARGUMENT", "UNKNOWN", "Expected positive integer; no branch was selected.", source);
        if (word is "ELSE" or "ELSEIF")
        {
            if (stack.Count == 0 || b.Contexts[stack[^1]].Directive is not ("IF" or "ELSEIF")) b.Diagnostic("ORPHAN_BRANCH", "UNKNOWN", "Branch has no matching IF context.", source);
            else { parent = b.Contexts[stack[^1]].ParentId; stack.RemoveAt(stack.Count - 1); }
        }
        if (word is "CASE" or "DEF")
        {
            if (stack.Count > 0 && b.Contexts[stack[^1]].Directive is "CASE" or "DEF") stack.RemoveAt(stack.Count - 1);
            parent = stack.Count == 0 ? null : stack[^1];
            if (parent is null || b.Contexts[parent.Value].Directive is not ("SWITCH" or "SETSWITCH")) b.Diagnostic("ORPHAN_CASE", "UNKNOWN", "CASE/DEF has no matching SWITCH.", source);
        }
        var id = b.Contexts.Count; b.Contexts.Add(new(id, word, argument, parent, source));
        if (word is "ENDIF" or "ENDRANDOM" or "ENDSW")
        {
            if (word == "ENDSW" && stack.Count > 0 && b.Contexts[stack[^1]].Directive is "CASE" or "DEF") stack.RemoveAt(stack.Count - 1);
            var expected = word == "ENDIF" ? new[] { "IF", "ELSEIF", "ELSE" } : word == "ENDRANDOM" ? ["RANDOM", "SETRANDOM"] : ["SWITCH", "SETSWITCH"];
            if (stack.Count == 0 || !expected.Contains(b.Contexts[stack[^1]].Directive, StringComparer.Ordinal)) b.Diagnostic("MISMATCHED_CONDITIONAL_END", "UNKNOWN", "No matching syntax context; no branches discarded.", source);
            else stack.RemoveAt(stack.Count - 1);
        }
        else if (word != "SKIP")
        {
            if (stack.Count >= b.Limits.MaximumConditionalDepth) throw new ChartSyntaxLimitException(); stack.Add(id);
        }
    }
}
