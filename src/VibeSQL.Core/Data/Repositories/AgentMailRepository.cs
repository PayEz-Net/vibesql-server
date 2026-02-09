using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VibeSQL.Core.Entities;
using VibeSQL.Core.Interfaces;
using VibeSQL.Core.Data;
using System.Text.Json;

namespace VibeSQL.Core.Data.Repositories;

/// <summary>
/// Repository for agent_mail_messages table operations.
/// Abstracts data access from business logic following Clean Architecture.
/// </summary>
public class AgentMailRepository : IAgentMailRepository
{
    private readonly VibeDbContext _context;
    private readonly ILogger<AgentMailRepository> _logger;

    private const string CollectionName = "agent_mail";
    private const string MessagesTable = "agent_mail_messages";

    public AgentMailRepository(VibeDbContext context, ILogger<AgentMailRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<VibeDocument?> GetMessageAsync(int clientId, int messageId)
    {
        var messages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return messages.FirstOrDefault(d =>
        {
            var data = TryDeserialize<MessageData>(d.Data);
            return data?.Id == messageId;
        });
    }

    public async Task<List<VibeDocument>> GetMessagesByThreadAsync(int clientId, string threadId)
    {
        var messages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        return messages.Where(d =>
        {
            var data = TryDeserialize<MessageData>(d.Data);
            return data?.ThreadId == threadId;
        }).OrderBy(d => d.CreatedAt).ToList();
    }

    public async Task<VibeDocument> CreateMessageAsync(
        int clientId,
        int fromAgentId,
        int fromUserId,
        string threadId,
        string subject,
        string body,
        string bodyFormat,
        string importance)
    {
        var now = DateTimeOffset.UtcNow;

        // Get next ID from sequence
        var nextId = await GetNextMessageIdAsync(clientId);

        var messageData = new
        {
            id = nextId,
            from_agent_id = fromAgentId,
            from_user_id = fromUserId,
            thread_id = threadId,
            subject = subject,
            body = body,
            body_format = bodyFormat,
            importance = importance,
            created_at = now.ToString("o")
        };

        var document = new VibeDocument
        {
            ClientId = clientId,
            OwnerUserId = fromUserId,
            Collection = CollectionName,
            TableName = MessagesTable,
            Data = JsonSerializer.Serialize(messageData),
            CreatedAt = now,
            CreatedBy = fromUserId
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation("AGENT_MAIL_MESSAGE_CREATED: MessageId={MessageId}, FromAgent={FromAgent}, ClientId={ClientId}",
            nextId, fromAgentId, clientId);

        return document;
    }

    private async Task<int> GetNextMessageIdAsync(int clientId)
    {
        var seqName = $"vibe.seq_{clientId}_{CollectionName}_messages";

        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            try
            {
                // Ensure sequence exists
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = $"CREATE SEQUENCE IF NOT EXISTS {seqName} START WITH 1 INCREMENT BY 1";
                await createCmd.ExecuteNonQueryAsync();

                // Get next value
                using var nextCmd = connection.CreateCommand();
                nextCmd.CommandText = $"SELECT nextval('{seqName}')";
                var result = await nextCmd.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get sequence value, falling back to max+1");
            var allMessages = await _context.Documents
                .Where(d => d.ClientId == clientId
                         && d.Collection == CollectionName
                         && d.TableName == MessagesTable
                         && d.DeletedAt == null)
                .ToListAsync();

            var maxId = allMessages
                .Select(d => TryDeserialize<MessageData>(d.Data)?.Id ?? 0)
                .DefaultIfEmpty(0)
                .Max();
            return maxId + 1;
        }
    }

    public async Task<(List<MessageSearchData> Results, int TotalCount)> SearchMessagesAsync(
        int clientId,
        string query,
        string? fromFilter = null,
        string? toFilter = null,
        DateTimeOffset? afterDate = null,
        DateTimeOffset? beforeDate = null,
        string? threadId = null,
        int? mailboxAgentId = null,
        string sortBy = "relevance",
        int limit = 20,
        int offset = 0)
    {
        // Get all messages for this client (in production, use PostgreSQL full-text search)
        var allMessages = await _context.Documents
            .Where(d => d.ClientId == clientId
                     && d.Collection == CollectionName
                     && d.TableName == MessagesTable
                     && d.DeletedAt == null)
            .ToListAsync();

        // Parse and filter messages
        var parsedMessages = allMessages
            .Select(d => new
            {
                Document = d,
                Data = TryDeserialize<FullMessageData>(d.Data)
            })
            .Where(x => x.Data != null)
            .ToList();

        // Apply text search (case-insensitive LIKE equivalent)
        var searchTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var filtered = parsedMessages.Where(x =>
        {
            var subject = x.Data!.Subject?.ToLowerInvariant() ?? "";
            var body = x.Data!.Body?.ToLowerInvariant() ?? "";
            var combined = subject + " " + body;

            // All terms must match (AND semantics)
            return searchTerms.All(term => combined.Contains(term));
        }).ToList();

        // Apply optional filters
        if (!string.IsNullOrEmpty(fromFilter))
        {
            var fromLower = fromFilter.ToLowerInvariant();
            filtered = filtered.Where(x =>
            {
                // For now, match against from_agent_id - in production would join to agents table
                return x.Data!.FromAgentId.ToString().Contains(fromLower);
            }).ToList();
        }

        if (afterDate.HasValue)
        {
            filtered = filtered.Where(x =>
            {
                if (DateTimeOffset.TryParse(x.Data!.CreatedAt, out var created))
                    return created >= afterDate.Value;
                return false;
            }).ToList();
        }

        if (beforeDate.HasValue)
        {
            filtered = filtered.Where(x =>
            {
                if (DateTimeOffset.TryParse(x.Data!.CreatedAt, out var created))
                    return created < beforeDate.Value;
                return false;
            }).ToList();
        }

        if (!string.IsNullOrEmpty(threadId))
        {
            filtered = filtered.Where(x => x.Data!.ThreadId == threadId).ToList();
        }

        // Calculate relevance scores (simple term frequency)
        var scoredResults = filtered.Select(x =>
        {
            var subject = x.Data!.Subject?.ToLowerInvariant() ?? "";
            var body = x.Data!.Body?.ToLowerInvariant() ?? "";

            // Score: subject matches weighted 2x, body matches 1x
            double score = 0;
            foreach (var term in searchTerms)
            {
                var subjectCount = CountOccurrences(subject, term);
                var bodyCount = CountOccurrences(body, term);
                score += (subjectCount * 2.0) + bodyCount;
            }

            // Normalize to 0-1 range (rough approximation)
            var normalizedScore = Math.Min(1.0, score / (searchTerms.Length * 5.0));

            // Generate highlighted subject
            var highlightedSubject = HighlightMatches(x.Data!.Subject ?? "", searchTerms);

            // Generate snippet with highlights
            var snippet = GenerateSnippet(x.Data!.Body ?? "", searchTerms, 200);

            return new MessageSearchData
            {
                MessageId = x.Data!.Id,
                ThreadId = x.Data!.ThreadId,
                FromAgentId = x.Data!.FromAgentId,
                Subject = x.Data!.Subject,
                SubjectHighlighted = highlightedSubject,
                Snippet = snippet,
                CreatedAt = x.Data!.CreatedAt,
                Score = normalizedScore,
                HasAttachments = false // TODO: Check for attachments
            };
        }).ToList();

        // Sort results
        var sortedResults = sortBy switch
        {
            "date_desc" => scoredResults.OrderByDescending(r =>
                DateTimeOffset.TryParse(r.CreatedAt, out var d) ? d : DateTimeOffset.MinValue).ToList(),
            "date_asc" => scoredResults.OrderBy(r =>
                DateTimeOffset.TryParse(r.CreatedAt, out var d) ? d : DateTimeOffset.MaxValue).ToList(),
            _ => scoredResults.OrderByDescending(r => r.Score).ToList() // relevance
        };

        var totalCount = sortedResults.Count;

        // Apply pagination
        var pagedResults = sortedResults
            .Skip(offset)
            .Take(limit)
            .ToList();

        return (pagedResults, totalCount);
    }

    private static int CountOccurrences(string text, string term)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(term))
            return 0;

        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += term.Length;
        }
        return count;
    }

    private static string HighlightMatches(string text, string[] terms)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var result = text;
        foreach (var term in terms)
        {
            result = System.Text.RegularExpressions.Regex.Replace(
                result,
                System.Text.RegularExpressions.Regex.Escape(term),
                match => $"<mark>{match.Value}</mark>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        return result;
    }

    private static string GenerateSnippet(string body, string[] terms, int maxLength)
    {
        if (string.IsNullOrEmpty(body)) return "";

        // Find first occurrence of any search term
        var lowerBody = body.ToLowerInvariant();
        int firstMatchIndex = body.Length;
        foreach (var term in terms)
        {
            var idx = lowerBody.IndexOf(term.ToLowerInvariant());
            if (idx >= 0 && idx < firstMatchIndex)
                firstMatchIndex = idx;
        }

        if (firstMatchIndex == body.Length)
            firstMatchIndex = 0;

        // Calculate snippet window
        int start = Math.Max(0, firstMatchIndex - 50);
        int length = Math.Min(maxLength, body.Length - start);

        var snippet = body.Substring(start, length);

        // Add ellipsis if truncated
        if (start > 0) snippet = "..." + snippet;
        if (start + length < body.Length) snippet = snippet + "...";

        // Highlight matches in snippet
        return HighlightMatches(snippet, terms);
    }

    private static T? TryDeserialize<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private class MessageData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("thread_id")]
        public string? ThreadId { get; set; }
    }

    private class FullMessageData
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public int Id { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("thread_id")]
        public string? ThreadId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("from_agent_id")]
        public int FromAgentId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("from_user_id")]
        public int FromUserId { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("subject")]
        public string? Subject { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("body")]
        public string? Body { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("body_format")]
        public string? BodyFormat { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("importance")]
        public string? Importance { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }
}
