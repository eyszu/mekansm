using System.ComponentModel.DataAnnotations;
using Mekansm.Data;
using Mekansm.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Mekansm.Pages;

[EnableRateLimiting("auth")]
public class RegisterModel(UserRepository users, PasswordService hasher) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public class RegisterInput
    {
        [Required, EmailAddress, StringLength(254)]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8,
            ErrorMessage = "Password must be between 8 and 128 characters.")]
        public string Password { get; set; } = "";
    }

    public IActionResult OnGet() =>
        User.Identity?.IsAuthenticated == true ? RedirectToPage("/Dashboard/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var email = Input.Email.Trim().ToLowerInvariant();
        var user = await users.CreateAsync(email, hasher.Hash(Input.Password));
        if (user is null)
        {
            ModelState.AddModelError("Input.Email", "That email is already registered.");
            return Page();
        }

        await AuthHelper.SignInAsync(HttpContext, user, persistent: true);
        return RedirectToPage("/Dashboard/Index");
    }
}
