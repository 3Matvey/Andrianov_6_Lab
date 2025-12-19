namespace Andrianov_6_Lab.Models;

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public Guid StatusId { get; set; }
    public string RoleCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Role? Role { get; set; }
    public UserStatus? Status { get; set; }
}

public class Role
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class UserStatus
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class VotingSession
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public bool IsPublished { get; set; }
    public string Visibility { get; set; } = "private";
    public DateTime CreatedAt { get; set; }
    public User? Creator { get; set; }
    public VotingSettings? Settings { get; set; }
    public List<Candidate> Candidates { get; set; } = new();
    public VotingResults? Results { get; set; }
}

public class VotingSettings
{
    public Guid SessionId { get; set; }
    public bool Anonymous { get; set; } = true;
    public bool MultiSelect { get; set; } = false;
    public int MaxChoices { get; set; } = 1;
    public bool RequireConfirmedEmail { get; set; } = false;
    public bool AllowVoteChangeUntilClose { get; set; } = false;
}

public class Candidate
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid CandidateTypeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CandidateType? Type { get; set; }
    public int VoteCount { get; set; }
}

public class CandidateType
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class Vote
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Guid? UserId { get; set; }
    public DateTime CastAt { get; set; }
    public decimal Weight { get; set; } = 1;
    public bool IsValid { get; set; } = true;
    public Candidate? Candidate { get; set; }
    public User? User { get; set; }
}

public class VotingResults
{
    public Guid SessionId { get; set; }
    public DateTime GeneratedAt { get; set; }
    public int TotalVotes { get; set; }
    public string Payload { get; set; } = "[]";
    public string? Signature { get; set; }
}

public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; }
    public User? User { get; set; }
}

public class UserLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Meta { get; set; }
    public DateTime CreatedAt { get; set; }
    public User? User { get; set; }
}