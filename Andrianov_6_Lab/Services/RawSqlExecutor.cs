using System.Data;
using Npgsql;
namespace Andrianov_6_Lab.Services;

public class RawSqlExecutor
{
    private readonly string _connectionString;
    private readonly string _scriptsPath;

    public RawSqlExecutor(string connectionString, string scriptsPath)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _scriptsPath = scriptsPath ?? ".";
    }

    public IEnumerable<string> GetScriptFiles()
    {
        try
        {
            if (!Path.IsPathRooted(_scriptsPath))
            {
                var baseDir = AppContext.BaseDirectory;
                var folder = Path.GetFullPath(Path.Combine(baseDir, _scriptsPath));
                return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.sql").Select(Path.GetFileName) : Array.Empty<string>();
            }
            return Directory.Exists(_scriptsPath) ? Directory.GetFiles(_scriptsPath, "*.sql").Select(Path.GetFileName) : Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public string? ReadScript(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        string path = Path.IsPathRooted(fileName) ? fileName : Path.Combine(GetScriptsFolder(), fileName);
        if (!File.Exists(path)) return null;
        return File.ReadAllText(path);
    }

    private string GetScriptsFolder()
    {
        if (Path.IsPathRooted(_scriptsPath)) return _scriptsPath;
        var baseDir = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(baseDir, _scriptsPath));
    }

    public async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string sql)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.CommandTimeout = 60;
        try
        {
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dict = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var name = reader.GetName(i);
                    var val = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
                    dict[name] = val;
                }
                rows.Add(dict);
            }
        }
        finally
        {
            await conn.CloseAsync();
        }
        return rows;
    }
}
