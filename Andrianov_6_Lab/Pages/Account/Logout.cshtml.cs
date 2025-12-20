using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Andrianov_6_Lab.Services;

namespace Andrianov_6_Lab.Pages.Account;

public class LogoutModel : PageModel
{
    private readonly VotingService _votingService;

    public LogoutModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var email = User?.Identity?.Name ?? string.Empty;
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        await _votingService.LogAuthenticationAsync("LOGOUT", email, userId);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Index");
    }
}
