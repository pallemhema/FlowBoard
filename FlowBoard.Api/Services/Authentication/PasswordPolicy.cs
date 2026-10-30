namespace FlowBoard.Api.Services.Authentication;

public static class PasswordPolicy
{
    public static bool IsStrong(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return false;

        if (password.Length < 8)
            return false;

        var hasUppercase = password.Any(char.IsUpper);
        var hasLowercase = password.Any(char.IsLower);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecialCharacter = password.Any(
            character => !char.IsLetterOrDigit(character));

        return hasUppercase
            && hasLowercase
            && hasDigit
            && hasSpecialCharacter;
    }
}