using Andrianov_6_Lab.Models;
using Andrianov_6_Lab.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Andrianov_6_Lab.Pages.Admin;

[Authorize(Roles = "ADMIN")]
public class EditSessionModel : PageModel
{
    private readonly VotingService _votingService;

    public EditSessionModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    public VotingSession? Session { get; set; }
    public VotingSettings Settings { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Session = await _votingService.GetSessionWithCandidatesAsync(id, includeUnpublished: true);
        if (Session == null)
        {
            return NotFound();
        }
        Settings = Session.Settings ?? new VotingSettings { SessionId = Session.Id };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid sessionId, string title, string description, DateTime startAt, DateTime endAt, string visibility, bool anonymous, bool multiSelect, int maxChoices)
    {
        await _votingService.UpdateSessionAsync(sessionId, title, description, startAt, endAt, visibility, anonymous, multiSelect, maxChoices);
        TempData["Message"] = "Session updated.";
        return RedirectToPage(new { id = sessionId });
    }
}
