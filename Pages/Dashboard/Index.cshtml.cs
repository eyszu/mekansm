using Mekansm.Data;
using Mekansm.Models;
using Mekansm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mekansm.Pages.Dashboard;

public class IndexModel(PostRepository posts) : PageModel
{
    public IReadOnlyList<Post> Posts { get; private set; } = [];

    public async Task OnGetAsync() =>
        Posts = await posts.ListByAuthorAsync(User.GetUserId());

    public async Task<IActionResult> OnPostDeleteAsync(long id)
    {
        var deleted = await posts.DeleteAsync(id, User.GetUserId());
        TempData["Flash"] = deleted ? "Post deleted." : "Post not found.";
        return RedirectToPage();
    }
}
