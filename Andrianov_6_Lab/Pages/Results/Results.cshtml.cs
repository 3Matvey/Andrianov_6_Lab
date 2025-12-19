using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Andrianov_6_Lab.Services;
using Andrianov_6_Lab.Models;

namespace Andrianov_6_Lab.Pages.Results;

public class ResultsModel : PageModel
{
    private readonly VotingService _votingService;

    public ResultsModel(VotingService votingService)
    {
        _votingService = votingService;
    }

    public VotingSession? Session { get; set; }
    public VotingResults? Results { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        Session = await _votingService.GetSessionWithCandidatesAsync(id);
        if (Session == null)
        {
            return NotFound();
        }

        Results = await _votingService.GetResultsAsync(id);

        // Calculate vote counts for each candidate
        if (Session.Candidates.Any())
        {
            var idsArray = Session.Candidates.Select(c => c.Id).ToArray();
            var voteResults = await _votingService.GetVoteCountsAsync(idsArray);

            foreach (var candidate in Session.Candidates)
            {
                var voteRow = voteResults.FirstOrDefault(r => Guid.Parse(r["candidate_id"]?.ToString() ?? "") == candidate.Id);
                candidate.VoteCount = voteRow != null ? int.Parse(voteRow["vote_count"]?.ToString() ?? "0") : 0;
            }
        }

        return Page();
    }
}