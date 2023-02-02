using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace WebAssemblyWithOpenIddict.Server.ViewModels.Authorization;

public class LogoutViewModel
{
    [BindNever]
    public string RequestId { get; set; } = null!;
}
