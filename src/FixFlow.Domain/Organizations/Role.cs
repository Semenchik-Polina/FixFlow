namespace FixFlow.Domain.Organizations;

/// <summary>
/// The fixed roles (D7), held per membership: a person can have different roles in each organization. Admin can do
/// everything a Manager can; the policies grant it that, so an Admin membership doesn't also need Manager.
/// </summary>
public enum Role
{
    Tenant,
    Technician,
    Manager,
    Admin,
}
