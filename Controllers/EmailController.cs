using Microsoft.AspNetCore.Mvc;
using AceJob.Services;

namespace AceJob.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly GmailEmailService _emailService;

        public EmailController(GmailEmailService emailService)
        {
            _emailService = emailService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendEmail(string toEmail, string name)
        {
            string subject = "Welcome to Our Platform";
            string body = $"<h2>Hello {name},</h2><p>Your account has been created successfully.</p>";

            await _emailService.SendEmailAsync(toEmail, subject, body);

            return Ok($"✅ Email sent to {toEmail}");
        }
    }
}