using Mekansm.Data;
using Mekansm.Models;
using Mekansm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mekansm.Pages;

public class PostModel(PostRepository posts, MarkdownService markdown) : PageModel
{
    public Post? Item { get; private set; }
    public string Html { get; private set; } = "";
    public bool IsOwner { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var post = await posts.GetBySlugAsync(slug);
        if (post is null) return NotFound();

        IsOwner = User.Identity?.IsAuthenticated == true && post.AuthorId == User.GetUserId();
        if (!post.IsPublished && !IsOwner) return NotFound();

        Item = post;
        Html = markdown.ToHtml(post.BodyMd);
        return Page();
    }
}
