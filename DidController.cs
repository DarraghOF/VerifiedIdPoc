using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace AspNetCoreVerifiableCredentials
{
    [Route(".well-known")]
    public class DidController : Controller
    {

        [AllowAnonymous]
        [HttpGet("did.json")]
        public async Task<ActionResult> GetDid()
        {
            string readText = System.IO.File.ReadAllText("./Resources/did.json");
            return new ContentResult { ContentType = "application/json", Content = readText };
        }

        [AllowAnonymous]
        [HttpGet("did-configuration.json")]
        public async Task<ActionResult> GetDidConfiguration()
        {
            string readText = System.IO.File.ReadAllText("./Resources/did-configuration.json");
            return new ContentResult { ContentType = "application/json", Content = readText };
        }
    }
}
