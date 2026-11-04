using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Mekansm.Pages;

[IgnoreAntiforgeryToken]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorModel : PageModel
{
    public int Code { get; private set; } = 500;

    public void OnGet(int? code) => Code = code ?? 500;

    // Status-code re-execution keeps the original verb (e.g. a POST that 404s).
    public void OnPost(int? code) => Code = code ?? 500;
}
