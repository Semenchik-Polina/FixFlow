using FixFlow.Application.Auth;

namespace FixFlow.Application.Properties;

/// <summary>What a caller does to a property, checked by <see cref="PropertyAuthorizationHandler"/>.</summary>
public static class PropertyOps
{
    public static readonly Operation Update = new("Property.Update");

    /// <summary>Archiving it, with its units.</summary>
    public static readonly Operation Archive = new("Property.Archive");

    public static readonly Operation AddUnit = new("Property.AddUnit");
}
