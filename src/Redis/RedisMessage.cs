using StackExchange.Redis;

public class RedisMessage : IDisposable
{
    private readonly IConnectionMultiplexer _connection;
    private readonly IDatabase _redis;
    private bool _disposed;

    public RedisMessage(string connectionString = "localhost")
    {
        _connection = ConnectionMultiplexer.Connect(connectionString);
        _redis = _connection.GetDatabase();
    }

    public RedisMessage(IConnectionMultiplexer connectionMultiplexer)
    {
        _connection = connectionMultiplexer;
        _redis = _connection.GetDatabase();
    }

    public bool SetMessage(string keyName, string message, TimeSpan? expiry = null)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));

        if (message == null)
            throw new ArgumentNullException(nameof(message));

        return _redis.StringSet(keyName, message, expiry);
    }

    public string GetMessage(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ArgumentException("Key name cannot be empty", nameof(keyName));

        return _redis.StringGet(keyName);
    }

    #region Hash Operations

    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        return await _redis.HashSetAsync(key, field, value);
    }

    public async Task<RedisValue> HashGetAsync(string key, string field)
    {
        return await _redis.HashGetAsync(key, field);
    }

    public async Task<HashEntry[]> HashGetAllAsync(string key)
    {
        return await _redis.HashGetAllAsync(key);
    }

    public async Task<long> HashDeleteAsync(string key, params string[] fields)
    {
        var redisValues = fields.Select(f => (RedisValue)f).ToArray();
        return await _redis.HashDeleteAsync(key, redisValues);
    }

    public async Task<RedisValue[]> HashKeysAsync(string key)
    {
        return await _redis.HashKeysAsync(key);
    }

    public async Task<long> HashLengthAsync(string key)
    {
        return await _redis.HashLengthAsync(key);
    }

    #endregion

    #region Set Operations

    public async Task<bool> SetAddAsync(string key, string value)
    {
        return await _redis.SetAddAsync(key, value);
    }

    public async Task<long> SetAddAsync(string key, params string[] values)
    {
        var redisValues = values.Select(v => (RedisValue)v).ToArray();
        return await _redis.SetAddAsync(key, redisValues);
    }

    public async Task<bool> SetRemoveAsync(string key, string value)
    {
        return await _redis.SetRemoveAsync(key, value);
    }

    public async Task<RedisValue[]> SetMembersAsync(string key)
    {
        return await _redis.SetMembersAsync(key);
    }

    public async Task<bool> SetContainsAsync(string key, string value)
    {
        return await _redis.SetContainsAsync(key, value);
    }

    public async Task<long> SetLengthAsync(string key)
    {
        return await _redis.SetLengthAsync(key);
    }

    #endregion

    #region Key Operations

    public async Task<bool> KeyDeleteAsync(string key)
    {
        return await _redis.KeyDeleteAsync(key);
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _redis.KeyExistsAsync(key);
    }

    public async Task<bool> KeyExpireAsync(string key, TimeSpan expiry)
    {
        return await _redis.KeyExpireAsync(key, expiry);
    }

    #endregion

    #region Utility Methods

    public async Task<TimeSpan> PingAsync()
    {
        return await _redis.PingAsync();
    }

    public IDatabase GetDatabase()
    {
        return _redis;
    }

    public IConnectionMultiplexer GetConnection()
    {
        return _connection;
    }

    #endregion

    #region Dispose Pattern

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing) _connection?.Dispose();
            _disposed = true;
        }
    }

    #endregion
}