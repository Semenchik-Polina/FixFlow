namespace FixFlow.Domain;

/// <summary>
/// Something that happened in an aggregate: an immutable record named in the past tense (<c>WorkOrderAssigned</c>),
/// carrying IDs and minimal data, never personal data.
/// </summary>
public interface IDomainEvent;
