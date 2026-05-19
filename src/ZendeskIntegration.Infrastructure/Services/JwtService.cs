using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ZendeskIntegration.Core.DTOs;
using ZendeskIntegration.Core.Interfaces;
using ZendeskIntegration.Core.Models;

namespace ZendeskIntegration.Infrastructure.Services;

public class JwtService : IJwtService
{
    private readonly ZendeskOptions _options;
    private readonly IJwtTokenLogRepository _logRepo;
    private readonly ILogger<JwtService> _logger;

    public JwtService(
        IOptions<ZendeskOptions> options,
        IJwtTokenLogRepository logRepo,
        ILogger<JwtService> logger)
    {
        _options = options.Value;
        _logRepo = logRepo;
        _logger = logger;
    }

    public async Task<GenerateTokenResponse> GenerateTokenAsync(
        GenerateTokenRequest request,
        string? ipAddress = null,
        string? userAgent = null)
    {
        if (string.IsNullOrWhiteSpace(request.ExternalUserId))
            throw new ArgumentException("ExternalUserId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("Email is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));

        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.JwtExpiryMinutes);

        var tokenHandler = new JwtSecurityTokenHandler();
        SigningCredentials credentials = BuildSigningCredentials();

        // Zendesk Messaging JWT claims
        var claims = new List<Claim>
        {
            new Claim("external_id", request.ExternalUserId),
            new Claim("name", request.Name),
            new Claim("email", request.Email),
            new Claim(JwtRegisteredClaimNames.Iat,
                new DateTimeOffset(issuedAt).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        if (!string.IsNullOrWhiteSpace(_options.JwtKeyId))
        {
            // kid in header helps Zendesk resolve the correct signing key
            tokenHandler.SetDefaultTimesOnTokenCreation = false;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = credentials,
        };

        var token = tokenHandler.CreateToken(descriptor);

        // Inject kid into header if configured
        if (!string.IsNullOrWhiteSpace(_options.JwtKeyId) && token is JwtSecurityToken jwtToken)
        {
            jwtToken.Header["kid"] = _options.JwtKeyId;
        }

        var tokenString = tokenHandler.WriteToken(token);

        _logger.LogInformation(
            "JWT generated for user {UserId} ({Email}) using {Algorithm}",
            request.ExternalUserId, request.Email, _options.JwtAlgorithm);

        // Persist audit log to SQL Server — store hash, never the raw token
        var tokenHash = ComputeSha256Hash(tokenString);
        await _logRepo.AddAsync(new JwtTokenLog
        {
            ExternalUserId = request.ExternalUserId,
            UserName = request.Name,
            UserEmail = request.Email,
            Algorithm = _options.JwtAlgorithm,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            TokenHash = tokenHash,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        });

        return new GenerateTokenResponse
        {
            Token = tokenString,
            IssuedAt = issuedAt,
            ExpiresAt = expiresAt,
            Algorithm = _options.JwtAlgorithm,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private SigningCredentials BuildSigningCredentials()
    {
        return _options.JwtAlgorithm.ToUpperInvariant() switch
        {
            "RS256" => BuildRs256Credentials(),
            _ => BuildHs256Credentials(),
        };
    }

    private SigningCredentials BuildHs256Credentials()
    {
        if (string.IsNullOrWhiteSpace(_options.JwtSecret))
            throw new InvalidOperationException(
                "Zendesk:JwtSecret is not configured. Set it via environment variable or appsettings.");

        var keyBytes = Encoding.UTF8.GetBytes(_options.JwtSecret);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException("JwtSecret must be at least 32 characters for HS256.");

        var securityKey = new SymmetricSecurityKey(keyBytes);
        return new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
    }

    private SigningCredentials BuildRs256Credentials()
    {
        if (string.IsNullOrWhiteSpace(_options.JwtSecret))
            throw new InvalidOperationException(
                "Zendesk:JwtSecret (RSA private key PEM) is not configured for RS256.");

        var rsa = RSA.Create();
        rsa.ImportFromPem(_options.JwtSecret);
        var rsaKey = new RsaSecurityKey(rsa);
        return new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256);
    }

    private static string ComputeSha256Hash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
