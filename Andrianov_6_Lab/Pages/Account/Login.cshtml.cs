using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Andrianov_6_Lab.Services;

namespace Andrianov_6_Lab.Pages.Account;

public class LoginModel : PageModel
{
    private readonly VotingService _votingService;

    public LoginModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        var normalizedEmail = Email.Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Email and password are required.";
            return Page();
        }

        var user = await _votingService.GetUserByEmailAsync(normalizedEmail);
        if (user == null)
        {
            ErrorMessage = "Invalid credentials.";
            await _votingService.LogAuthenticationAsync("LOGIN_FAILED", normalizedEmail, null, new { reason = "user_not_found" });
            return Page();
        }

        // For demo: compare plain password to stored password_hash (in real app use proper hashing)
        if (user.PasswordHash != Password)
        {
            ErrorMessage = "Invalid credentials.";
            await _votingService.LogAuthenticationAsync("LOGIN_FAILED", normalizedEmail, user.Id, new { reason = "invalid_password" });
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName ?? user.Email),
        };
        if (!string.IsNullOrEmpty(user.RoleCode)) claims.Add(new Claim(ClaimTypes.Role, user.RoleCode));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        await _votingService.LogAuthenticationAsync("LOGIN_SUCCESS", normalizedEmail, user.Id);

        return RedirectToPage("/Index");
    }
}
