# Zendesk Backend API Integration

A production-ready C# (.NET 8) backend for Zendesk JWT Authentication and Ticket Creation, with full SQL Server persistence and audit logging.

---

## Architecture

```
ZendeskIntegration/
├── src/
│   ├── ZendeskIntegration.Core/           # Domain models, interfaces, DTOs
│   │   ├── DTOs/                          # Request/response contracts
│   │   ├── Interfaces/                    # IJwtService, IZendeskTicketService, repositories
│   │   └── Models/                        # Entity models + ZendeskOptions
│   │
│   ├── ZendeskIntegration.Infrastructure/ # EF Core, services, repositories
│   │   ├── Data/
│   │   │   ├── ZendeskDbContext.cs        # SQL Server EF Core context
│   │   │   └── Repositories/             # TicketRepository, JwtTokenLogRepository, etc.
│   │   ├── Services/
│   │   │   ├── JwtService.cs             # HS256/RS256 token generation + SQL audit log
│   │   │   └── ZendeskTicketService.cs   # Zendesk REST API + SQL persistence
│   │   └── ServiceCollectionExtensions.cs # DI registration helper
│   │
│   └── ZendeskIntegration.API/           # ASP.NET Core Web API
│       ├── Controllers/
│       │   ├── AuthController.cs          # POST /api/auth/token
│       │   └── TicketsController.cs       # POST /api/tickets, GET /api/tickets
│       ├── Program.cs
│       └── appsettings.json
│
├── scripts/sql/
│   └── 001_InitialSchema.sql             # Manual SQL Server schema (alternative to EF migrations)
│
└── postman/
    ├── ZendeskIntegration.postman_collection.json
    └── ZendeskIntegration.postman_environment.json
```

### SQL Server Tables

| Table | Purpose |
|---|---|
| `SupportTickets` | Every ticket submitted, with Zendesk sync status and raw response |
| `JwtTokenLogs` | Audit trail for every JWT generated (stores SHA-256 hash, never raw token) |
| `ZendeskApiLogs` | Every HTTP call to Zendesk — status code, duration, request/response body |

---

## Prerequisites

- .NET 8 SDK
- SQL Server 2019+ (or SQL Server Express / Azure SQL)
- A Zendesk account with:
  - API token (Admin > Apps and Integrations > Zendesk API)
  - Messaging JWT credentials (Admin > Channels > Messaging > Integration)

---

## Setup

### 1. Clone and restore

```bash
git clone https://github.com/your-org/zendesk-integration.git
cd zendesk-integration
dotnet restore
```

### 2. Configure SQL Server

Copy the example config and fill in your values:

```bash
cp src/ZendeskIntegration.API/appsettings.example.json \
   src/ZendeskIntegration.API/appsettings.Development.json
```

Edit `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ZendeskIntegration;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Zendesk": {
    "Subdomain": "mycompany",
    "AgentEmail": "agent@mycompany.com",
    "ApiToken": "your-zendesk-api-token",
    "JwtSecret": "your-32-char-minimum-secret-here",
    "JwtKeyId": "your-key-id-from-zendesk",
    "JwtAlgorithm": "HS256",
    "JwtExpiryMinutes": 60
  }
}
```

**Using environment variables instead (recommended for production):**

```bash
export ConnectionStrings__DefaultConnection="Server=...;Database=ZendeskIntegration;..."
export Zendesk__Subdomain="mycompany"
export Zendesk__AgentEmail="agent@mycompany.com"
export Zendesk__ApiToken="your-api-token"
export Zendesk__JwtSecret="your-secret"
export Zendesk__JwtKeyId="your-key-id"
```

### 3. Create the database schema

**Option A — EF Core migrations (recommended):**

```bash
cd src/ZendeskIntegration.API

# Install EF Core tools if not already installed
dotnet tool install --global dotnet-ef

# Apply migrations (auto-runs in Development mode on startup)
dotnet ef database update --project ../ZendeskIntegration.Infrastructure
```

**Option B — Manual SQL script:**

