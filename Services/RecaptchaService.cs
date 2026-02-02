using System.Text.Json;

namespace AceJob.Services
{
    public interface IRecaptchaService
    {
   Task<RecaptchaResponse> VerifyToken(string token);
    }

    public class RecaptchaService : IRecaptchaService
    {
        private readonly string? _secretKey;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RecaptchaService> _logger;
        private const string VerifyUrl = "https://www.google.com/recaptcha/api/siteverify";

        public RecaptchaService(
            IConfiguration configuration,
         IHttpClientFactory httpClientFactory,
  ILogger<RecaptchaService> logger)
        {
      _secretKey = configuration["RecaptchaSettings:SecretKey"];
   _httpClientFactory = httpClientFactory;
  _logger = logger;
        }

     public async Task<RecaptchaResponse> VerifyToken(string token)
        {
            // Validate secret key
            if (string.IsNullOrEmpty(_secretKey))
            {
    _logger.LogError("reCAPTCHA Secret Key is missing from configuration. Check appsettings.json RecaptchaSettings:SecretKey");
   return RecaptchaResponse.Failure("missing-secret-key");
            }

            // Warn about potentially invalid secret key format
if (!_secretKey.StartsWith("6L") || _secretKey.Length < 35)
        {
                _logger.LogWarning("reCAPTCHA Secret Key may be invalid. Expected format: starts with '6L' and ~40 characters. Current length: {Length}", _secretKey.Length);
  }

            // Validate token
            if (string.IsNullOrEmpty(token))
      {
     _logger.LogWarning("reCAPTCHA token is empty or null");
     return RecaptchaResponse.Failure("missing-input-response");
    }

  // Log token preview for debugging
_logger.LogDebug("reCAPTCHA Token received - Length: {Length}, Preview: {Preview}...",
                token.Length,
      token.Length > 20 ? token[..20] : token);

    try
            {
       return await SendVerificationRequest(token);
            }
            catch (HttpRequestException ex)
   {
                _logger.LogError(ex, "Network error when contacting reCAPTCHA API. Check internet connection and firewall settings.");
      return RecaptchaResponse.Failure("network-error");
         }
  catch (JsonException ex)
       {
    _logger.LogError(ex, "Failed to parse reCAPTCHA API response");
     return RecaptchaResponse.Failure("parse-error");
            }
       catch (Exception ex)
      {
     _logger.LogError(ex, "Unexpected error verifying reCAPTCHA token");
         return RecaptchaResponse.Failure("exception-occurred");
    }
     }

        private async Task<RecaptchaResponse> SendVerificationRequest(string token)
        {
          var httpClient = _httpClientFactory.CreateClient();
     var parameters = new Dictionary<string, string>
            {
           { "secret", _secretKey! },
           { "response", token }
 };

       var content = new FormUrlEncodedContent(parameters);

          _logger.LogDebug("Sending reCAPTCHA verification request to Google...");

            var response = await httpClient.PostAsync(VerifyUrl, content);
      var jsonResponse = await response.Content.ReadAsStringAsync();

       _logger.LogInformation("reCAPTCHA API Response Status: {StatusCode}, Body: {Response}",
         response.StatusCode, jsonResponse);

  if (!response.IsSuccessStatusCode)
  {
       _logger.LogError("reCAPTCHA API returned non-success status: {StatusCode}", response.StatusCode);
    return RecaptchaResponse.Failure("api-error");
        }

            var recaptchaResponse = JsonSerializer.Deserialize<RecaptchaResponse>(jsonResponse, new JsonSerializerOptions
  {
     PropertyNameCaseInsensitive = true
  });

            if (recaptchaResponse == null)
            {
    return RecaptchaResponse.Failure("null-response");
            }

          LogRecaptchaResult(recaptchaResponse);
    return recaptchaResponse;
        }

     private void LogRecaptchaResult(RecaptchaResponse response)
        {
       _logger.LogInformation(
   "reCAPTCHA Validation Result - Success: {Success}, Score: {Score}, Action: {Action}, Hostname: {Hostname}",
        response.Success,
          response.Score,
        response.Action,
      response.Hostname);

            if (response.ErrorCodes is not { Length: > 0 })
    return;

       _logger.LogWarning("reCAPTCHA returned errors: {Errors}", string.Join(", ", response.ErrorCodes));

    foreach (var error in response.ErrorCodes)
            {
       var explanation = GetErrorExplanation(error);
              _logger.LogWarning("reCAPTCHA Error [{Error}]: {Explanation}", error, explanation);
            }
        }

        private static string GetErrorExplanation(string error)
      {
            return error switch
       {
     "missing-input-secret" => "The secret parameter is missing - check SecretKey configuration",
  "invalid-input-secret" => "The secret parameter is invalid or malformed - verify your SecretKey in Google reCAPTCHA admin",
   "missing-input-response" => "The response parameter (token) is missing",
         "invalid-input-response" => "The response parameter is invalid or malformed - token may be corrupted or expired",
                "bad-request" => "The request is invalid or malformed",
            "timeout-or-duplicate" => "The response is no longer valid - token already used or expired (tokens expire after 2 minutes)",
      _ => $"Unknown error: {error}"
     };
        }
    }

    public class RecaptchaResponse
    {
        public bool Success { get; set; }
public double Score { get; set; }
        public string Action { get; set; } = string.Empty;
      public DateTime ChallengeTs { get; set; }
        public string Hostname { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }

     /// <summary>
        /// Creates a failure response with the specified error code
     /// </summary>
 public static RecaptchaResponse Failure(string errorCode)
 {
            return new RecaptchaResponse
    {
        Success = false,
                ErrorCodes = new[] { errorCode }
   };
        }
    }
}
