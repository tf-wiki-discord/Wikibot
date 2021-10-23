using LinqToWiki.Generated;
using Microsoft.Extensions.Configuration;
using Serilog;
using System;
using WikiClientLibrary.Client;
using WikiClientLibrary.Sites;

namespace Wikibot.Logic.Logic
{
    public class WikiAccessLogic : IWikiAccessLogic
    {
        private IConfigurationSection _wikiConfig;
        private ILogger _log;

        public WikiAccessLogic(IConfiguration config, ILogger log)
        {
            _wikiConfig = config.GetSection("WikiLogin");
            _log = log;
        }

        public Wiki GetLoggedInWiki()
        {
            var url = _wikiConfig["APIUrl"];
            var path = _wikiConfig["APIPath"];
            Wiki wiki = new Wiki("WikiBot", url, path);

            LogIntoWiki(wiki);

            return wiki;
        }

        public WikiSite GetLoggedInWikiSite(WikiClient client)
        {
            var url = _wikiConfig["APIUrl"];
            var path = _wikiConfig["APIPath"];
            var uriBuilder = new UriBuilder(url);
            uriBuilder.Path = path;
            var site = new WikiSite(client, uriBuilder.Uri.ToString());
            LogIntoWikiSite(site);
            return site;
        }

        private void LogIntoWikiSite(WikiSite site)
        {
            var username = _wikiConfig["Username"];
            var password = _wikiConfig["Password"];

            // Wait for initialization to complete.
            // Throws error if any.
            site.Initialization.Wait();
            try
            {
                site.LoginAsync(username, password).Wait();
            }
            catch(Exception ex)
            {
                _log.Error(ex, "Error logging in:");
                throw;
            }
        }

        private void LogIntoWiki(Wiki wiki)
        {
            var username = _wikiConfig["Username"];
            var password = _wikiConfig["Password"];

            var result = wiki.login(username, password);

            if (result.result == loginresult.NeedToken)
                result = wiki.login(username, password, token: result.token);


            if (result.result != loginresult.Success)
            {
                throw new Exception(result.result.ToString());
            }
        }
    }
}
