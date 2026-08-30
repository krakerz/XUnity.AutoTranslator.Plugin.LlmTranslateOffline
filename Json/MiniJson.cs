using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Json
{
    // Minimal, dependency-free JSON reader/writer. Only supports what the
    // OpenAI-compatible chat-completions request/response shapes need:
    // objects, arrays, strings, numbers, bools and null.
    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            var index = 0;
            return ParseValue(json, ref index);
        }

        public static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            switch (s[i])
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': i += 4; return true;
                case 'f': i += 5; return false;
                case 'n': i += 4; return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                var key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                i++; // :
                var value = ParseValue(s, ref i);
                result[key] = value;
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
                break;
            }
            return result;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (s[i] == ']') { i++; return result; }
            while (true)
            {
                var value = ParseValue(s, ref i);
                result.Add(value);
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
                break;
            }
            return result;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                if (s[i] == '\\')
                {
                    i++;
                    switch (s[i])
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            var hex = s.Substring(i + 1, 4);
                            sb.Append((char)int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                    }
                    i++;
                }
                else
                {
                    sb.Append(s[i]);
                    i++;
                }
            }
            i++; // closing quote
            return sb.ToString();
        }

        private static double ParseNumber(string s, ref int i)
        {
            var start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            {
                i++;
            }
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }
    }
}
