using System.Text.Json;

namespace SuperCalc.Core;

public sealed record Calculation(string Expression, string Result, DateTimeOffset Timestamp);

public sealed class AppState
{
    public bool Onboarded { get; set; }
    public bool FocusMode { get; set; }
    public bool DramaticMode { get; set; } = true;
    public bool Animations { get; set; } = true;
    public bool Recommendations { get; set; } = true;
    public bool ExitSurvey { get; set; } = true;
    public bool LocalPersona { get; set; }
    public bool PremiumPretend { get; set; }
    public int Calculations { get; set; }
    public int Ceremonies { get; set; }
    public decimal Memory { get; set; }
    public List<Calculation> History { get; set; } = [];

    public void Record(string expression, decimal value)
    {
        History.Insert(0, new(expression, Calculator.Format(value), DateTimeOffset.Now));
        if (History.Count > 100) History.RemoveRange(100, History.Count - 100);
        Calculations++;
    }
}

public sealed class StateStore(string path)
{
    public string? LastError { get; private set; }
    public AppState Load()
    {
        try
        {
            if (!File.Exists(path)) return new();
            var state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(path)) ?? new();
            state.History = (state.History ?? []).Where(x => x is not null && x.Expression is not null && x.Result is not null).Take(100).ToList();
            return state;
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        { LastError = "本地设置无法读取，本次使用默认设置。"; return new(); }
    }

    public bool Save(AppState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
            LastError = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { LastError = "未能保存到本地磁盘；本次会话仍可继续。"; return false; }
    }
}
