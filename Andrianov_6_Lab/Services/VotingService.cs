using Andrianov_6_Lab.Models;
using Andrianov_6_Lab.Services;

namespace Andrianov_6_Lab.Services;

public class VotingService
{
    private readonly RawSqlExecutor _executor;

    public VotingService(RawSqlExecutor executor)
    {
        _executor = executor;
    }

    // Get all published voting sessions
    public async Task<List<VotingSession>> GetPublishedSessionsAsync()
    {
        var sql = "SELECT id, title, description, created_by, start_at, end_at, is_published, visibility, created_at FROM voting.voting_sessions WHERE is_published = TRUE ORDER BY created_at DESC";
        try
        {
            var results = await _executor.ExecuteQueryAsync(sql);
            return results.Select(MapToVotingSession).ToList();
        }
        catch
        {
            return new List<VotingSession>();
        }
    }

    // Get session details with candidates
    public async Task<VotingSession?> GetSessionWithCandidatesAsync(Guid sessionId)
    {
        var sql = @"
            SELECT vs.id, vs.title, vs.description, vs.created_by, vs.start_at, vs.end_at, vs.is_published, vs.visibility, vs.created_at,
                   s.anonymous, s.multi_select, s.max_choices, s.require_confirmed_email, s.allow_vote_change_until_close,
                   c.id as candidate_id, c.full_name, c.description, ct.code as candidate_type_code
            FROM voting.voting_sessions vs
            LEFT JOIN voting.voting_settings s ON s.session_id = vs.id
            LEFT JOIN voting.candidates c ON c.session_id = vs.id
            LEFT JOIN voting.candidate_types ct ON ct.id = c.candidate_type_id
            WHERE vs.id = @sessionId AND vs.is_published = TRUE";

        var results = await _executor.ExecuteQueryAsync(sql.Replace("@sessionId", $"'{sessionId}'"));
        try
        {
            if (!results.Any()) return null;

            var session = MapToVotingSession(results.First());
            session.Settings = MapToVotingSettings(results.First());
            session.Candidates = results.Where(r => r.ContainsKey("candidate_id") && r["candidate_id"] != null)
                                        .Select(MapToCandidate).ToList();

            return session;
        }
        catch
        {
            return null;
        }
    }

    // Cast a vote
    public async Task CastVoteAsync(Guid userId, Guid candidateId, decimal weight = 1)
    {
        var sql = $"CALL voting.sp_cast_vote('{userId}', '{candidateId}', {weight})";
        await _executor.ExecuteQueryAsync(sql);
    }

