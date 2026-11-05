using BaseMethod.Models;

namespace WeldAvalonia.Models;

/// <summary>
/// Сущность, существующая только в методике «Сварка».
/// </summary>
public sealed class WeldMaterial : EntityBase
{
    private string _name = string.Empty;
    private decimal _consumptionKilograms;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public decimal ConsumptionKilograms
    {
        get => _consumptionKilograms;
        set => SetProperty(ref _consumptionKilograms, value);
    }
}
