using Microsoft.Extensions.Logging;

using HaversineShared;

internal sealed class App
{
    private readonly ILogger<App> _logger;
    private readonly IHaversineFormula _formula;

    public App(
        ILogger<App> logger, IHaversineFormula formula) =>
            (_logger, _formula) = 
            ( logger,  formula);

    public async Task RunAsync()
    {
        double Value = _formula.Reference(0.5, 0.5, 1.0, 1.0);
        _logger.LogTrace("Awesome trace log");
        _logger.LogDebug("debug message");
        _logger.LogInformation("Hello?");
        _logger.LogWarning("The answer is: {Value}", Value);
        await Task.FromResult(0);
    }
}