```bash
sqlcmd -S localhost -i scripts/sql/001_InitialSchema.sql
```

### 4. Run the API

```bash
cd src/ZendeskIntegration.API
dotnet run
```

Swagger UI opens at `https://localhost:7001` (or `http://localhost:5001`).

---

## API Reference

### POST /api/auth/token

Generates a Zendesk Messaging JWT token. An audit entry is persisted to `JwtTokenLogs`.

**Request:**
```json
{
  "externalUserId": "user-001",
  "name": "Jane Smith",
  "email": "jane@example.com"
}
```

**Response (200 OK):**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "issuedAt": "2025-01-15T10:00:00Z",
  "expiresAt": "2025-01-15T11:00:00Z",
  "algorithm": "HS256"
}
```

### POST /api/tickets

Creates a ticket in Zendesk and persists full details to `SupportTickets` and `ZendeskApiLogs`.

**Request:**
```json
{
  "subject": "Login issue on mobile app",
  "description": "Unable to log in since version 4.2...",
  "tags": ["mobile", "ios", "login"],
  "requesterName": "Jane Smith",
  "requesterEmail": "jane@example.com",
  "priority": "high",
  "type": "problem"
}
```

**Response (201 Created):**
```json
{
  "success": true,
  "zendeskTicketId": 12345,
  "ticketUrl": "https://mycompany.zendesk.com/api/v2/tickets/12345.json",
  "status": "open",
  "createdAt": "2025-01-15T10:01:00Z"
}
```

### GET /api/tickets?page=1&pageSize=20

Returns paginated tickets from SQL Server.

### GET /api/tickets/{id}

Returns a single ticket by local database ID.

### GET /health

Returns SQL Server connection health status.

---

## Postman Testing

### Import

1. Open Postman
2. **Import** → select both files from the `postman/` folder:
   - `ZendeskIntegration.postman_collection.json`
   - `ZendeskIntegration.postman_environment.json`
3. Select the **Zendesk Integration - Local** environment

### Test flow

1. **Health Check** — confirm API and SQL Server are running
2. **Generate JWT Token** — the test script auto-saves the token to `{{jwtToken}}`
3. Copy the `token` value from the response
4. Open [jwt.io](https://jwt.io) and paste the token — verify `external_id`, `name`, and `email` claims
5. **Create Ticket** — check your Zendesk Dashboard for the live ticket

---

## RS256 Configuration

To use RSA signing instead of HMAC:

1. Generate an RSA key pair:
   ```bash
   openssl genrsa -out private.pem 2048
   openssl rsa -in private.pem -pubout -out public.pem
   ```
2. Register the public key in Zendesk Admin > Channels > Messaging
3. Set in config:
   ```json
   {
     "Zendesk": {
       "JwtAlgorithm": "RS256",
       "JwtSecret": "-----BEGIN RSA PRIVATE KEY-----\n...\n-----END RSA PRIVATE KEY-----"
     }
   }
   ```

---

## Useful SQL Queries

```sql
-- Tickets not yet synced to Zendesk
SELECT Id, Subject, CreatedAt
FROM dbo.SupportTickets
WHERE SyncedToZendesk = 0
ORDER BY CreatedAt DESC;

-- JWT audit trail for a user
SELECT * FROM dbo.JwtTokenLogs
WHERE ExternalUserId = 'user-001'
ORDER BY CreatedAt DESC;

-- Failed Zendesk API calls
SELECT * FROM dbo.ZendeskApiLogs
WHERE Success = 0
ORDER BY CreatedAt DESC;

-- Average API response time by operation
SELECT Operation, AVG(DurationMs) AS AvgMs, COUNT(*) AS TotalCalls
FROM dbo.ZendeskApiLogs
GROUP BY Operation;
```

---

## Security Notes

- **Never commit `appsettings.json` with real secrets** — use `appsettings.Development.json` (git-ignored) or environment variables
- JWT tokens are never stored — only their SHA-256 hash is persisted for audit tracing
- Zendesk API credentials use HTTP Basic Auth over HTTPS only
- SQL Server connection string should use a least-privilege application account in production
