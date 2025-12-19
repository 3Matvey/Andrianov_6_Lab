using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Andrianov_6_Lab.Services;

namespace Andrianov_6_Lab.Pages.Db;

public class QueriesModel : PageModel
{
    private readonly RawSqlExecutor _executor;

    public QueriesModel(RawSqlExecutor executor)
    {
        _executor = executor;
    }

    [BindProperty]
    public string? SelectedScript { get; set; }

    public List<SelectListItem> ScriptFilesSelect { get; set; } = new();

    public List<Dictionary<string, object?>>? LastResults { get; set; }
    public List<string> Columns { get; set; } = new();

    public void OnGet()
    {
        LoadScripts();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        LoadScripts();
        if (!string.IsNullOrWhiteSpace(SelectedScript))
        {
            var sql = _executor.ReadScript(SelectedScript);
            if (!string.IsNullOrWhiteSpace(sql))
            {
                try
                {
                    LastResults = await _executor.ExecuteQueryAsync(sql);
                    if (LastResults.Any()) Columns = LastResults.First().Keys.ToList();
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, ex.Message);
                }
            }
        }
        return Page();
    }

    private void LoadScripts()
    {
        ScriptFilesSelect = _executor.GetScriptFiles().Select(f => new SelectListItem(f, f)).ToList();
    }
}
