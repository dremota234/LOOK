// Автоматически сгенерировано из Custom DSL
#![allow(non_snake_case)]
use glam::Vec3;


#[repr(C)]
#[derive(Debug, Default, Clone, Copy)]
pub struct MortonPair {
    pub mortonCode: u32, // Сам код Мортона
    pub objectID: u32,   // Исходный индекс объекта
}

#[repr(C)]
#[derive(Debug, Default, Clone, Copy)]
pub struct SceneBounds {
    pub SceneMin: Vec3,
    pub SceneInvSize: Vec3, // 1.0 / (SceneMax - SceneMin)
    pub ObjectCount_SceneBounds: u32,
}

pub fn ExpandBits(mut v: u32) -> u32 {
    v = (v * 0x00010001_u32) & 0xFF0000FF_u32;
    v = (v * 0x00000101_u32) & 0x0F00F00F_u32;
    v = (v * 0x00000011_u32) & 0xC30C30C3_u32;
    v = (v * 0x00000005_u32) & 0x49249249_u32;
    let mut v = return::default();
}

pub fn CalculateMorton3D(center: Vec3, scene_bounds: &SceneBounds) -> u32 {
    // Нормализуем центр относительно границ всей сцены в диапазон [0, 1]
    let mut normalizedPos: Vec3 = (center - scene_bounds.SceneMin) * scene_bounds.SceneInvSize;

    // Масштабируем [0.0, 1.0] в диапазон 10-битного целого числа [0, 1023]
    let mut x: u32 = ((normalizedPos.x * 1024.0_f32) as u32).clamp(0_u32, 1023_u32);
    let mut y: u32 = ((normalizedPos.y * 1024.0_f32) as u32).clamp(0_u32, 1023_u32);
    let mut z: u32 = ((normalizedPos.z * 1024.0_f32) as u32).clamp(0_u32, 1023_u32);

    // Раздвигаем биты и переплетаем их
    return (ExpandBits(x) << 0) | (ExpandBits(y) << 1) | (ExpandBits(z) << 2);
}

// [DSL Attribute: core = 1]
pub fn MortonKernel(scene_bounds: &SceneBounds) {
    let mut testCenter: Vec3 = Vec3::new(10.0_f32, 20.0_f32, 30.0_f32);

    let mut pair = MortonPair::default();
    pair.mortonCode = CalculateMorton3D(testCenter, scene_bounds);
    pair.objectID = 0_u32;
}

