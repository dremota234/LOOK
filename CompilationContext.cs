using System.Collections.Generic;

namespace DslCompiler
{
    public class CompilationContext
    {
        public string CBufferName { get; set; } = string.Empty;
        public HashSet<string> CBufferFields { get; } = new();
        public HashSet<string> StructNames { get; } = new();
        public Dictionary<string, bool> FunctionUsesCBuffer { get; } = new();

        public string CBufferVariableName => StringHelpers.ToSnakeCase(CBufferName);
    }

    public static class StringHelpers
    {
        public static string ToSnakeCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return System.Text.RegularExpressions.Regex.Replace(text, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
        }
    }
}
