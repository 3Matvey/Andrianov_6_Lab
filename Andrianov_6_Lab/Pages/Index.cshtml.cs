using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Andrianov_6_Lab.Services;
using Andrianov_6_Lab.Models;

namespace Andrianov_6_Lab.Pages
{
    public class IndexModel : PageModel
    {
        private readonly VotingService _votingService;

        public IndexModel(VotingService votingService)
        {
            _votingService = votingService;
        }

        public List<VotingSession> Sessions { get; set; } = new();

        public async Task OnGetAsync()
        {
            Sessions = await _votingService.GetPublishedSessionsAsync();
        }
    }
}
