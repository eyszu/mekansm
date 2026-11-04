using Mekansm.Data;
using Mekansm.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mekansm.Pages;

public class IndexModel(PostRepository posts) : PageModel
{
    private const int PageSize = 12;

    public IReadOnlyList<Post> Posts { get; private set; } = [];
    public Pager Pager { get; private set; } = new(1, 1);

    public async Task OnGetAsync(int p = 1)
    {
        var total = await posts.CountPublishedAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        var page = Math.Clamp(p, 1, pages);

        Posts = await posts.ListPublishedAsync(page, PageSize);
        Pager = new Pager(page, pages);
    }
}
