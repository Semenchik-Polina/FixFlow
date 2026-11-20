using System.Diagnostics.CodeAnalysis;
using System.Text;
using FixFlow.Domain;
using FixFlow.Domain.Properties;

namespace FixFlow.Application.Properties;

/// <summary>
/// A property's address as requests and responses carry it. Personal data, like the domain's <see cref="Address"/>
/// (S24): it prints no parts, so a logged request or command can't leak it.
/// </summary>
public sealed record PropertyAddress(string Line1, string? Line2, string City, string? PostalCode, string CountryCode)
{
    /// <exception cref="DomainException">A part isn't valid (<see cref="Address.Create"/>).</exception>
    internal Address ToDomain() => Address.Create(Line1, Line2, City, PostalCode, CountryCode);

    [SuppressMessage("Performance", "CA1822", Justification = "A record's PrintMembers is an instance method")]
    private bool PrintMembers(StringBuilder builder) => false;
}
