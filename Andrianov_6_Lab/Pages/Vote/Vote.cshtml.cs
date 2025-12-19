using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Andrianov_6_Lab.Services;
using Andrianov_6_Lab.Models;

namespace Andrianov_6_Lab.Pages.Vote;

public class VoteModel : PageModel
{
    private readonly VotingService _votingService;

    public VoteModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    public VotingSession? Session { get; set; }

    [BindProperty]
    public Guid SelectedCandidate { get; set; }

    [BindProperty]
    public List<Guid> SelectedCandidates { get; set; } = new();
    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Session = await _votingService.GetSessionWithCandidatesAsync(id);
        if (Session == null)
        {
            return NotFound();
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        Session = await _votingService.GetSessionWithCandidatesAsync(id);
        if (Session == null)
        {
            return NotFound();
        }

        if (Session.Settings?.MultiSelect == true && SelectedCandidates.Count > Session.Settings.MaxChoices)
        {
            ModelState.AddModelError(string.Empty, $"You can select no more than {Session.Settings.MaxChoices} candidates.");
            return Page();
        }

        if (Session.Settings?.MultiSelect != true && SelectedCandidate == Guid.Empty)
        {
            ModelState.AddModelError(string.Empty, "Please select a candidate.");
            return Page();
        }

        // Use authenticated user id if available
        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        Guid userId = Guid.Empty;
        if (!string.IsNullOrEmpty(userIdStr)) Guid.TryParse(userIdStr, out userId);

        try
        {
            if (Session.Settings?.MultiSelect == true)
            {
                foreach (var candidateId in SelectedCandidates)
                {
                    await _votingService.CastVoteAsync(userId == Guid.Empty ? Guid.Empty : userId, candidateId);
                }
            }
            else if (SelectedCandidate != Guid.Empty)
            {
                await _votingService.CastVoteAsync(userId == Guid.Empty ? Guid.Empty : userId, SelectedCandidate);
            }

            TempData["Message"] = "Your vote has been submitted successfully!";
            return RedirectToPage("/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Error submitting vote: {ex.Message}");
            return Page();
        }
    }
}