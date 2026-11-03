using FixFlow.Application.Validation;
using FixFlow.Domain.Organizations;
using FluentValidation;

namespace FixFlow.Application.Organizations;

/// <summary>
/// <see cref="Organization"/>'s rules as validation rules, so a bad name or slug is a 400 with a field error rather
/// than the domain's 422. The domain still enforces them. Each starts with <c>NotEmpty</c>; validators set
/// <c>RuleLevelCascadeMode = CascadeMode.Stop</c>, so a rule reports its first failure only.
/// </summary>
public static class OrganizationValidationRules
{
    public static IRuleBuilderOptions<T, string?> OrganizationName<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().MaximumLength(Organization.NameMaxLength).NoControlCharacters();

    /// <summary>
    /// <see cref="OrganizationSlug.IsValid"/>, with its message. Taken as written (P21): never trimmed or lowercased.
    /// Whether another organization has it is the unique index's job, a 422.
    /// </summary>
    public static IRuleBuilderOptionsConditions<T, string?> Slug<T>(this IRuleBuilder<T, string?> rule) =>
        rule.NotEmpty().Custom((slug, context) =>
        {
            // NotEmpty reports a null
            if (slug is not null && !OrganizationSlug.IsValid(slug, out var error))
            {
                context.AddFailure(error);
            }
        });
}
