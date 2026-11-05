using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BaseMethod.Data;

/// <summary>
/// Базовый контекст JSON-базы. Каждый зарегистрированный JsonSet хранится
/// в отдельном файле внутри рабочей папки проекта.
/// </summary>
public abstract class JsonDataContext
{
    private const int CurrentSchemaVersion = 1;
    private const string ManifestFileName = "manifest.json";
    private readonly List<IJsonSet> _sets = [];
    private readonly HashSet<string> _setNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _fileNames = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressChangeTracking;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    protected JsonDataContext(string databaseDirectory)
    {
        if (string.IsNullOrWhiteSpace(databaseDirectory))
            throw new ArgumentException("Папка JSON-базы не задана.", nameof(databaseDirectory));

        DatabaseDirectory = Path.GetFullPath(databaseDirectory);
    }

    public string DatabaseDirectory { get; }

    public bool HasChanges { get; private set; }

    protected abstract string DatabaseKind { get; }

    protected JsonSet<TEntity> RegisterSet<TEntity>(string name, string fileName)
        where TEntity : Models.EntityBase
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя набора не задано.", nameof(name));
        if (!_setNames.Add(name))
            throw new InvalidOperationException($"Набор '{name}' уже зарегистрирован.");

        ValidateFileName(fileName);
        if (!_fileNames.Add(fileName))
            throw new InvalidOperationException($"Файл '{fileName}' уже назначен другому набору.");

        var set = new JsonSet<TEntity>(name, fileName, MarkChanged);
        _sets.Add(set);
        return set;
    }

    public void CreateNew()
    {
        WithoutChangeTracking(() =>
        {
            foreach (var set in _sets)
                set.Clear();
        });

        OnAfterLoad();
        HasChanges = false;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(DatabaseDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Манифест JSON-базы не найден.", manifestPath);

        using var manifest = await ReadDocumentAsync(manifestPath, cancellationToken);
        var root = manifest.RootElement;

        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        if (schemaVersion > CurrentSchemaVersion)
            throw new InvalidDataException(
                $"Версия JSON-базы {schemaVersion} новее поддерживаемой версии {CurrentSchemaVersion}.");

        var kind = root.GetProperty("databaseKind").GetString();
        if (!string.Equals(kind, DatabaseKind, StringComparison.Ordinal))
            throw new InvalidDataException(
                $"Ожидалась база типа '{DatabaseKind}', но папка содержит базу типа '{kind}'.");

        var manifestSets = root.GetProperty("sets");

        // Все файлы сначала читаются во временные коллекции. Открытые данные
        // не меняются, если один из файлов отсутствует или повреждён.
        var loadedSets = new List<(IJsonSet Set, object Entities)>(_sets.Count);
        foreach (var set in _sets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!manifestSets.TryGetProperty(set.Name, out var fileNameElement))
            {
                // Набор мог появиться в более новой версии приложения.
                loadedSets.Add((set, set.Deserialize(CreateEmptyArray(), SerializerOptions)));
                continue;
            }

            var manifestFileName = fileNameElement.GetString();
            if (!string.Equals(manifestFileName, set.FileName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"Для набора '{set.Name}' ожидался файл '{set.FileName}', " +
                    $"но в манифесте указан '{manifestFileName}'.");

            var setPath = Path.Combine(DatabaseDirectory, set.FileName);
            if (!File.Exists(setPath))
                throw new FileNotFoundException(
                    $"Файл набора '{set.Name}' не найден.", setPath);

            using var setDocument = await ReadDocumentAsync(setPath, cancellationToken);
            if (setDocument.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException(
                    $"Файл '{set.FileName}' должен содержать JSON-массив.");

            loadedSets.Add((set, set.Deserialize(setDocument.RootElement, SerializerOptions)));
        }

        WithoutChangeTracking(() =>
        {
            foreach (var (set, entities) in loadedSets)
                set.ReplaceFrom(entities);
        });

        OnAfterLoad();
        HasChanges = false;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeBeforeSave();
        ValidateBeforeSave();
        foreach (var set in _sets) set.Validate();

        Directory.CreateDirectory(DatabaseDirectory);

        foreach (var set in _sets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var setPath = Path.Combine(DatabaseDirectory, set.FileName);
            await WriteAtomicallyAsync(
                setPath,
                writer => set.Write(writer, SerializerOptions),
                cancellationToken);
        }

        // Манифест сохраняется последним: он описывает уже записанные наборы.
        var manifestPath = Path.Combine(DatabaseDirectory, ManifestFileName);
        await WriteAtomicallyAsync(
            manifestPath,
            writer => WriteManifest(writer),
            cancellationToken);

        HasChanges = false;
    }

    protected virtual void OnAfterLoad()
    {
    }

    protected virtual void NormalizeBeforeSave()
    {
    }

    protected virtual void ValidateBeforeSave()
    {
    }

    private void WriteManifest(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", CurrentSchemaVersion);
        writer.WriteString("databaseKind", DatabaseKind);
        writer.WriteStartObject("sets");

        foreach (var set in _sets)
            writer.WriteString(set.Name, set.FileName);

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static async Task<JsonDocument> ReadDocumentAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static async Task WriteAtomicallyAsync(
        string path,
        Action<Utf8JsonWriter> writeContent,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 4096,
                             useAsync: true))
            {
                await using (var writer = new Utf8JsonWriter(
                                 stream,
                                 new JsonWriterOptions { Indented = true }))
                {
                    writeContent(writer);
                    await writer.FlushAsync(cancellationToken);
                }
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            throw;
        }
    }

    private void MarkChanged()
    {
        if (!_suppressChangeTracking) HasChanges = true;
    }

    private void WithoutChangeTracking(Action action)
    {
        _suppressChangeTracking = true;
        try { action(); }
        finally { _suppressChangeTracking = false; }
    }

    private static void ValidateFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Имя JSON-файла не задано.", nameof(fileName));
        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
            throw new ArgumentException("Набор должен использовать имя файла без пути.", nameof(fileName));
        if (!string.Equals(Path.GetExtension(fileName), ".json", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Файл набора должен иметь расширение .json.", nameof(fileName));
        if (string.Equals(fileName, ManifestFileName, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Имя '{ManifestFileName}' зарезервировано для манифеста.", nameof(fileName));
    }

    private static JsonElement CreateEmptyArray()
    {
        using var document = JsonDocument.Parse("[]");
        return document.RootElement.Clone();
    }
}
