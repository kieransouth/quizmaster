namespace Kieran.Quizmaster.Application.Auth.Dtos;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
