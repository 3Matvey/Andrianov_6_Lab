using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using Andrianov_6_Lab.Services;
using Andrianov_6_Lab.Models;

namespace Andrianov_6_Lab.Pages.Admin;

[Authorize(Roles = "ADMIN")]
public class AdminModel : PageModel
{
    private readonly VotingService _votingService;

    public AdminModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    private IActionResult? ForbidIfNotAdmin()
    {
        return User.IsInRole("ADMIN") ? null : Forbid();
    }

    public List<VotingSession> Sessions { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<Role> Roles { get; set; } = new();
    public List<UserStatus> Statuses { get; set; } = new();

    public async Task OnGetAsync()
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return;
        Sessions = await _votingService.GetAllSessionsAsync();
        Users = await _votingService.GetAllUsersAsync();
        Roles = await _votingService.GetRolesAsync();
        Statuses = await _votingService.GetUserStatusesAsync();
    }

    public async Task<IActionResult> OnPostCreateSessionAsync(string title, string description, DateTime startAt, DateTime endAt, string visibility, bool anonymous, bool multiSelect, int maxChoices)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
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
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
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

    public async Task<IActionResult> OnPostUpdateSessionAsync(Guid sessionId, string title, string description, DateTime startAt, DateTime endAt, string visibility, bool anonymous, bool multiSelect, int maxChoices)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        try
        {
            await _votingService.UpdateSessionAsync(sessionId, title, description, startAt, endAt, visibility, anonymous, multiSelect, maxChoices);
            TempData["Message"] = "Session updated successfully!";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteSessionAsync(Guid sessionId)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        try
        {
            await _votingService.DeleteSessionAsync(sessionId);
            TempData["Message"] = "Session deleted.";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostCreateUserAsync(string email, string password, string fullName, Guid roleId, Guid statusId)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        try
        {
            await _votingService.RegisterUserAsync(email, password, fullName);
            var created = await _votingService.GetUserByEmailAsync(email);
            if (created != null)
            {
                await _votingService.UpdateUserAsync(created.Id, fullName, roleId, statusId);
            }
            TempData["Message"] = "User created successfully.";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateUserAsync(Guid userId, string fullName, Guid roleId, Guid statusId)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        try
        {
            await _votingService.UpdateUserAsync(userId, fullName, roleId, statusId);
            TempData["Message"] = "User updated successfully.";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(Guid userId)
    {
        if (ForbidIfNotAdmin() is IActionResult forbidden) return forbidden;
        try
        {
            await _votingService.DeleteUserAsync(userId);
            TempData["Message"] = "User deleted.";
        }
        catch (Exception ex)
        {
            TempData["Message"] = $"Error: {ex.Message}";
        }
        return RedirectToPage();
    }
}
