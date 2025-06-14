using StackExchange.Redis;
using System;

public class RedisMessage : IDisposable
{
    private readonly string _redisKey;
    private readonly ConnectionMultiplexer _redisConnection;
    private readonly IDatabase _redisDatabase;
    private readonly IConfiguration _config;
    private readonly ILogger<RedisMessage> _logger;

    
    public RedisMessage(IConfiguration config)
    {
        _config = config;
    }

    public RedisMessage(string connectionString, string keyName,ILogger<RedisMessage> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Redis connection string cannot be empty", nameof(connectionString));
        
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));

        _redisKey = keyName;
        _redisConnection = ConnectionMultiplexer.Connect(connectionString);
        _redisDatabase = _redisConnection.GetDatabase();
        this._logger = logger;
    }

    public bool SetMessage(string message, TimeSpan? expiry = null)
    {
        if (message == null)
            throw new ArgumentNullException(nameof(message));
        _logger.LogInformation($"Sending message to redis: {message}");
        return _redisDatabase.StringSet(_redisKey, message, expiry);
    }

    public string GetMessage()
    {
        return _redisDatabase.StringGet(_redisKey);
    }

    public bool DeleteMessage()
    {
        return _redisDatabase.KeyDelete(_redisKey);
    }

    public bool MessageExists()
    {
        return _redisDatabase.KeyExists(_redisKey);
    }

    public void Dispose()
    {
        _redisConnection?.Dispose();
        GC.SuppressFinalize(this);
    }
}