using System.Text;

namespace MessSharp.Report;

/// <summary>
/// Shared character escaping for XML-flavoured renderers (XML, Checkstyle, HTML).
/// </summary>
public static class Escape
{
    /// <summary>
    /// Escapes the five XML entities and strips characters that are not legal
    /// in XML 1.0 documents. Numeric character references are not a valid
    /// encoding for that set, so the usual practice is to drop them; legal
    /// whitespace (tab, LF, CR) and all other Unicode are preserved.
    /// </summary>
    public static string Xml(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (!IsLegalXml(ch)) continue;
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case '\'': sb.Append("&#039;"); break;
                default: sb.Append(ch); break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// True when the UTF-16 code unit may appear unencoded in an XML 1.0
    /// document: tab, LF, CR, U+0020–U+FFFD (including surrogate halves, so
    /// valid pairs pass through untouched). U+FFFE and U+FFFF are never legal.
    /// </summary>
    private static bool IsLegalXml(char ch) =>
        ch == '\t' || ch == '\n' || ch == '\r' ||
        (ch >= ' ' && ch != '\uFFFE' && ch != '\uFFFF');
}
