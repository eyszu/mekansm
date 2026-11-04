namespace Mekansm.Models;

public sealed record AppUser(
    long Id,
    string Email,
    string Username,
    string PasswordHash,
    DateTime CreatedAt);

public sealed record Post(
    long Id,
    long AuthorId,
    string AuthorName,
    string Title,
    string Slug,
    string Excerpt,
    string BodyMd,
    string Status,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public bool IsPublished => Status == "published";
}

public sealed record Pager(int Page, int TotalPages);
