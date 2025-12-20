using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Andrianov_6_Lab.Services;
using Andrianov_6_Lab.Models;

namespace Andrianov_6_Lab.Pages.Admin;

[Authorize(Roles = "ADMIN")]
public class AddCandidateModel : PageModel
{
    private readonly VotingService _votingService;

    public AddCandidateModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    public VotingSession? Session { get; set; }
    public List<CandidateType> CandidateTypes { get; set; } = new();

    private IActionResult? ForbidIfNotAdmin()
    {
        if (!(User?.Identity?.IsAuthenticated ?? false)) return Challenge();
        return User.IsInRole("ADMIN") ? null : Forbid();
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        Session = await _votingService.GetSessionWithCandidatesAsync(id, includeUnpublished: true);
        if (Session == null)
        {
            return NotFound();
        }
        CandidateTypes = await _votingService.GetCandidateTypesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, string fullName, string description, string candidateType)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        try
        {
            await _votingService.AddCandidateAsync(id, candidateType, fullName, description);
            TempData["Message"] = "Candidate added successfully!";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostUpdateAsync(Guid id, Guid candidateId, string fullName, string description, string candidateType)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        await _votingService.UpdateCandidateAsync(candidateId, candidateType, fullName, description);
        TempData["Message"] = "Candidate updated.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid id, Guid candidateId)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        await _votingService.DeleteCandidateAsync(candidateId);
        TempData["Message"] = "Candidate removed.";
        return RedirectToPage(new { id });
    }
}