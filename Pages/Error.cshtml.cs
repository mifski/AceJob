using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Diagnostics;

namespace AceJob.Pages
{
 [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
     public string? RequestId { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;

        private readonly ILogger<ErrorModel> _logger;

        public ErrorModel(ILogger<ErrorModel> logger)
      {
         _logger = logger;
   }

        public void OnGet(string? code = null)
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
    ErrorCode = code ?? HttpContext.Response.StatusCode.ToString();

   // Get exception details if available
            var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            if (exceptionFeature != null)
    {
         _logger.LogError(exceptionFeature.Error, 
      "Unhandled exception occurred. RequestId: {RequestId}", RequestId);
       
  // Don't expose detailed error messages in production
       if (HttpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
    {
     ErrorMessage = exceptionFeature.Error.Message;
           }
       }

       // Set appropriate error message based on status code
       ErrorMessage = GetErrorMessage(ErrorCode);

            _logger.LogWarning("Error page displayed. Code: {ErrorCode}, RequestId: {RequestId}", 
           ErrorCode, RequestId);
        }

        private static string GetErrorMessage(string code)
  {
 return code switch
   {
          "400" => "The request was invalid or cannot be processed.",
            "401" => "You need to be logged in to access this resource.",
     "403" => "You don't have the required permissions to access this resource.",
     "404" => "The page you requested could not be found.",
 "405" => "The request method is not allowed for this resource.",
    "408" => "The request took too long to complete.",
                "429" => "Too many requests. Please slow down and try again later.",
                "500" => "An internal server error occurred. Our team has been notified.",
   "502" => "Bad gateway. The server received an invalid response.",
        "503" => "The service is temporarily unavailable. Please try again later.",
          "504" => "The server took too long to respond.",
                _ => "An unexpected error occurred."
          };
  }
 }
}
