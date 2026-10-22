namespace FixFlow.Domain;

/// <summary>
/// A business rule violation: a transition or change the aggregate doesn't allow, including for the actor's role.
/// The API answers 422 with the message as the Problem Details <c>detail</c>, so write it for the user and keep
/// personal data out of it.
/// </summary>
public class DomainException(string message) : Exception(message);
