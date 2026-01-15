using System;

namespace Ling.Mapper.Models;

internal readonly struct AdaptOptionsKey(AdaptOptions options) : IEquatable<AdaptOptionsKey>
{
    private readonly AdaptOptions _options = options;

    public bool Equals(AdaptOptionsKey other)
        => _options == other._options;

    public override bool Equals(object? obj)
        => obj is AdaptOptionsKey other && Equals(other);

    public override int GetHashCode()
        => (int)_options;

    public override string ToString()
        => _options.ToString();
}
