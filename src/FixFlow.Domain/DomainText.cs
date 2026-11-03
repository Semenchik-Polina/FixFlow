namespace FixFlow.Domain;

/// <summary>
/// The rule for one line of text a user types (a name, an address line): trimmed, within a maximum length, and without
/// control characters. PostgreSQL can't store NUL, and a line break doesn't belong in a single-line field.
/// </summary>
internal static class DomainText
{
    /// <param name="value">The text as typed.</param>
    /// <param name="maxLength">The longest it may be once trimmed.</param>
    /// <param name="label">What it is, as the start of a sentence: "A property's name".</param>
    /// <returns>The trimmed text.</returns>
    /// <exception cref="DomainException">The text is blank, too long or contains a control character.</exception>
    public static string Required(string value, int maxLength, string label)
    {
        ArgumentNullException.ThrowIfNull(value);

        return Optional(value, maxLength, label) ?? throw new DomainException($"{label} is required.");
    }

    /// <returns>The trimmed text, or null when it is null or blank.</returns>
    /// <exception cref="DomainException">The text is too long or contains a control character.</exception>
    /// <inheritdoc cref="Required" path="/param"/>
    public static string? Optional(string? value, int maxLength, string label)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{label} can't be longer than {maxLength} characters.");
        }

        if (trimmed.Any(char.IsControl))
        {
            throw new DomainException($"{label} can't contain line breaks or other control characters.");
        }

        return trimmed;
    }
}
