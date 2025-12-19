using Andrianov_6_Lab.Models;
using Andrianov_6_Lab.Services;
using Npgsql;

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
        const string sql = "SELECT id, title, description, created_by, start_at, end_at, is_published, visibility, created_at FROM voting.voting_sessions WHERE is_published = TRUE ORDER BY created_at DESC";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(MapToVotingSession).ToList();
    }

    // Get session details with candidates
    public async Task<VotingSession?> GetSessionWithCandidatesAsync(Guid sessionId, bool includeUnpublished = false)
    {
        const string sql = @"
            SELECT vs.id, vs.title, vs.description, vs.created_by, vs.start_at, vs.end_at, vs.is_published, vs.visibility, vs.created_at,
                   s.anonymous, s.multi_select, s.max_choices, s.require_confirmed_email, s.allow_vote_change_until_close,
                   c.id as candidate_id, c.full_name, c.description, ct.code as candidate_type_code
            FROM voting.voting_sessions vs
            LEFT JOIN voting.voting_settings s ON s.session_id = vs.id
            LEFT JOIN voting.candidates c ON c.session_id = vs.id
            LEFT JOIN voting.candidate_types ct ON ct.id = c.candidate_type_id
            WHERE vs.id = @sessionId AND (vs.is_published = TRUE OR @includeUnpublished = TRUE)";

        var results = await _executor.ExecuteQueryAsync(sql,
            new NpgsqlParameter("sessionId", sessionId),
            new NpgsqlParameter("includeUnpublished", includeUnpublished));
        if (!results.Any()) return null;

        var session = MapToVotingSession(results.First());
        session.Settings = MapToVotingSettings(results.First());
        session.Candidates = results.Where(r => r.ContainsKey("candidate_id") && r["candidate_id"] != null)
                                    .Select(MapToCandidate).ToList();

        return session;
    }

    // Cast a vote
    public async Task CastVoteAsync(Guid userId, Guid candidateId, decimal weight = 1)
    {
        var parameters = new[]
        {
            new NpgsqlParameter("p_user_id", userId == Guid.Empty ? DBNull.Value : userId),
            new NpgsqlParameter("p_candidate_id", candidateId),
            new NpgsqlParameter("p_weight", weight)
        };
        await _executor.ExecuteProcedureAsync("voting.sp_cast_vote", parameters);
        await LogActionAsync(userId, "CAST_VOTE", "candidate", candidateId.ToString());
    }

    // Get user by email (for login simulation)
    public async Task<User?> GetUserByEmailAsync(string email)
    {
        const string sql = "SELECT u.id, u.email, u.password_hash, u.full_name, u.role_id, u.status_id, u.created_at, u.updated_at, r.code as role_code FROM voting.users u LEFT JOIN voting.roles r ON r.id = u.role_id WHERE u.email = @email";
        var results = await _executor.ExecuteQueryAsync(sql, new NpgsqlParameter("email", email));
        return results.FirstOrDefault() != null ? MapToUser(results.First()) : null;
    }

    // Get all users (for admin)
    public async Task<List<User>> GetAllUsersAsync()
    {
        const string sql = "SELECT u.id, u.email, u.full_name, u.role_id, u.status_id, u.created_at, u.updated_at, r.name as role_name, s.name as status_name FROM voting.users u JOIN voting.roles r ON r.id = u.role_id JOIN voting.user_statuses s ON s.id = u.status_id ORDER BY u.created_at DESC";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(MapToUserWithDetails).ToList();
    }

    public async Task<List<Role>> GetRolesAsync()
    {
        const string sql = "SELECT id, code, name FROM voting.roles ORDER BY name";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(r => new Role
        {
            Id = Guid.Parse(r["id"]?.ToString() ?? Guid.Empty.ToString()),
            Code = r["code"]?.ToString() ?? string.Empty,
            Name = r["name"]?.ToString() ?? string.Empty
        }).ToList();
    }

    public async Task<List<UserStatus>> GetUserStatusesAsync()
    {
        const string sql = "SELECT id, code, name FROM voting.user_statuses ORDER BY name";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(r => new UserStatus
        {
            Id = Guid.Parse(r["id"]?.ToString() ?? Guid.Empty.ToString()),
            Code = r["code"]?.ToString() ?? string.Empty,
            Name = r["name"]?.ToString() ?? string.Empty
        }).ToList();
    }

    public async Task<List<VotingSession>> GetAllSessionsAsync()
    {
        const string sql = "SELECT id, title, description, created_by, start_at, end_at, is_published, visibility, created_at FROM voting.voting_sessions ORDER BY created_at DESC";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(MapToVotingSession).ToList();
    }

    // Create session
    public async Task CreateSessionAsync(string title, string? description, Guid createdBy, DateTime startAt, DateTime endAt, string visibility = "private", bool anonymous = true, bool multiSelect = false, int maxChoices = 1)
    {
        var parameters = new[]
        {
            new NpgsqlParameter("p_title", title),
            new NpgsqlParameter("p_description", description ?? string.Empty),
            new NpgsqlParameter("p_created_by", createdBy),
            new NpgsqlParameter("p_start_at", startAt),
            new NpgsqlParameter("p_end_at", endAt),
            new NpgsqlParameter("p_visibility", visibility)
        };
        await _executor.ExecuteProcedureAsync("voting.sp_create_session", parameters);
        await LogActionAsync(createdBy, "CREATE_SESSION", "voting_session", null, new { title, startAt, endAt, visibility });

        // apply settings
        var settingsParams = new[]
        {
            new NpgsqlParameter("p_session_id", await GetSessionIdByTitleAsync(title, createdBy, startAt)),
            new NpgsqlParameter("p_anonymous", anonymous),
            new NpgsqlParameter("p_multi_select", multiSelect),
            new NpgsqlParameter("p_max_choices", maxChoices)
        };
        if (settingsParams[0].Value is Guid)
        {
            await _executor.ExecuteProcedureAsync("voting.sp_upsert_settings", settingsParams);
        }
    }

    public async Task UpdateSessionAsync(Guid sessionId, string title, string? description, DateTime startAt, DateTime endAt, string visibility, bool anonymous, bool multiSelect, int maxChoices)
    {
        const string updateSessionSql = @"UPDATE voting.voting_sessions SET title=@title, description=@description, start_at=@startAt, end_at=@endAt, visibility=@visibility WHERE id=@id";
        await _executor.ExecuteNonQueryAsync(updateSessionSql,
            new NpgsqlParameter("title", title),
            new NpgsqlParameter("description", (object?)description ?? DBNull.Value),
            new NpgsqlParameter("startAt", startAt),
            new NpgsqlParameter("endAt", endAt),
            new NpgsqlParameter("visibility", visibility),
            new NpgsqlParameter("id", sessionId));

        var settingsParams = new[]
        {
            new NpgsqlParameter("p_session_id", sessionId),
            new NpgsqlParameter("p_anonymous", anonymous),
            new NpgsqlParameter("p_multi_select", multiSelect),
            new NpgsqlParameter("p_max_choices", maxChoices)
        };
        await _executor.ExecuteProcedureAsync("voting.sp_upsert_settings", settingsParams);
        await LogActionAsync(Guid.Empty, "UPDATE_SESSION", "voting_session", sessionId.ToString(), new { title, startAt, endAt, visibility, anonymous, multiSelect, maxChoices });
    }

    public async Task DeleteSessionAsync(Guid sessionId)
    {
        const string sql = "DELETE FROM voting.voting_sessions WHERE id=@id";
        await _executor.ExecuteNonQueryAsync(sql, new NpgsqlParameter("id", sessionId));
        await LogActionAsync(Guid.Empty, "DELETE_SESSION", "voting_session", sessionId.ToString());
    }

    // Add candidate
    public async Task AddCandidateAsync(Guid sessionId, string candidateTypeCode, string fullName, string? description)
    {
        var parameters = new[]
        {
            new NpgsqlParameter("p_session_id", sessionId),
            new NpgsqlParameter("p_candidate_type_code", candidateTypeCode),
            new NpgsqlParameter("p_full_name", fullName),
            new NpgsqlParameter("p_description", description ?? (object)DBNull.Value)
        };
        await _executor.ExecuteProcedureAsync("voting.sp_add_candidate", parameters);
        await LogActionAsync(Guid.Empty, "ADD_CANDIDATE", "candidate", null, new { sessionId, candidateTypeCode, fullName });
    }

    public async Task UpdateCandidateAsync(Guid candidateId, string candidateTypeCode, string fullName, string? description)
    {
        const string sql = @"UPDATE voting.candidates SET full_name=@fullName, description=@description, candidate_type_id=(SELECT id FROM voting.candidate_types WHERE code=@code LIMIT 1) WHERE id=@id";
        await _executor.ExecuteNonQueryAsync(sql,
            new NpgsqlParameter("fullName", fullName),
            new NpgsqlParameter("description", (object?)description ?? DBNull.Value),
            new NpgsqlParameter("code", candidateTypeCode),
            new NpgsqlParameter("id", candidateId));
        await LogActionAsync(Guid.Empty, "UPDATE_CANDIDATE", "candidate", candidateId.ToString(), new { candidateTypeCode, fullName });
    }

    public async Task DeleteCandidateAsync(Guid candidateId)
    {
        const string sql = "DELETE FROM voting.candidates WHERE id=@id";
        await _executor.ExecuteNonQueryAsync(sql, new NpgsqlParameter("id", candidateId));
        await LogActionAsync(Guid.Empty, "DELETE_CANDIDATE", "candidate", candidateId.ToString());
    }

    // Publish session
    public async Task PublishSessionAsync(Guid sessionId)
    {
        await _executor.ExecuteProcedureAsync("voting.sp_publish_session", new NpgsqlParameter("p_session_id", sessionId));
        await LogActionAsync(Guid.Empty, "PUBLISH_SESSION", "voting_session", sessionId.ToString());
    }

    // Get results
    public async Task<VotingResults?> GetResultsAsync(Guid sessionId)
    {
        const string sql = "SELECT session_id, generated_at, total_votes, payload, signature FROM voting.results WHERE session_id = @sessionId";
        var results = await _executor.ExecuteQueryAsync(sql, new NpgsqlParameter("sessionId", sessionId));
        return results.FirstOrDefault() != null ? MapToVotingResults(results.First()) : null;
    }

    // Get notifications for user
    public async Task<List<Notification>> GetUserNotificationsAsync(Guid userId)
    {
        const string sql = "SELECT id, user_id, type, title, body, is_read, created_at FROM voting.notifications WHERE user_id = @userId ORDER BY created_at DESC";
        var results = await _executor.ExecuteQueryAsync(sql, new NpgsqlParameter("userId", userId));
        return results.Select(MapToNotification).ToList();
    }

    public async Task<List<CandidateType>> GetCandidateTypesAsync()
    {
        const string sql = "SELECT id, code, name FROM voting.candidate_types ORDER BY name";
        var results = await _executor.ExecuteQueryAsync(sql);
        return results.Select(r => new CandidateType
        {
            Id = Guid.Parse(r["id"]?.ToString() ?? Guid.Empty.ToString()),
            Code = r["code"]?.ToString() ?? string.Empty,
            Name = r["name"]?.ToString() ?? string.Empty
        }).ToList();
    }

    public async Task RegisterUserAsync(string email, string passwordHash, string fullName)
    {
        var parameters = new[]
        {
            new NpgsqlParameter("p_email", email),
            new NpgsqlParameter("p_password_hash", passwordHash),
            new NpgsqlParameter("p_full_name", fullName)
        };
        await _executor.ExecuteProcedureAsync("voting.sp_register_user", parameters);
    }

    public async Task UpdateUserAsync(Guid userId, string fullName, Guid roleId, Guid statusId)
    {
        const string sql = "UPDATE voting.users SET full_name=@fullName, role_id=@roleId, status_id=@statusId, updated_at=now() WHERE id=@id";
        await _executor.ExecuteNonQueryAsync(sql,
            new NpgsqlParameter("fullName", fullName),
            new NpgsqlParameter("roleId", roleId),
            new NpgsqlParameter("statusId", statusId),
            new NpgsqlParameter("id", userId));
        await LogActionAsync(Guid.Empty, "UPDATE_USER", "user", userId.ToString(), new { fullName, roleId, statusId });
    }

    public async Task DeleteUserAsync(Guid userId)
    {
        const string sql = "DELETE FROM voting.users WHERE id=@id";
        await _executor.ExecuteNonQueryAsync(sql, new NpgsqlParameter("id", userId));
        await LogActionAsync(Guid.Empty, "DELETE_USER", "user", userId.ToString());
    }

    public async Task MarkNotificationReadAsync(Guid notificationId, Guid userId)
    {
        var parameters = new[]
        {
            new NpgsqlParameter("p_notification_id", notificationId),
            new NpgsqlParameter("p_user_id", userId)
        };
        await _executor.ExecuteProcedureAsync("voting.sp_mark_notification_read", parameters);
    }

    // Execute raw query (for custom queries)
    public async Task<List<Dictionary<string, object?>>> ExecuteRawQueryAsync(string sql)
    {
        return await _executor.ExecuteQueryAsync(sql);
    }

    public async Task<List<Dictionary<string, object?>>> GetVoteCountsAsync(Guid[] candidateIds)
    {
        if (candidateIds.Length == 0) return new List<Dictionary<string, object?>>();
        const string sql = "SELECT candidate_id, COUNT(*) as vote_count FROM voting.votes WHERE candidate_id = ANY(@candidateIds) AND is_valid = TRUE GROUP BY candidate_id";
        return await _executor.ExecuteQueryAsync(sql, new NpgsqlParameter("candidateIds", candidateIds));
    }

    private async Task<Guid?> GetSessionIdByTitleAsync(string title, Guid createdBy, DateTime startAt)
    {
        const string sql = "SELECT id FROM voting.voting_sessions WHERE title=@title AND created_by=@createdBy AND start_at=@startAt ORDER BY created_at DESC LIMIT 1";
        var rows = await _executor.ExecuteQueryAsync(sql,
            new NpgsqlParameter("title", title),
            new NpgsqlParameter("createdBy", createdBy),
            new NpgsqlParameter("startAt", startAt));
        if (rows.FirstOrDefault() is { } row && Guid.TryParse(row["id"]?.ToString(), out var id))
        {
            return id;
        }
        return null;
    }

    private async Task LogActionAsync(Guid userId, string action, string? entityType = null, string? entityId = null, object? meta = null)
    {
        var parameters = new[]
        {
            new NpgsqlParameter("p_user_id", userId == Guid.Empty ? DBNull.Value : userId),
            new NpgsqlParameter("p_action", action),
            new NpgsqlParameter("p_entity_type", (object?)entityType ?? DBNull.Value),
            new NpgsqlParameter("p_entity_id", (object?)entityId ?? DBNull.Value),
            new NpgsqlParameter("p_meta", NpgsqlTypes.NpgsqlDbType.Jsonb)
            {
                Value = meta == null
                    ? DBNull.Value
                    : System.Text.Json.JsonSerializer.Serialize(meta)
            }
        };
        await _executor.ExecuteProcedureAsync("voting.sp_log_action", parameters);
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