namespace BaseMethod.Models;

/// <summary>
/// Общая сущность расчёта, доступная всем приложениям-методикам.
/// </summary>
public sealed class Header : EntityBase
{
    private string _name = "Новый расчёт";
    private string? _notes;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }
}
