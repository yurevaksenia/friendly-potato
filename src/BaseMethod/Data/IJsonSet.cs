using System.Text.Json;

namespace BaseMethod.Data;

internal interface IJsonSet
{
    string Name { get; }

    string FileName { get; }

    object Deserialize(JsonElement json, JsonSerializerOptions options);

    void ReplaceFrom(object entities);

    void Write(Utf8JsonWriter writer, JsonSerializerOptions options);

    void Clear();

    void Validate();
}
