using System.ComponentModel.DataAnnotations;
using Mekansm.Data;
using Mekansm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Mekansm.Pages;

[EnableRateLimiting("auth")]
public class LoginModel(UserRepository users, PasswordService hasher) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public class LoginInput
    {
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password), StringLength(128)]
        public string Password { get; set; } = "";

        public bool RememberMe { get; set; }
    }

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? RedirectToPage("/Dashboard/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await users.FindByEmailAsync(Input.Email.Trim());
        var ok = hasher.Verify(Input.Password, user?.PasswordHash);
        if (user is null || !ok)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

        await AuthHelper.SignInAsync(HttpContext, user, Input.RememberMe);

        return !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? LocalRedirect(ReturnUrl)
            : RedirectToPage("/Dashboard/Index");
    }
}
