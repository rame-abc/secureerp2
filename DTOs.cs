namespace SecureERP2
{
    // Authentication DTOs
    public record LoginRequest(string Username, string Password);
    public record LoginResponse(string Token, UserInfo User);
    public record UserInfo(int Id, string Username, string Email, string Role);
    
    // User Management DTOs
    public record CreateUserRequest(string Username, string Password, string Email, string Role, string FirstName, string LastName);
    public record CreateUserRequestWithRoleId(string Username, string Password, string Email, int RoleId, string FirstName, string LastName, string? PhoneNumber = null, string? Department = null);
    public record UpdateUserRequest(string? Email, string? Role, string? FirstName, string? LastName, bool? IsActive);
    public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    public record UserResponse(int Id, string Username, string Email, string Role, string FirstName, string LastName, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt);
    
    // Company Management DTOs
    public record AddUserToCompanyRequest(int UserId, string Role, bool IsCompanyAdmin = false);
    public record UpdateSettingRequest(string Value);
    public record SwitchCompanyRequest(int CompanyId);
    public record ValidateCompanyAccessRequest(int CompanyId);
}
