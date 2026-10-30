namespace FlowBoard.Api.Data;

public sealed class DevelopmentSeedSettings
{
    public DevelopmentSeedUserSettings Owner { get; set; } = new();

    public DevelopmentSeedUserSettings Ravi { get; set; } = new();

    public DevelopmentSeedUserSettings Priya { get; set; } = new();

    public DevelopmentSeedUserSettings Arjun { get; set; } = new();
}

public sealed class DevelopmentSeedUserSettings
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Role { get; set; } = "Employee";
}
