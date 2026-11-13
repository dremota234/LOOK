using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DslCompiler
{
    public class DslToRustCompiler
    {
        public string CompileToString(string sourceCode)
        {
            var ctx = CodeAnalyzer.Analyze(sourceCode);
            var sb = new StringBuilder();

            sb.AppendLine("// Автоматически сгенерировано из Custom DSL");
            sb.AppendLine("#![allow(non_snake_case)]");
            sb.AppendLine("use glam::Vec3;");
            sb.AppendLine();

            var lines = sourceCode.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool inStruct = false;
            bool inFunc = false;
            string? pendingAttribute = null;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                if (string.IsNullOrEmpty(line))
                {
                    sb.AppendLine();
                    continue;
                }

                // Атрибуты вроде [core(1)]
                var attrMatch = Regex.Match(line, @"^\[(\w+)\((\d+)\)\]$");
                if (attrMatch.Success)
                {
                    pendingAttribute = $"// [DSL Attribute: {attrMatch.Groups[1].Value} = {attrMatch.Groups[2].Value}]";
                    continue;
                }

                // Структуры и cbuffer
                var blockMatch = Regex.Match(line, @"^(struct|cbuffer)\s+(\w+)");
                if (blockMatch.Success)
                {
                    string name = blockMatch.Groups[2].Value;
                    sb.AppendLine("#[repr(C)]");
                    sb.AppendLine("#[derive(Debug, Default, Clone, Copy)]");
                    sb.AppendLine($"pub struct {name} {{");
                    inStruct = true;
                    continue;
                }

                if (inStruct)
                {
                    if (line == "{") continue;
                    if (line.StartsWith("};") || line == "}")
                    {
                        sb.AppendLine("}");
                        inStruct = false;
                        continue;
                    }

                    var fieldMatch = Regex.Match(line, @"^(\w+)\s+(\w+)\s*;(.*)$");
                    if (fieldMatch.Success)
                    {
                        string type = StatementTranslator.MapType(fieldMatch.Groups[1].Value);
                        string name = fieldMatch.Groups[2].Value;
                        string comment = fieldMatch.Groups[3].Value;
                        sb.AppendLine($"    pub {name}: {type},{comment}");
                    }
                    continue;
                }

                // Заголовки функций
                var funcMatch = Regex.Match(line, @"^(\w+)\s+(\w+)\s*\(([^)]*)\)");
                if (funcMatch.Success && !inFunc)
                {
                    if (pendingAttribute != null)
                    {
                        sb.AppendLine(pendingAttribute);
                        pendingAttribute = null;
                    }

                    string retType = StatementTranslator.MapType(funcMatch.Groups[1].Value);
                    string funcName = funcMatch.Groups[2].Value;
                    string rawArgs = funcMatch.Groups[3].Value.Trim();
                    string funcBody = CodeAnalyzer.ExtractBlockBody(lines, i);

                    var rustArgs = BuildFunctionArguments(rawArgs, funcBody, funcName, ctx);
                    string retStr = string.IsNullOrEmpty(retType) ? string.Empty : $" -> {retType}";

                    sb.AppendLine($"pub fn {funcName}({string.Join(", ", rustArgs)}){retStr} {{");
                    inFunc = true;
                    continue;
                }

                // Тело функции
                if (inFunc)
                {
                    if (line == "{") continue;
                    if (line == "}")
                    {
                        sb.AppendLine("}");
                        inFunc = false;
                        continue;
                    }

                    sb.AppendLine("    " + StatementTranslator.TranslateLine(line, ctx));
                }
            }

            return sb.ToString();
        }

        public void CompileToFile(string sourceCode, string outputRsFilePath)
        {
            string rustCode = CompileToString(sourceCode);

            string? directory = Path.GetDirectoryName(outputRsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputRsFilePath, rustCode, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static List<string> BuildFunctionArguments(string rawArgs, string funcBody, string funcName, CompilationContext ctx)
        {
            var rustArgs = new List<string>();

            if (!string.IsNullOrEmpty(rawArgs))
            {
                foreach (var arg in rawArgs.Split(','))
                {
                    var parts = arg.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        string argType = StatementTranslator.MapType(parts[0]);
                        string argName = parts[1];
                        bool isMutated = Regex.IsMatch(funcBody, $@"\b{argName}\s*=(?!=)");
                        string mutPrefix = isMutated ? "mut " : string.Empty;
                        rustArgs.Add($"{mutPrefix}{argName}: {argType}");
                    }
                }
            }

            if (ctx.FunctionUsesCBuffer.TryGetValue(funcName, out bool needsCBuffer) && needsCBuffer)
            {
                rustArgs.Add($"{ctx.CBufferVariableName}: &{ctx.CBufferName}");
            }

            return rustArgs;
        }
    }
}
