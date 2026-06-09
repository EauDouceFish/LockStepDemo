// Ported from Ikemen GO (MIT License), Copyright (c) 2016 Suehiro and contributors.
// Source: src/image.go:18-22 (TransType enum) + src/compiler.go:6380-6404 (trans text → type + default alpha).
// Adapted to fixed-point lockstep. See Docs/移植方案_Ikemen.md.
namespace Lockstep.Mugen.Char
{
    /// <summary>
    /// 精灵混合模式（移植 Ikemen TransType，image.go:18）。表现态：决定绘制时的 alpha 混合。
    /// addalpha/add1 在 Ikemen 都归 Add，仅默认 alpha 不同（由解析阶段决定默认 src/dst）。
    /// </summary>
    public enum MTransType
    {
        None = 0,
        Add = 1,
        Sub = 2,
        Default = 3,
    }
}
