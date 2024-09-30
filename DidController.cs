using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreVerifiableCredentials
{
    [Route(".well-known/did.json")]
    [ApiController]
    public class DidController : ControllerBase
    {

        [AllowAnonymous]
        [HttpGet]
        public ActionResult getPresentationDetails()
        {

            Data data = new Data{
                Success = true,
            }; 
            return new ContentResult { ContentType = "application/json", Content = JsonSerializer.Serialize(data) };

        }
    }

    public class Data
    {
        public bool Success { get; set; }
    }
}
