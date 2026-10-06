namespace DevStation.Application.Shared.Security;

public enum SqlTier { Read = 0, Write = 1, Dangerous = 2 }

public sealed record SqlVerdict(SqlTier Tier, string Reason, bool MultiStatement)
{
    public bool RequiresConfirm => Tier == SqlTier.Dangerous;
}

/// <summary>
/// Regex-free SQL statement classifier. Comments and string/quoted-identifier/
/// dollar-quoted literals are stripped before scanning, so keywords hidden in
/// '-- DROP', /* */ or '...' text cannot poison or smuggle a verdict.
///
/// Verb positions scanned: statement start, after ';', first word of a '('
/// group (only when it is a known verb — catches CTE bodies `WITH x AS (DELETE…)`
/// and subqueries), word immediately after ')' (catches the main verb following
/// a CTE list and `EXPLAIN (ANALYZE) DELETE`), plus an anywhere-scan of verbs
/// that are reserved words (cannot appear as bare identifiers).
/// Anything unrecognized at statement start fails toward Dangerous.
/// </summary>
public static class SqlGuard
{
    private static readonly HashSet<string> ReadVerbs =
        ["select", "with", "values", "table", "show", "explain", "describe"];

    private static readonly HashSet<string> WriteVerbs =
        ["insert", "update", "delete", "merge", "call", "execute", "exec"];

    private static readonly HashSet<string> DangerousVerbs =
    [
        "drop", "truncate", "alter", "create", "grant", "revoke", "deny",
        "backup", "restore", "attach", "detach", "copy", "do", "vacuum",
        "reindex", "cluster", "checkpoint", "discard", "listen", "unlisten",
        "notify", "load", "security", "import", "comment", "lock", "set",
        "reset", "begin", "commit", "rollback", "start", "savepoint",
        "release", "prepare", "declare", "fetch", "close", "deallocate",
    ];

    // Reserved words in every supported dialect — cannot be bare identifiers,
    // so they may be scanned anywhere without false positives. `into` is here
    // because SELECT…INTO creates a table (pg + mssql) — it must not read as Read.
    private static readonly HashSet<string> AnywhereWrite =
        ["insert", "update", "delete", "merge", "call", "execute", "into"];
    private static readonly HashSet<string> AnywhereDangerous =
        ["do", "drop", "truncate", "alter", "create", "grant", "revoke"];

    public static SqlVerdict Classify(string? sql)
    {
        var toks = Tokenize(sql ?? "");
        var statements = SplitStatements(toks);
        if (statements.Count == 0)
            return new SqlVerdict(SqlTier.Dangerous, "empty statement", false);

        var tier = SqlTier.Read;
        string? reason = null;
        foreach (var (s, e) in statements)
        {
            var (t, r) = ClassifyStatement(toks, s, e);
            if (t >= tier) { tier = t; if (t == SqlTier.Dangerous || reason is null) reason = r; }
        }

        var multi = statements.Count > 1;
        if (multi && tier < SqlTier.Write)
        {
            tier = SqlTier.Write;
            reason = "multi-statement batch";
        }
        else if (multi) reason = (reason ?? "") + " (multi-statement)";

        return new SqlVerdict(tier, reason ?? "ok", multi);
    }

    private static (SqlTier, string) ClassifyStatement(List<Tok> toks, int s, int e)
    {
        var tier = SqlTier.Read;
        string? reason = null;
        var sawVerb = false;

        void Bump(SqlTier t, string r)
        {
            if (t > tier) { tier = t; reason = r; }
            sawVerb = true;
        }

        // leading verb — every nonempty statement must start with a word
        var lead = -1;
        for (var i = s; i < e; i++)
            if (toks[i].Word is { } w) { lead = i; Bump(TierOf(w), VerbReason(w)); break; }
            else if (toks[i].Punc == '(') { /* parenthesized statement — group scan below picks the verb */ }
        if (lead < 0) return (SqlTier.Dangerous, "unparseable statement");

        var depth = 0;
        var groupHasWord = new Stack<bool>(); // per-open-paren: saw a word yet
        var afterParen = false;
        for (var i = s; i < e; i++)
        {
            var t = toks[i];
            if (t.Word is { } w)
            {
                if (AnywhereWrite.Contains(w)) Bump(SqlTier.Write, $"verb '{w}'");
                else if (AnywhereDangerous.Contains(w)) Bump(SqlTier.Dangerous, $"dangerous verb '{w}'");
                else if (groupHasWord.Count > 0 && !groupHasWord.Peek() && KnownVerb(w))
                    // first word of a '(' group that is a real verb — CTE body / subquery
                    Bump(TierOf(w), $"nested verb '{w}'");
                else if (afterParen && KnownVerb(w))
                    // word after ')' — CTE-list main verb, EXPLAIN (opts) verb
                    Bump(TierOf(w), $"verb '{w}' after ')'");
                if (groupHasWord.Count > 0 && !groupHasWord.Peek())
                {
                    groupHasWord.Pop();
                    groupHasWord.Push(true);
                }
                afterParen = false;
            }
            else
            {
                switch (t.Punc)
                {
                    case '(': depth++; groupHasWord.Push(false); break;
                    case ')': depth--; if (groupHasWord.Count > 0) groupHasWord.Pop(); afterParen = true; break;
                    default: afterParen = false; break;
                }
            }
        }
        if (!sawVerb) return (SqlTier.Dangerous, "unparseable statement");
        return (tier, reason ?? "read");
    }

