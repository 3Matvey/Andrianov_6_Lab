using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Andrianov_6_Lab.Services;
using Andrianov_6_Lab.Models;

namespace Andrianov_6_Lab.Pages.Admin;

[Authorize]
    public class AdminModel : PageModel
    {
        private readonly VotingService _votingService;

    public AdminModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    public List<VotingSession> Sessions { get; set; } = new();

    public async Task OnGetAsync()
    {
        Sessions = await _votingService.GetAllSessionsAsync();
    }

    public async Task<IActionResult> OnPostCreateSessionAsync(string title, string description, DateTime startAt, DateTime endAt, string visibility, bool anonymous, bool multiSelect, int maxChoices)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Challenge();
            var adminId = Guid.Parse(userIdClaim);
            await _votingService.CreateSessionAsync(title, description, adminId, startAt, endAt, visibility, anonymous, multiSelect, maxChoices);
            TempData["Message"] = "Session created successfully!";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPublishSessionAsync(Guid sessionId)
    {
        try
        {
            await _votingService.PublishSessionAsync(sessionId);
            TempData["Message"] = "Session published successfully!";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage();
    }
}