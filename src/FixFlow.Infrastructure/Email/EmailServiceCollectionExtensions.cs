using FixFlow.Application.Email;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// <see cref="IEmailSender"/> over SMTP with MailKit, configured from <see cref="SmtpOptions.SectionName"/>. The
    /// settings are checked when the first message is sent, so a run that sends none needs none; a wrong setting fails
    /// that send with <c>OptionsValidationException</c>.
    /// </summary>
    public static IServiceCollection AddFixFlowEmail(this IServiceCollection services)
    {
        services.AddOptions<SmtpOptions>()
            .BindConfiguration(SmtpOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                smtp => string.IsNullOrEmpty(smtp.UserName) == string.IsNullOrEmpty(smtp.Password),
                "Smtp:UserName and Smtp:Password are set together or not at all.");

        services.AddSingleton<IEmailSender, MailKitEmailSender>();

        return services;
    }
}
