using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DslCompiler
{
    public static class CodeAnalyzer
    {
        public static CompilationContext Analyze(string code)
        {
            var ctx = new CompilationContext();

            foreach (Match m in Regex.Matches(code, @"struct\s+(\w+)"))
                ctx.StructNames.Add(m.Groups[1].Value);

            var cbMatch = Regex.Match(code, @"cbuffer\s+(\w+)[^{]*\{([^}]*)\}");
            if (cbMatch.Success)
            {
                ctx.CBufferName = cbMatch.Groups[1].Value;
                foreach (Match f in Regex.Matches(cbMatch.Groups[2].Value, @"\b\w+\s+(\w+)\s*;"))
                    ctx.CBufferFields.Add(f.Groups[1].Value);
            }

            var lines = code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var funcBodies = new Dictionary<string, string>();

            for (int i = 0; i < lines.Length; i++)
            {
                var m = Regex.Match(lines[i].Trim(), @"^(\w+)\s+(\w+)\s*\([^)]*\)");
                if (m.Success)
                {
                    string fName = m.Groups[2].Value;
                    funcBodies[fName] = ExtractBlockBody(lines, i);
                }
            }

            // Прямое обращение к полям cbuffer
            foreach (var kvp in funcBodies)
            {
                bool usesDirect = ctx.CBufferFields.Any(field => Regex.IsMatch(kvp.Value, $@"\b{field}\b"));
                ctx.FunctionUsesCBuffer[kvp.Key] = usesDirect;
            }

            // Транзитивное обращение через вызов других функций
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (var kvp in funcBodies)
                {
                    if (ctx.FunctionUsesCBuffer[kvp.Key]) continue;

                    foreach (var callee in funcBodies.Keys)
                    {
                        if (ctx.FunctionUsesCBuffer[callee] && Regex.IsMatch(kvp.Value, $@"\b{callee}\s*\("))
                        {
                            ctx.FunctionUsesCBuffer[kvp.Key] = true;
                            changed = true;
                        }
                    }
                }
            }

            return ctx;
        }

        public static string ExtractBlockBody(string[] lines, int startLineIndex)
        {
            var body = new StringBuilder();
            int braces = 0;
            bool started = false;

            for (int i = startLineIndex; i < lines.Length; i++)
            {
                foreach (char c in lines[i])
                {
                    if (c == '{') { braces++; started = true; }
                    if (c == '}') braces--;
                }
                if (started) body.AppendLine(lines[i]);
                if (started && braces == 0) break;
            }
            return body.ToString();
        }
    }
}
