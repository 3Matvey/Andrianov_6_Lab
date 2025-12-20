using System.Security.Claims;
using Andrianov_6_Lab.Models;
using Andrianov_6_Lab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Andrianov_6_Lab.Pages.Notifications;

[Authorize]
public class IndexModel : PageModel
{
    private readonly VotingService _votingService;

    public IndexModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    public List<Notification> Notifications { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Challenge();
        }

        Notifications = await _votingService.GetUserNotificationsAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostMarkReadAsync(Guid id)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Challenge();
        }

        await _votingService.MarkNotificationReadAsync(id, userId);
        return RedirectToPage();
    }
}
