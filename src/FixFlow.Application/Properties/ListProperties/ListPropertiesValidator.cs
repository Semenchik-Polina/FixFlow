using FixFlow.Application.Paging;
using FixFlow.Application.Validation;
using FixFlow.Domain.Properties;
using FluentValidation;
using static FixFlow.Application.Validation.TextValidationRules;

namespace FixFlow.Application.Properties.ListProperties;

public sealed class ListPropertiesValidator : PagedQueryValidator<ListPropertiesQuery>
{
    public ListPropertiesValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        // No property's name is longer, so a longer filter can't match
        RuleFor(query => Trimmed(query.Name))
            .MaximumLength(Property.NameMaxLength)
            .NoControlCharacters()
            .OverridePropertyName(nameof(ListPropertiesQuery.Name));
    }
}
