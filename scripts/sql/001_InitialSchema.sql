-- ============================================================
-- ZendeskIntegration Database Setup
-- SQL Server 2019+ / Azure SQL
-- Run this script on a new database, or use EF Core migrations.
-- ============================================================

USE master;
GO

-- Create database if it doesn't exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'ZendeskIntegration')
BEGIN
    CREATE DATABASE ZendeskIntegration
        COLLATE SQL_Latin1_General_CP1_CI_AS;
    PRINT 'Database ZendeskIntegration created.';
END
GO

USE ZendeskIntegration;
GO

-- ============================================================
-- SupportTickets
-- Stores every ticket submitted, with Zendesk sync status.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SupportTickets')
BEGIN
    CREATE TABLE dbo.SupportTickets (
        Id                  INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Subject             NVARCHAR(500)     NOT NULL,
        [Description]       NVARCHAR(MAX)     NOT NULL,
        Tags                NVARCHAR(2000)    NULL,           -- JSON array
        RequesterName       NVARCHAR(200)     NULL,
        RequesterEmail      NVARCHAR(320)     NULL,
        Priority            NVARCHAR(20)      NOT NULL CONSTRAINT DF_Tickets_Priority DEFAULT 'normal',
        [Type]              NVARCHAR(20)      NOT NULL CONSTRAINT DF_Tickets_Type    DEFAULT 'problem',
        [Status]            NVARCHAR(50)      NOT NULL CONSTRAINT DF_Tickets_Status  DEFAULT 'pending',

        -- Zendesk sync fields
        ZendeskTicketId     BIGINT            NULL,
        ZendeskTicketUrl    NVARCHAR(500)     NULL,
        SyncedToZendesk     BIT               NOT NULL CONSTRAINT DF_Tickets_Synced  DEFAULT 0,
        ZendeskCreatedAt    DATETIME2         NULL,
        ZendeskRawResponse  NVARCHAR(MAX)     NULL,

        -- Audit
        CreatedBy           NVARCHAR(200)     NULL,
        CreatedAt           DATETIME2         NOT NULL CONSTRAINT DF_Tickets_CreatedAt DEFAULT GETUTCDATE(),
        UpdatedAt           DATETIME2         NOT NULL CONSTRAINT DF_Tickets_UpdatedAt DEFAULT GETUTCDATE()
    );

    CREATE NONCLUSTERED INDEX IX_SupportTickets_ZendeskTicketId
        ON dbo.SupportTickets (ZendeskTicketId);

    CREATE NONCLUSTERED INDEX IX_SupportTickets_CreatedAt
        ON dbo.SupportTickets (CreatedAt DESC);

    CREATE NONCLUSTERED INDEX IX_SupportTickets_SyncedToZendesk
        ON dbo.SupportTickets (SyncedToZendesk) INCLUDE (Id, Subject, Status);

    PRINT 'Table SupportTickets created.';
END
GO

-- ============================================================
-- JwtTokenLogs
-- Audit trail for every JWT generated. Stores hash, not raw token.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'JwtTokenLogs')
BEGIN
    CREATE TABLE dbo.JwtTokenLogs (
        Id              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ExternalUserId  NVARCHAR(200)     NOT NULL,
        UserName        NVARCHAR(200)     NOT NULL,
        UserEmail       NVARCHAR(320)     NOT NULL,
        Algorithm       NVARCHAR(10)      NOT NULL CONSTRAINT DF_JwtLogs_Algo DEFAULT 'HS256',
        IssuedAt        DATETIME2         NOT NULL,
        ExpiresAt       DATETIME2         NOT NULL,
        TokenHash       NVARCHAR(64)      NOT NULL,  -- SHA-256 hex
        IpAddress       NVARCHAR(45)      NULL,      -- IPv6 max
        UserAgent       NVARCHAR(500)     NULL,
        CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_JwtLogs_CreatedAt DEFAULT GETUTCDATE()
    );

    CREATE NONCLUSTERED INDEX IX_JwtTokenLogs_ExternalUserId
        ON dbo.JwtTokenLogs (ExternalUserId);

    CREATE NONCLUSTERED INDEX IX_JwtTokenLogs_TokenHash
        ON dbo.JwtTokenLogs (TokenHash);

    CREATE NONCLUSTERED INDEX IX_JwtTokenLogs_CreatedAt
        ON dbo.JwtTokenLogs (CreatedAt DESC);

    PRINT 'Table JwtTokenLogs created.';
END
GO

-- ============================================================
-- ZendeskApiLogs
-- Records every HTTP call made to the Zendesk REST API.
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ZendeskApiLogs')
BEGIN
    CREATE TABLE dbo.ZendeskApiLogs (
        Id              INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Operation       NVARCHAR(100)     NOT NULL,
        HttpMethod      NVARCHAR(10)      NOT NULL,
        Endpoint        NVARCHAR(500)     NOT NULL,
        HttpStatusCode  INT               NULL,
        Success         BIT               NOT NULL,
        RequestBody     NVARCHAR(MAX)     NULL,
        ResponseBody    NVARCHAR(MAX)     NULL,
        ErrorMessage    NVARCHAR(2000)    NULL,
        DurationMs      BIGINT            NOT NULL,
        RelatedTicketId INT               NULL
            CONSTRAINT FK_ApiLogs_Tickets FOREIGN KEY REFERENCES dbo.SupportTickets(Id),
        CreatedAt       DATETIME2         NOT NULL CONSTRAINT DF_ApiLogs_CreatedAt DEFAULT GETUTCDATE()
    );

    CREATE NONCLUSTERED INDEX IX_ZendeskApiLogs_Operation
        ON dbo.ZendeskApiLogs (Operation);

    CREATE NONCLUSTERED INDEX IX_ZendeskApiLogs_Success
        ON dbo.ZendeskApiLogs (Success) INCLUDE (Operation, DurationMs, CreatedAt);

    CREATE NONCLUSTERED INDEX IX_ZendeskApiLogs_RelatedTicketId
        ON dbo.ZendeskApiLogs (RelatedTicketId);

    CREATE NONCLUSTERED INDEX IX_ZendeskApiLogs_CreatedAt
        ON dbo.ZendeskApiLogs (CreatedAt DESC);

    PRINT 'Table ZendeskApiLogs created.';
END
GO

-- ============================================================
-- EF Core __EFMigrationsHistory (required for EF compatibility)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '__EFMigrationsHistory')
BEGIN
    CREATE TABLE dbo.__EFMigrationsHistory (
        MigrationId    NVARCHAR(150) NOT NULL PRIMARY KEY,
        ProductVersion NVARCHAR(32)  NOT NULL
    );
    PRINT 'Table __EFMigrationsHistory created.';
END
GO

-- ============================================================
-- Useful diagnostic queries
-- ============================================================

-- View all tickets not yet synced to Zendesk:
-- SELECT Id, Subject, CreatedAt FROM dbo.SupportTickets WHERE SyncedToZendesk = 0 ORDER BY CreatedAt DESC;

-- View JWT audit trail for a specific user:
-- SELECT * FROM dbo.JwtTokenLogs WHERE ExternalUserId = 'user-001' ORDER BY CreatedAt DESC;

-- View failed Zendesk API calls:
-- SELECT * FROM dbo.ZendeskApiLogs WHERE Success = 0 ORDER BY CreatedAt DESC;

-- Average Zendesk API response time:
-- SELECT Operation, AVG(DurationMs) AS AvgMs, COUNT(*) AS Calls FROM dbo.ZendeskApiLogs GROUP BY Operation;

PRINT 'ZendeskIntegration schema setup complete.';
GO
