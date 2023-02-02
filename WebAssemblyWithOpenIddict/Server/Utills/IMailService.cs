using WebAssemblyWithOpenIddict.Server.Models;
//using WebAssemblyWithOpenIddict.Shared.Utils.Account;

namespace WebAssemblyWithOpenIddict.Server.Utills
{
    public interface IMailService
    {
        Task SendEmailAsync(string username, string userEmail, string redirectUrl);
        //Task SendRegisterEmailAsync(RegisterRequest request, string redirectUrl);
        Task ResendConfirmationEmailAsync(ApplicationUser request, string redirectUrl);
    }
}
