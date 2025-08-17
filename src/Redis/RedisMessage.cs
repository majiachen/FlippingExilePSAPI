using StackExchange.Redis;
using System;

public class RedisMessage : IDisposable
{
    private readonly ConnectionMultiplexer _redisConnection;
    private readonly IDatabase _redisDatabase;
    private readonly ILogger<RedisMessage> _logger;

    
    public RedisMessage(string connectionString, ILogger<RedisMessage> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string cannot be empty", nameof(connectionString));

        _redisConnection = ConnectionMultiplexer.Connect(connectionString);
        _redisDatabase = _redisConnection.GetDatabase();
        _logger = logger;
    }

    public bool SetMessage(string keyName, string message, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));
            
        if (message == null)
            throw new ArgumentNullException(nameof(message));
            
        _logger?.LogInformation($"Sending message to redis with key '{keyName}': {message}");
        return _redisDatabase.StringSet(keyName, message, expiry);
    }

    public string GetMessage(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));
            
        return _redisDatabase.StringGet(keyName);
    }

    public bool DeleteMessage(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));
            
        return _redisDatabase.KeyDelete(keyName);
    }

    public bool MessageExists(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));
            
        return _redisDatabase.KeyExists(keyName);
    }

    public void Dispose()
    {
        _redisConnection?.Dispose();
        GC.SuppressFinalize(this);
    }
}