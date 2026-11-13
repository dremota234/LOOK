using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DslCompiler
{
    public static class StatementTranslator
    {
        private static readonly Dictionary<string, string> TypeMap = new()
        {
            { "uint", "u32" },
            { "int", "i32" },
            { "float", "f32" },
            { "float3", "Vec3" },
            { "void", string.Empty }
        };

        public static string MapType(string dslType) =>
            TypeMap.TryGetValue(dslType, out var mapped) ? mapped : dslType;

        public static bool IsKnownType(string typeName, CompilationContext ctx) =>
            TypeMap.ContainsKey(typeName) || ctx.StructNames.Contains(typeName);

        public static string TranslateLine(string line, CompilationContext ctx)
        {
            string comment = string.Empty;
            int commentIndex = line.IndexOf("//");
            if (commentIndex != -1)
            {
                comment = " " + line.Substring(commentIndex);
                line = line.Substring(0, commentIndex).Trim();
            }

            if (string.IsNullOrEmpty(line)) return comment.Trim();

            // Суффиксы числовых литералов
            line = Regex.Replace(line, @"\b(0x[0-9A-Fa-f]+|\d+)u\b", "${1}_u32");
            line = Regex.Replace(line, @"\b(\d+\.\d+)f\b", "${1}_f32");

            // Конструкторы векторов и приведение типов
            line = Regex.Replace(line, @"\bfloat3\s*\(", "Vec3::new(");
            line = Regex.Replace(line, @"\(uint\)\s*\(([^()]+(?:\([^()]*\)[^()]*)*)\)", "($1) as u32");

            // Встроенные функции математики
            line = Regex.Replace(line, @"min\(max\((.+?),\s*(.+?)\),\s*(.+?)\)", "($1).clamp($2, $3)");
            line = Regex.Replace(line, @"\bmin\((.+?),\s*(.+?)\)", "($1).min($2)");
            line = Regex.Replace(line, @"\bmax\((.+?),\s*(.+?)\)", "($1).max($2)");

            // Доступ к полям cbuffer
            string cbVar = ctx.CBufferVariableName;
            foreach (var field in ctx.CBufferFields)
            {
                line = Regex.Replace(line, $@"\b{field}\b", $"{cbVar}.{field}");
            }

            // Проброс ссылки на cbuffer в вызываемые функции
            foreach (var kvp in ctx.FunctionUsesCBuffer)
            {
                if (kvp.Value)
                {
                    line = Regex.Replace(line, $@"\b{kvp.Key}\(([^)]+)\)", $"{kvp.Key}($1, {cbVar})");
                }
            }

            // Объявление переменной без инициализации: MortonPair pair;
            var uninitMatch = Regex.Match(line, @"^(\w+)\s+(\w+)\s*;$");
            if (uninitMatch.Success)
            {
                string type = MapType(uninitMatch.Groups[1].Value);
                return $"let mut {uninitMatch.Groups[2].Value} = {type}::default();{comment}";
            }

            // Объявление переменной с инициализацией: uint x = ...;
            var initMatch = Regex.Match(line, @"^(\w+)\s+(\w+)\s*=\s*(.+);$");
            if (initMatch.Success)
            {
                string rawType = initMatch.Groups[1].Value;
                if (IsKnownType(rawType, ctx))
                {
                    string type = MapType(rawType);
                    return $"let mut {initMatch.Groups[2].Value}: {type} = {initMatch.Groups[3].Value};{comment}";
                }
            }

            return line + comment;
        }
    }
}
