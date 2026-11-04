using Mekansm.Data;
using Mekansm.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mekansm.Pages;

public class AuthorModel(PostRepository posts) : PageModel
{
    private const int PageSize = 12;

    public string Username { get; private set; } = "";
    public IReadOnlyList<Post> Posts { get; private set; } = [];
    public Pager Pager { get; private set; } = new(1, 1);

    public async Task<IActionResult> OnGetAsync(string username, int p = 1)
    {
        Username = username;
        var total = await posts.CountPublishedAsync(username);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(p, 1, pages);

        Posts = await posts.ListPublishedAsync(page, PageSize, username);
        Pager = new Pager(page, pages);
        return Page();
    }
}
