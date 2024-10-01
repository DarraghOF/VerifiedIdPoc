using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace AspNetCoreVerifiableCredentials
{
    [Route(".well-known/did.json")]
    public class DidController : Controller
    {

        [AllowAnonymous]
        [HttpGet]
        public async Task<ActionResult> GetDid()
        {
            string readText = System.IO.File.ReadAllText("./Resources/did.json");
            return new ContentResult { ContentType = "application/json", Content = readText };
        }
    }
}
