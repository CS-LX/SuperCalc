using SuperCalc.Core;

var passed = 0;
var failed = 0;
void Test(string name, Action action)
{
    try { action(); passed++; Console.WriteLine("PASS " + name); }
    catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
}
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
}
void Reject<T>(string expression) where T : Exception
{
    try { Calculator.Evaluate(expression); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}

foreach (var (expression, answer) in new (string, decimal)[]
{
    ("2+2", 4), ("0.1+0.2", .3m), ("2+3*4", 14), ("(2+3)*4", 20),
    ("12÷3×2", 8), ("5−8", -3), ("-2^2", -4), ("(-2)^2", 4),
    ("2^3^2", 512), ("2^-3", .125m), ("50%", .5m), ("200*10%", 20),
    ("200+10%", 200.1m), ("sqrt(81)", 9), ("sqrt(16)+2^3", 12),
    ("1/(2+2)", .25m), ("3--2", 5), (".5 + .25", .75m),
    (" 12 + 3 ", 15), ("0^0", 1), ("10^-28", .0000000000000000000000000001m),
    ("100000000000000000000^-2", 0), ("100%%", .01m), ("+5", 5),
    ("79228162514264337593543950335", decimal.MaxValue), ("(12+8)×5", 100)
}) Test(expression, () => Equal(answer, Calculator.Evaluate(expression)));

foreach (var expression in new[] { "", "abc", "1+", "(1+2", "1..2", "1 2", "sqrt(-1)", "2^0.5", "2^101", "1e3", "2(3)", new string('(', 70) + "1" + new string(')', 70), new string('1', 513) })
    Test("reject " + expression[..Math.Min(30, expression.Length)], () => Reject<FormatException>(expression));
Test("division by zero", () => Reject<DivideByZeroException>("1/0"));
Test("negative power zero", () => Reject<DivideByZeroException>("0^-1"));
Test("overflow", () => Reject<OverflowException>("79228162514264337593543950335+1"));
Test("format decimal", () => Equal("0.3", Calculator.Format(.30m)));
Test("format small decimal remains parseable", () => Equal(.0000000000000000000000000001m, Calculator.Evaluate(Calculator.Format(.0000000000000000000000000001m))));

var directory = Path.Combine(Path.GetTempPath(), "SuperCalc.Tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "state.json");
    Test("missing state defaults", () => Equal(false, new StateStore(path).Load().Onboarded));
    Test("history capped and ordered", () =>
    {
        var state = new AppState();
        for (var i = 0; i < 105; i++) state.Record(i.ToString(), i);
        Equal(100, state.History.Count); Equal("104", state.History[0].Result); Equal(105, state.Calculations);
    });
    Test("persistence roundtrip", () =>
    {
        var state = new AppState { FocusMode = true, Memory = 12.5m, Onboarded = true, ResultExperienceChosen = true, OpenResultsWithAssistant = false };
        state.Record("2+2", 4);
        var store = new StateStore(path);
        Equal(true, store.Save(state));
        var loaded = store.Load();
        Equal(true, loaded.FocusMode); Equal(12.5m, loaded.Memory); Equal("4", loaded.History[0].Result);
        Equal(true, loaded.ResultExperienceChosen); Equal(false, loaded.OpenResultsWithAssistant);
    });
    Test("invalid JSON recovers", () =>
    {
        File.WriteAllText(path, "{broken"); var store = new StateStore(path);
        Equal(0, store.Load().History.Count); Equal(true, store.LastError is not null);
    });
    Test("null history recovers", () =>
    {
        File.WriteAllText(path, "{\"History\":null}"); Equal(0, new StateStore(path).Load().History.Count);
    });
    Test("blocked save reports failure", () => Equal(false, new StateStore(directory).Save(new AppState())));
}
finally { Directory.Delete(directory, recursive: true); }
Console.WriteLine($"\n{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;
