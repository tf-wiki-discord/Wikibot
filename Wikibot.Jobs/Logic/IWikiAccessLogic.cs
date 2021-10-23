using LinqToWiki.Generated;
using Microsoft.Extensions.Configuration;
using Serilog;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace Wikibot.Logic.Logic
{
    public interface IWikiAccessLogic
    {
        Wiki GetLoggedInWiki();
        WikiSite GetLoggedInWikiSite(WikiClient client);
    }
}