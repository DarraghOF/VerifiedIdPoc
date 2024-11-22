using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreVerifiableCredentials
{
    [Route(".well-known")]
    public class DidController : Controller
    {

        [AllowAnonymous]
        [HttpGet("did.json")]
        public async Task<ActionResult> GetDid()
        {
            string readText = await System.IO.File.ReadAllTextAsync("./Resources/did.json");
            return new ContentResult { ContentType = "application/json", Content = readText };
        }

        [AllowAnonymous]
        [HttpGet("did-configuration.json")]
        public async Task<ActionResult> GetDidConfiguration()
        {
            string readText = await System.IO.File.ReadAllTextAsync("./Resources/did-configuration.json");
            return new ContentResult { ContentType = "application/json", Content = readText };
        }
    }
}
