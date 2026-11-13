using System;
using DslCompiler;

class Program
{
    static void Main()
    {
        string dslCode = @"
struct MortonPair 
{
    uint mortonCode; // Сам код Мортона
    uint objectID;   // Исходный индекс объекта
}; 

cbuffer SceneBounds : register(b0)
{
    float3 SceneMin;
    float3 SceneInvSize; // 1.0 / (SceneMax - SceneMin)
    uint   ObjectCount_SceneBounds;
};

// Вспомогательная функция: вставляет 2 нуля между каждым битом 10-битного числа
uint ExpandBits(uint v)
{
    v = (v * 0x00010001u) & 0xFF0000FFu;
    v = (v * 0x00000101u) & 0x0F00F00Fu;
    v = (v * 0x00000011u) & 0xC30C30C3u;
    v = (v * 0x00000005u) & 0x49249249u;
    return v;
}

uint CalculateMorton3D(float3 center)
{
    // Нормализуем центр относительно границ всей сцены в диапазон [0, 1]
    float3 normalizedPos = (center - SceneMin) * SceneInvSize;
    
    // Масштабируем [0.0, 1.0] в диапазон 10-битного целого числа [0, 1023]
    uint x = min(max((uint)(normalizedPos.x * 1024.0f), 0u), 1023u);
    uint y = min(max((uint)(normalizedPos.y * 1024.0f), 0u), 1023u);
    uint z = min(max((uint)(normalizedPos.z * 1024.0f), 0u), 1023u);

    // Раздвигаем биты и переплетаем их
    return (ExpandBits(x) << 0) | (ExpandBits(y) << 1) | (ExpandBits(z) << 2);
}

[core(1)]
void MortonKernel()
{
    float3 testCenter = float3(10.0f, 20.0f, 30.0f);
    
    MortonPair pair;
    pair.mortonCode = CalculateMorton3D(testCenter);
    pair.objectID = 0u;
}
";

        string outputPath = @"C:\Users\User\Desktop\morton.rs";

        var compiler = new DslToRustCompiler();
        compiler.CompileToFile(dslCode, outputPath);

        Console.WriteLine($"Файл успешно сгенерирован: {outputPath}");
    }
}
