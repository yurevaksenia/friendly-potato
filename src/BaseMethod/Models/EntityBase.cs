using CommunityToolkit.Mvvm.ComponentModel;

namespace BaseMethod.Models;

/// <summary>
/// Базовый тип сущностей JSON-контекста.
/// Идентификатор создаётся один раз и не меняется в течение жизни объекта.
/// </summary>
public abstract class EntityBase : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
