using BaseMethod.Data;
using WeldAvalonia.Models;

namespace WeldAvalonia.Data;

/// <summary>
/// JSON-контекст методики «Сварка».
/// Наследует общие наборы и регистрирует собственные.
/// </summary>
public sealed class WeldDataContext : MethodDataContext
{
    public WeldDataContext(string databasePath) : base(databasePath)
    {
        WeldMaterials = RegisterSet<WeldMaterial>("weldMaterials", "weld-materials.json");
    }

    public JsonSet<WeldMaterial> WeldMaterials { get; }

    protected override string DatabaseKind => "weld";
}
