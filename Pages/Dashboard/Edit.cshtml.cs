using System.ComponentModel.DataAnnotations;
using Mekansm.Data;
using Mekansm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mekansm.Pages.Dashboard;

public class EditModel(PostRepository posts, MarkdownService markdown) : PageModel
{
    [BindProperty]
    public PostInput Input { get; set; } = new();

    public long? PostId { get; private set; }
    public string? Slug { get; private set; }
    public bool IsPublished { get; private set; }

    public class PostInput
    {
        [Required, StringLength(200)]
        public string Title { get; set; } = "";

        [Required(ErrorMessage = "Write something first.")]
        [StringLength(200_000)]
        public string Body { get; set; } = "";

        [StringLength(500)]
        public string? Excerpt { get; set; }

        public bool Publish { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(long? id)
    {
        if (id is null) return Page();

        var post = await posts.GetByIdAsync(id.Value);
        if (post is null || post.AuthorId != User.GetUserId()) return NotFound();

        PostId = post.Id;
        Slug = post.Slug;
        IsPublished = post.IsPublished;
        Input = new PostInput
        {
            Title = post.Title,
            Body = post.BodyMd,
            Excerpt = post.Excerpt,
            Publish = post.IsPublished,
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(long? id)
    {
        var userId = User.GetUserId();
        PostId = id;

        if (id is not null)
        {
            var existing = await posts.GetByIdAsync(id.Value);
            if (existing is null || existing.AuthorId != userId) return NotFound();
            Slug = existing.Slug;
            IsPublished = existing.IsPublished;
        }

        if (!ModelState.IsValid) return Page();

        var title = Input.Title.Trim();
        var excerpt = string.IsNullOrWhiteSpace(Input.Excerpt)
            ? markdown.ToExcerpt(Input.Body)
            : Input.Excerpt.Trim();

        if (id is null)
        {
            var created = await posts.CreateAsync(userId, title, excerpt, Input.Body, Input.Publish);
            id = created.Id;
        }
        else
        {
            await posts.UpdateAsync(id.Value, userId, title, excerpt, Input.Body, Input.Publish);
        }

        TempData["Flash"] = Input.Publish ? "Published." : "Saved as draft.";
        return RedirectToPage(new { id });
    }
}
