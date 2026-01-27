namespace OrderProcessor;

public class OrderProcessorSettings
{
    public int PollingIntervalMinutes { get; set; } = 5;
    public int BatchSize { get; set; } = 100;
}
