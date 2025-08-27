namespace FlippingExilesPublicStashAPI.API;

public class RateLimiter
{
    private static int _maxRequests = 30; // Default: 30 requests per 300 sec
    private static int _windowSeconds = 300; // Default: 300 sec window
    private static readonly object _lock = new();
    private static ILogger _logger = null!;
    private static DateTime _lastRequestTime = DateTime.UtcNow; // Tracks last request time

    /// <summary>
    /// Initializes the logger from Program.cs
    /// </summary>
    public RateLimiter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Waits asynchronously until a request can be made within the rate limit.
    /// </summary>
    public async Task WaitAsync(CancellationToken cancellationToken = default)
    {
        int delay;
        lock (_lock)
        {
            var nextAllowedTime = _lastRequestTime.AddSeconds(_windowSeconds / _maxRequests + 1);
            var waitTime = (int)(nextAllowedTime - DateTime.UtcNow).TotalMilliseconds;

            delay = waitTime > 0 ? waitTime : 0;

            _logger?.LogDebug("Waiting {Delay} ms before next request.", delay);
            _lastRequestTime = DateTime.UtcNow.AddMilliseconds(delay);
        }

        if (delay > 0)
        {
            await Task.Delay(delay, cancellationToken);
        }

        _logger?.LogDebug("Request allowed at {Time}.", DateTime.UtcNow);
    }

    /// <summary>
    /// Updates the rate limit dynamically based on API response headers.
    /// </summary>
    public void UpdateRateLimits(int maxRequests, int windowSeconds)
    {
        lock (_lock)
        {
            _maxRequests = maxRequests;
            _windowSeconds = windowSeconds;
        }

        _logger?.LogInformation("RateLimiter updated: {MaxRequests} requests per {WindowSeconds} seconds", maxRequests, windowSeconds);
    }
}