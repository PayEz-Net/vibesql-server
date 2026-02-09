-- Migration: Add read receipt privacy setting to vibe_agents
-- Date: 2026-01-27
-- Author: MarcuVale (Launch Czar)

-- Add send_read_receipts column to vibe_agents
-- Default TRUE: agents send read receipts unless they opt out

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('vibe_agents') AND name = 'send_read_receipts'
)
BEGIN
    ALTER TABLE vibe_agents 
    ADD send_read_receipts BIT NOT NULL DEFAULT 1;
    
    PRINT 'Added send_read_receipts column to vibe_agents';
END
ELSE
BEGIN
    PRINT 'Column send_read_receipts already exists';
END
GO
