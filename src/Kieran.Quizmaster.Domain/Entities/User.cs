using Microsoft.AspNetCore.Identity;

namespace Kieran.Quizmaster.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;

    public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
}
