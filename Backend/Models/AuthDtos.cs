namespace Backend.Models;

public sealed record SignUpRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    string ConfirmPassword);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(int Id, string FirstName, string LastName, string Email);