    // Get user by email (for login simulation)
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        var sql = $"SELECT u.id, u.email, u.password_hash, u.full_name, u.role_id, u.status_id, u.created_at, u.updated_at, r.code as role_code FROM voting.users u LEFT JOIN voting.roles r ON r.id = u.role_id WHERE u.email = '{email}'";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.FirstOrDefault() != null ? MapToUser(results.First()) : null;
    }

    // Get all users (for admin)
    public async Task<List<User>> GetAllUsersAsync()
    {
        var sql = "SELECT u.id, u.email, u.full_name, u.role_id, u.status_id, u.created_at, u.updated_at, r.name as role_name, s.name as status_name FROM voting.users u JOIN voting.roles r ON r.id = u.role_id JOIN voting.user_statuses s ON s.id = u.status_id ORDER BY u.created_at DESC";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(MapToUserWithDetails).ToList();
    }

    // Create session
    public async Task CreateSessionAsync(string title, string? description, Guid createdBy, DateTime startAt, DateTime endAt, string visibility = "private")
    {
        var sql = $"CALL voting.sp_create_session('{title.Replace("'", "''")}', '{description?.Replace("'", "''") ?? ""}', '{createdBy}', '{startAt:yyyy-MM-dd HH:mm:ss}', '{endAt:yyyy-MM-dd HH:mm:ss}', '{visibility}')";
        await _executor.ExecuteQueryAsync(sql);
    }

    // Add candidate
    public async Task AddCandidateAsync(Guid sessionId, string candidateTypeCode, string fullName, string? description)
    {
        var sql = $"CALL voting.sp_add_candidate('{sessionId}', '{candidateTypeCode}', '{fullName.Replace("'", "''")}', '{description?.Replace("'", "''") ?? ""}')";
        await _executor.ExecuteQueryAsync(sql);
    }

    // Publish session
    public async Task PublishSessionAsync(Guid sessionId)
    {
        var sql = $"CALL voting.sp_publish_session('{sessionId}')";
        await _executor.ExecuteQueryAsync(sql);
    }

    // Get results
    public async Task<VotingResults?> GetResultsAsync(Guid sessionId)
    {
        var sql = $"SELECT session_id, generated_at, total_votes, payload, signature FROM voting.results WHERE session_id = '{sessionId}'";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.FirstOrDefault() != null ? MapToVotingResults(results.First()) : null;
    }

    // Get notifications for user
    public async Task<List<Notification>> GetUserNotificationsAsync(Guid userId)
    {
        var sql = $"SELECT id, user_id, type, title, body, is_read, created_at FROM voting.notifications WHERE user_id = '{userId}' ORDER BY created_at DESC";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(MapToNotification).ToList();
    }

    // Execute raw query (for custom queries)
    public async Task<List<Dictionary<string, object?>>> ExecuteRawQueryAsync(string sql)
    {
        return await _executor.ExecuteQueryAsync(sql);
    }

    // Helper methods to map database results to models
    private VotingSession MapToVotingSession(Dictionary<string, object?> row)
    {
        return new VotingSession
        {
            Id = Guid.Parse(row["id"]?.ToString() ?? ""),
            Title = row["title"]?.ToString() ?? "",
            Description = row["description"]?.ToString(),
            CreatedBy = Guid.Parse(row["created_by"]?.ToString() ?? ""),
            StartAt = DateTime.Parse(row["start_at"]?.ToString() ?? ""),
            EndAt = DateTime.Parse(row["end_at"]?.ToString() ?? ""),
            IsPublished = bool.Parse(row["is_published"]?.ToString() ?? "false"),
            Visibility = row["visibility"]?.ToString() ?? "private",
            CreatedAt = DateTime.Parse(row["created_at"]?.ToString() ?? "")
        };
    }

    private VotingSettings MapToVotingSettings(Dictionary<string, object?> row)
    {
        return new VotingSettings
        {
            SessionId = Guid.Parse(row["id"]?.ToString() ?? ""),
            Anonymous = bool.Parse(row["anonymous"]?.ToString() ?? "true"),
            MultiSelect = bool.Parse(row["multi_select"]?.ToString() ?? "false"),
            MaxChoices = int.Parse(row["max_choices"]?.ToString() ?? "1"),
            RequireConfirmedEmail = bool.Parse(row["require_confirmed_email"]?.ToString() ?? "false"),
            AllowVoteChangeUntilClose = bool.Parse(row["allow_vote_change_until_close"]?.ToString() ?? "false")
        };
    }

    private Candidate MapToCandidate(Dictionary<string, object?> row)
    {
        return new Candidate
        {
            Id = Guid.Parse(row["candidate_id"]?.ToString() ?? ""),
            SessionId = Guid.Parse(row["id"]?.ToString() ?? ""),
            FullName = row["full_name"]?.ToString() ?? "",
            Description = row["description"]?.ToString(),
            Type = new CandidateType { Code = row["candidate_type_code"]?.ToString() ?? "" }
        };
    }

    private User MapToUser(Dictionary<string, object?> row)
    {
        return new User
        {
            Id = Guid.Parse(row["id"]?.ToString() ?? ""),
            Email = row["email"]?.ToString() ?? "",
            PasswordHash = row["password_hash"]?.ToString() ?? "",
            FullName = row["full_name"]?.ToString() ?? "",
            RoleId = Guid.Parse(row["role_id"]?.ToString() ?? ""),
            StatusId = Guid.Parse(row["status_id"]?.ToString() ?? ""),
            RoleCode = row.ContainsKey("role_code") ? (row["role_code"]?.ToString() ?? "") : string.Empty,
            CreatedAt = DateTime.Parse(row["created_at"]?.ToString() ?? ""),
            UpdatedAt = DateTime.Parse(row["updated_at"]?.ToString() ?? "")
        };
    }

    private User MapToUserWithDetails(Dictionary<string, object?> row)
    {
        var user = MapToUser(row);
        user.Role = new Role { Name = row["role_name"]?.ToString() ?? "" };
        user.Status = new UserStatus { Name = row["status_name"]?.ToString() ?? "" };
        return user;
    }

    private VotingResults MapToVotingResults(Dictionary<string, object?> row)
    {
        return new VotingResults
        {
            SessionId = Guid.Parse(row["session_id"]?.ToString() ?? ""),
            GeneratedAt = DateTime.Parse(row["generated_at"]?.ToString() ?? ""),
            TotalVotes = int.Parse(row["total_votes"]?.ToString() ?? "0"),
            Payload = row["payload"]?.ToString() ?? "[]",
            Signature = row["signature"]?.ToString()
        };
    }

    private Notification MapToNotification(Dictionary<string, object?> row)
    {
        return new Notification
        {
            Id = Guid.Parse(row["id"]?.ToString() ?? ""),
            UserId = Guid.Parse(row["user_id"]?.ToString() ?? ""),
            Type = row["type"]?.ToString() ?? "",
            Title = row["title"]?.ToString() ?? "",
            Body = row["body"]?.ToString(),
            IsRead = bool.Parse(row["is_read"]?.ToString() ?? "false"),
            CreatedAt = DateTime.Parse(row["created_at"]?.ToString() ?? "")
        };
    }
}

public static class Extensions
{
    public static T Let<T>(this T obj, Func<T, T> func) => func(obj);
}