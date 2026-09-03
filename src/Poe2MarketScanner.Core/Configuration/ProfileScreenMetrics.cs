namespace Poe2MarketScanner.Core.Configuration;

public sealed record ProfileScreenMetrics(double Left, double Top, double Width, double Height)
{
    public static ProfileScreenMetrics Default { get; } = new(0, 0, 1920, 1080);

    public double SafeWidth => Width <= 0 ? Default.Width : Width;

    public double SafeHeight => Height <= 0 ? Default.Height : Height;
}
