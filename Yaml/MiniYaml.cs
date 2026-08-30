using System.Collections.Generic;

namespace XUnity.AutoTranslator.Plugin.LlmTranslateOffline.Yaml
{
    // Minimal, dependency-free reader for the flat subset of YAML this plugin's
    // config file needs: top-level "key: value" scalars, comments, blank lines,
    // and literal block scalars ("key: |") for multi-line prompt text.
    internal static class MiniYaml
    {
        public static Dictionary<string, string> ParseFlat(string content)
        {
            var result = new Dictionary<string, string>();
            var lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            var i = 0;

            while (i < lines.Length)
            {
                var line = lines[i];
                var trimmed = line.TrimStart();

                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                {
                    i++;
                    continue;
                }

                var colonIndex = trimmed.IndexOf(':');
                if (colonIndex < 0)
                {
                    i++;
                    continue;
                }

                var key = trimmed.Substring(0, colonIndex).Trim();
                var rest = trimmed.Substring(colonIndex + 1).Trim();
                var keyIndent = line.Length - trimmed.Length;
                i++;

                if (rest.Length > 0 && rest[0] == '|')
                {
                    i = ReadBlockScalar(lines, i, keyIndent, out var blockValue);
                    result[key] = blockValue;
                }
                else
                {
                    result[key] = Unquote(StripInlineComment(rest));
                }
            }

            return result;
        }

        private static int ReadBlockScalar(string[] lines, int i, int keyIndent, out string value)
        {
            var blockLines = new List<string>();
            int? blockIndent = null;

            while (i < lines.Length)
            {
                var raw = lines[i];

                if (raw.Trim().Length == 0)
                {
                    blockLines.Add(string.Empty);
                    i++;
                    continue;
                }

                var indent = raw.Length - raw.TrimStart().Length;
                if (indent <= keyIndent)
                {
                    break;
                }

                if (blockIndent == null)
                {
                    blockIndent = indent;
                }

                blockLines.Add(raw.Length >= blockIndent.Value ? raw.Substring(blockIndent.Value) : raw.TrimStart());
                i++;
            }

            while (blockLines.Count > 0 && blockLines[blockLines.Count - 1].Length == 0)
            {
                blockLines.RemoveAt(blockLines.Count - 1);
            }

            value = string.Join("\n", blockLines);
            return i;
        }

        private static string StripInlineComment(string value)
        {
            if (value.Length == 0 || value[0] == '"')
            {
                return value;
            }

            var hashIndex = value.IndexOf('#');
            return hashIndex < 0 ? value.Trim() : value.Substring(0, hashIndex).Trim();
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
            {
                return value.Substring(1, value.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            }

            return value;
        }
    }
}