    private static bool KnownVerb(string w)
        => ReadVerbs.Contains(w) || WriteVerbs.Contains(w) || DangerousVerbs.Contains(w);

    private static SqlTier TierOf(string w)
        => ReadVerbs.Contains(w) ? SqlTier.Read
         : WriteVerbs.Contains(w) ? SqlTier.Write
         : SqlTier.Dangerous; // listed dangerous OR unrecognized — fail toward safety

    private static string VerbReason(string w)
        => ReadVerbs.Contains(w) ? "read"
         : WriteVerbs.Contains(w) ? $"write verb '{w}'"
         : DangerousVerbs.Contains(w) ? $"dangerous verb '{w}'"
         : $"unrecognized verb '{w}'";

    private readonly struct Tok
    {
        public string? Word { get; init; }   // lowercased
        public char Punc { get; init; }      // ';' '(' ')' ',' else '\0'
        public bool IsWord => Word is not null;
    }

    /// <summary>Non-empty token ranges separated by ';'.</summary>
    private static List<(int s, int e)> SplitStatements(List<Tok> toks)
    {
        var ranges = new List<(int, int)>();
        var start = 0;
        for (var i = 0; i <= toks.Count; i++)
        {
            if (i < toks.Count && toks[i].Punc != ';') continue;
            if (HasWord(toks, start, i)) ranges.Add((start, i));
            start = i + 1;
        }
        return ranges;
    }

    private static bool HasWord(List<Tok> toks, int s, int e)
    {
        for (var i = s; i < e; i++) if (toks[i].IsWord) return true;
        return false;
    }

    /// <summary>
    /// Emits word tokens (lowercased) and structural punctuation.
    /// Strips: -- line comments, nested /* */ blocks, '..' literals (with
    /// '' and backslash escapes), ".." quoted identifiers, $tag$ dollar-quoted
    /// bodies. Everything else (operators, digits-only handled as words) is skipped.
    /// </summary>
    private static List<Tok> Tokenize(string sql)
    {
        var toks = new List<Tok>();
        var i = 0;
        var n = sql.Length;
        while (i < n)
        {
            var c = sql[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (c == '-' && i + 1 < n && sql[i + 1] == '-')
            {
                while (i < n && sql[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < n && sql[i + 1] == '*')
            {
                var depth = 1;
                i += 2;
                while (i < n && depth > 0)
                {
                    if (i + 1 < n && sql[i] == '/' && sql[i + 1] == '*') { depth++; i += 2; }
                    else if (i + 1 < n && sql[i] == '*' && sql[i + 1] == '/') { depth--; i += 2; }
                    else i++;
                }
                continue;
            }
            if (c == '\'' || c == '"' || (c is 'e' or 'E' or 'b' or 'B' && i + 1 < n && sql[i + 1] == '\''))
            {
                // string literal / quoted identifier / E''/B'' — strip entirely
                if (c != '\'' && c != '"') i++; // skip E/B prefix
                var q = sql[i++];
                while (i < n)
                {
                    if (sql[i] == '\\') { i += 2; continue; }
                    if (sql[i] == q)
                    {
                        if (i + 1 < n && sql[i + 1] == q) { i += 2; continue; } // '' or ""
                        i++;
                        break;
                    }
                    i++;
                }
                continue;
            }
            if (c == '$')
            {
                // dollar-quoted body: $tag$ ... $tag$
                var tagEnd = i + 1;
                while (tagEnd < n && (char.IsLetterOrDigit(sql[tagEnd]) || sql[tagEnd] == '_')) tagEnd++;
                if (tagEnd < n && sql[tagEnd] == '$')
                {
                    var tag = sql[i..(tagEnd + 1)];
                    var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
                    i = close < 0 ? n : close + tag.Length;
                    continue;
                }
                i++;
                continue;
            }
            if (c is ';' or '(' or ')' or ',')
            {
                toks.Add(new Tok { Punc = c });
                i++;
                continue;
            }
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                var j = i + 1;
                while (j < n && (char.IsLetterOrDigit(sql[j]) || sql[j] == '_')) j++;
                toks.Add(new Tok { Word = sql[i..j].ToLowerInvariant() });
                i = j;
                continue;
            }
            i++; // operators, punctuation we don't care about
        }
        return toks;
    }
}
