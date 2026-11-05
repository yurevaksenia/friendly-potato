using BaseMethod.Models;

namespace BaseMethod.Data;

/// <summary>
/// Общий контекст всех расчётных методик.
/// </summary>
public abstract class MethodDataContext : JsonDataContext
{
    protected MethodDataContext(string databasePath) : base(databasePath)
    {
        Headers = RegisterSet<Header>("headers", "headers.json");
    }

    public JsonSet<Header> Headers { get; }
}
