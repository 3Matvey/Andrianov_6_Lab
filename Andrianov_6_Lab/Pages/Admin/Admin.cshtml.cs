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
        // Get all sessions (including unpublished for admin)
        var sql = "SELECT id, title, description, created_by, start_at, end_at, is_published, visibility, created_at FROM voting.voting_sessions ORDER BY created_at DESC";
        var results = await _votingService.ExecuteRawQueryAsync(sql);
        Sessions = results.Select(r => new VotingSession
        {
            Id = Guid.Parse(r["id"]?.ToString() ?? ""),
            Title = r["title"]?.ToString() ?? "",
            Description = r["description"]?.ToString(),
            CreatedBy = Guid.Parse(r["created_by"]?.ToString() ?? ""),
            StartAt = DateTime.Parse(r["start_at"]?.ToString() ?? ""),
            EndAt = DateTime.Parse(r["end_at"]?.ToString() ?? ""),
            IsPublished = bool.Parse(r["is_published"]?.ToString() ?? "false"),
            Visibility = r["visibility"]?.ToString() ?? "private",
            CreatedAt = DateTime.Parse(r["created_at"]?.ToString() ?? "")
        }).ToList();
    }

    public async Task<IActionResult> OnPostCreateSessionAsync(string title, string description, DateTime startAt, DateTime endAt, string visibility)
    {
        try
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim)) return Challenge();
            var adminId = Guid.Parse(userIdClaim);
            await _votingService.CreateSessionAsync(title, description, adminId, startAt, endAt, visibility);
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