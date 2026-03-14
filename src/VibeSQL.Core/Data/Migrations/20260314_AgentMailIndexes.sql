-- =====================================================================================
-- Agent Mail Performance Indexes
-- =====================================================================================
-- Purpose: Add targeted indexes for agent mail inbox and message lookups
-- Date: 2026-03-14
-- Author: Aurum
-- Database: vibe
-- Schema: vibe
-- =====================================================================================

-- Index 1: Inbox lookups by agent (covers GetInboxEntriesAsync, GetUnreadCountAsync)
CREATE INDEX IF NOT EXISTS idx_agent_mail_inbox_by_agent
ON vibe.documents (client_id, ((data->>'agent_id')::int), created_at DESC)
WHERE collection = 'agent_mail'
  AND table_name = 'agent_mail_inbox'
  AND deleted_at IS NULL;

-- Index 2: Message lookups by ID (covers GetMessageAsync, the join path)
CREATE INDEX IF NOT EXISTS idx_agent_mail_messages_by_id
ON vibe.documents (client_id, ((data->>'id')::int))
WHERE collection = 'agent_mail'
  AND table_name = 'agent_mail_messages'
  AND deleted_at IS NULL;

-- Index 3: Inbox lookup by message_id (covers GetInboxEntryByMessageIdAsync, MarkMessageAsReadAsync)
CREATE INDEX IF NOT EXISTS idx_agent_mail_inbox_by_message
ON vibe.documents (client_id, ((data->>'agent_id')::int), ((data->>'message_id')::int))
WHERE collection = 'agent_mail'
  AND table_name = 'agent_mail_inbox'
  AND deleted_at IS NULL;
