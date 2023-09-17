namespace HaversineGenerator;

using HaversineShared;

internal sealed class App
{
    private readonly IHaversineFormula _formula;

    public App(IHaversineFormula formula)
    {
        _formula = formula;
    }

    public async Task RunAsync()
    {
        double Value = _formula.Reference(0.5, 0.5, 1.0, 1.0);
        Console.WriteLine($"The answer is {Value}");
        await Task.FromResult(0);
    }
}