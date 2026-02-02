using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AceJob.Pages
{
    public class NotFoundModel : PageModel
{
   public void OnGet()
        {
   Response.StatusCode = 404;
        }
    }
}
