using BaseMethod.Views;

namespace WeldAvalonia.Views;

/// <summary>
/// Главное окно методики «Сварка».
/// Общий интерфейс и общую логику оно получает от BaseWindow.
/// </summary>
public sealed class WeldWindow : BaseWindow
{
    public WeldWindow()
    {
        Title = "Сварка";
    }
}
