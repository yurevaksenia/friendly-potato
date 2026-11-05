using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using BaseMethod.Models;

namespace BaseMethod.Data;

/// <summary>
/// Типизированный аналог DbSet для JSON-контекста.
/// Поддерживает наблюдаемую коллекцию для Avalonia и быстрый поиск по Guid.
/// </summary>
public sealed class JsonSet<TEntity> : IEnumerable<TEntity>, IJsonSet where TEntity : EntityBase
{
    private readonly ObservableCollection<TEntity> _items = [];
    private readonly Dictionary<Guid, TEntity> _index = [];
    private readonly Action _onChanged;

    internal JsonSet(string name, string fileName, Action onChanged)
    {
        Name = name;
        FileName = fileName;
        _onChanged = onChanged;
        Items = new ReadOnlyObservableCollection<TEntity>(_items);
    }

    public string Name { get; }

    public string FileName { get; }

    public ReadOnlyObservableCollection<TEntity> Items { get; }

    public int Count => _items.Count;

    public TEntity? Find(Guid id) => _index.GetValueOrDefault(id);

    public void Add(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ValidateIdentifier(entity.Id);

        if (!_index.TryAdd(entity.Id, entity))
            throw new InvalidOperationException(
                $"Набор '{Name}' уже содержит сущность с Id {entity.Id}.");

        entity.PropertyChanged += OnEntityPropertyChanged;
        _items.Add(entity);
        _onChanged();
    }

    public bool Remove(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (!_index.Remove(entity.Id)) return false;

        entity.PropertyChanged -= OnEntityPropertyChanged;
        _items.Remove(entity);
        _onChanged();
        return true;
    }

    public bool Remove(Guid id) => Find(id) is { } entity && Remove(entity);

    public IEnumerator<TEntity> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    object IJsonSet.Deserialize(JsonElement json, JsonSerializerOptions options) =>
        json.Deserialize<List<TEntity>>(options) ?? [];

    void IJsonSet.ReplaceFrom(object entities)
    {
        if (entities is not List<TEntity> typedEntities)
            throw new InvalidOperationException($"Неверный тип данных для набора '{Name}'.");

        var newIndex = new Dictionary<Guid, TEntity>();
        foreach (var entity in typedEntities)
        {
            ValidateIdentifier(entity.Id);
            if (!newIndex.TryAdd(entity.Id, entity))
                throw new InvalidDataException(
                    $"В наборе '{Name}' найден повторяющийся Id {entity.Id}.");
        }

        foreach (var entity in _items)
            entity.PropertyChanged -= OnEntityPropertyChanged;

        _items.Clear();
        _index.Clear();

        foreach (var entity in typedEntities)
        {
            _index.Add(entity.Id, entity);
            entity.PropertyChanged += OnEntityPropertyChanged;
            _items.Add(entity);
        }

        _onChanged();
    }

    void IJsonSet.Write(Utf8JsonWriter writer, JsonSerializerOptions options) => JsonSerializer.Serialize(writer, _items, options);

    void IJsonSet.Clear()
    {
        foreach (var entity in _items)
            entity.PropertyChanged -= OnEntityPropertyChanged;

        _items.Clear();
        _index.Clear();
        _onChanged();
    }

    void IJsonSet.Validate()
    {
        if (_items.Count != _index.Count)
            throw new InvalidDataException($"Нарушен индекс набора '{Name}'.");

        foreach (var entity in _items)
        {
            ValidateIdentifier(entity.Id);
            if (!_index.TryGetValue(entity.Id, out var indexed) || !ReferenceEquals(entity, indexed))
                throw new InvalidDataException($"Нарушен индекс сущности {entity.Id} в наборе '{Name}'.");
        }
    }

    private void OnEntityPropertyChanged(object? sender, PropertyChangedEventArgs e) => _onChanged();

    private static void ValidateIdentifier(Guid id)
    {
        if (id == Guid.Empty)
            throw new InvalidDataException("Идентификатор сущности не может быть пустым.");
    }
}
