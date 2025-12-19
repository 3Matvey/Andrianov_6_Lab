using System.Security.Claims;
using Andrianov_6_Lab.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Andrianov_6_Lab.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly VotingService _votingService;

    public RegisterModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string FullName { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(FullName))
        {
            ErrorMessage = "All fields are required.";
            return Page();
        }

        try
        {
            await _votingService.RegisterUserAsync(Email.Trim(), Password.Trim(), FullName.Trim());
            var user = await _votingService.GetUserByEmailAsync(Email.Trim());
            if (user != null)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.FullName ?? user.Email)
                };
                if (!string.IsNullOrEmpty(user.RoleCode))
                {
                    claims.Add(new Claim(ClaimTypes.Role, user.RoleCode));
                }

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
            }
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }
}
