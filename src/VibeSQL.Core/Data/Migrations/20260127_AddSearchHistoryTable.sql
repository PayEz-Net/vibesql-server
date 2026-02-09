-- Migration: Add search history table for agent mail
-- Date: 2026-01-27
-- Author: MarcuVale (Launch Czar)

-- Search history table for suggestions and autocomplete
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'agent_search_history')
BEGIN
    CREATE TABLE agent_search_history (
        id BIGINT IDENTITY(1,1) PRIMARY KEY,
        agent_id INT NOT NULL,
        query VARCHAR(500) NOT NULL,
        result_count INT NOT NULL,
        searched_at DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        
        CONSTRAINT FK_search_history_agent FOREIGN KEY (agent_id) 
            REFERENCES vibe_agents(id) ON DELETE CASCADE
    );
    
    -- Index for agent lookups
    CREATE INDEX IX_search_history_agent ON agent_search_history(agent_id);
    
    -- Index for date-based queries
    CREATE INDEX IX_search_history_date ON agent_search_history(searched_at DESC);
    
    -- Index for query pattern matching
    CREATE INDEX IX_search_history_query ON agent_search_history(query);
    
    PRINT 'Created agent_search_history table';
END
ELSE
BEGIN
    PRINT 'Table agent_search_history already exists';
END
GO

-- Optional: Full-text catalog for messages (if not already exists)
-- Uncomment if full-text search is desired over LIKE-based search

/*
IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'AgentMailCatalog')
BEGIN
    CREATE FULLTEXT CATALOG AgentMailCatalog AS DEFAULT;
    PRINT 'Created full-text catalog AgentMailCatalog';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.fulltext_indexes 
    WHERE object_id = OBJECT_ID('agent_mail_messages')
)
BEGIN
    CREATE FULLTEXT INDEX ON agent_mail_messages (
        subject LANGUAGE 1033,
        body LANGUAGE 1033
    ) KEY INDEX PK_agent_mail_messages
    ON AgentMailCatalog
    WITH CHANGE_TRACKING AUTO;
    
    PRINT 'Created full-text index on agent_mail_messages';
END
GO
*/
