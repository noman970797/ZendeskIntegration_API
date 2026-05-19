-- ============================================================
-- ZendeskIntegration - Migration 002
-- Adds AttachmentLogs and WebhookEvents tables.
-- Run AFTER 001_InitialSchema.sql
-- ============================================================

USE ZendeskIntegration;
GO

-- ============================================================
-- AttachmentLogs
-- Tracks every file uploaded to Zendesk via the Upload API.
-- The upload token is passed into ticket creation requests.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttachmentLogs')
BEGIN
    CREATE TABLE dbo.AttachmentLogs (
        Id              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FileName        NVARCHAR(500)     NOT NULL,
        ContentType     NVARCHAR(200)     NOT NULL,
        FileSizeBytes   BIGINT            NOT NULL,
        UploadToken     NVARCHAR(500)     NOT NULL,
        RelatedTicketId INT               NULL
            CONSTRAINT FK_AttachmentLogs_Tickets FOREIGN KEY REFERENCES dbo.SupportTickets(Id),
        TokenUsed       BIT               NOT NULL CONSTRAINT DF_Att_Used DEFAULT 0,
        UploadedBy      NVARCHAR(200)     NULL,
        CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_Att_CreatedAt DEFAULT GETUTCDATE()
    );

    CREATE NONCLUSTERED INDEX IX_AttachmentLogs_UploadToken
        ON dbo.AttachmentLogs (UploadToken);

    CREATE NONCLUSTERED INDEX IX_AttachmentLogs_RelatedTicketId
        ON dbo.AttachmentLogs (RelatedTicketId);

    CREATE NONCLUSTERED INDEX IX_AttachmentLogs_TokenUsed
        ON dbo.AttachmentLogs (TokenUsed) INCLUDE (FileName, CreatedAt);

    PRINT 'Table AttachmentLogs created.';
END
GO

-- ============================================================
-- WebhookEvents
-- Persists every inbound Zendesk webhook notification for
-- audit, replay, and local ticket synchronization.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'WebhookEvents')
BEGIN
    CREATE TABLE dbo.WebhookEvents (
        Id                   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ZendeskTicketId      BIGINT            NULL,
        EventType            NVARCHAR(100)     NOT NULL,
        TicketStatus         NVARCHAR(50)      NULL,
        TicketPriority       NVARCHAR(20)      NULL,
        AssigneeEmail        NVARCHAR(320)     NULL,
        LatestCommentAuthor  NVARCHAR(200)     NULL,
        RawPayload           NVARCHAR(MAX)     NOT NULL,
        ProcessedSuccessfully BIT              NOT NULL CONSTRAINT DF_Wh_Processed DEFAULT 0,
        ProcessingError      NVARCHAR(2000)    NULL,
        SourceIpAddress      NVARCHAR(45)      NULL,
        LocalTicketId        INT               NULL
            CONSTRAINT FK_WebhookEvents_Tickets FOREIGN KEY REFERENCES dbo.SupportTickets(Id),
        ReceivedAt           DATETIME2         NOT NULL CONSTRAINT DF_Wh_ReceivedAt DEFAULT GETUTCDATE()
    );

    CREATE NONCLUSTERED INDEX IX_WebhookEvents_ZendeskTicketId
        ON dbo.WebhookEvents (ZendeskTicketId);

    CREATE NONCLUSTERED INDEX IX_WebhookEvents_EventType
        ON dbo.WebhookEvents (EventType) INCLUDE (ZendeskTicketId, ReceivedAt);

    CREATE NONCLUSTERED INDEX IX_WebhookEvents_ProcessedSuccessfully
        ON dbo.WebhookEvents (ProcessedSuccessfully) INCLUDE (EventType, ZendeskTicketId, ReceivedAt);

    CREATE NONCLUSTERED INDEX IX_WebhookEvents_ReceivedAt
        ON dbo.WebhookEvents (ReceivedAt DESC);

    PRINT 'Table WebhookEvents created.';
END
GO

-- ============================================================
-- Useful diagnostic queries
-- ============================================================

-- Unused attachment tokens (uploaded but never attached to a ticket):
-- SELECT Id, FileName, ContentType, FileSizeBytes, CreatedAt
-- FROM dbo.AttachmentLogs WHERE TokenUsed = 0 ORDER BY CreatedAt DESC;

-- Unprocessed webhook events for retry:
-- SELECT Id, ZendeskTicketId, EventType, ProcessingError, ReceivedAt
-- FROM dbo.WebhookEvents WHERE ProcessedSuccessfully = 0 ORDER BY ReceivedAt DESC;

-- Webhook event history for a specific Zendesk ticket:
-- SELECT EventType, TicketStatus, LatestCommentAuthor, ReceivedAt
-- FROM dbo.WebhookEvents WHERE ZendeskTicketId = 12345 ORDER BY ReceivedAt DESC;

PRINT 'Migration 002 complete.';
GO
