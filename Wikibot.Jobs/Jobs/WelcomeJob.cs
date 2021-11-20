using LinqToWiki.Generated;
using Microsoft.Extensions.Configuration;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wikibot.Logic.Logic;
using WikiClientLibrary.Client;
using WikiClientLibrary.Generators;
using WikiClientLibrary.Generators.Primitive;
using WikiClientLibrary.Pages;

namespace Wikibot.Logic.Jobs
{
    public class WelcomeJob : WikiJob
    {
        private IWikiAccessLogic _accessLogic;
        private int _interval;
        private string _templateMarkup;
        private string _editSummary;

        public WelcomeJob(IWikiAccessLogic accessLogic, IConfiguration config, ILogger log)
        {
            _accessLogic = accessLogic;
            _interval = config.GetValue<int>("WelcomeInterval") * -1;
            _templateMarkup = config.GetValue<string>("WelcomeTemplateMarkup");
            _editSummary = config.GetValue<string>("WelcomeEditSummary");
            Log = log; 
        }

        public async void Execute(bool unitTest)
        {
            Log.Information("Starting WelcomeJob");
            using (var client = new WikiClient())
            {
                //Check for new user creation
                var wiki = _accessLogic.GetLoggedInWikiSite(client);
                var end = DateTime.Now.AddMinutes(_interval);
                var items = _accessLogic.GetLoggedInWiki().Query .logevents().Where(e => e.type == logeventstype2.newusers && e.end == end).ToList(); 

                //var generator = new RecentChangesGenerator(wiki)
                //{
                //    PaginationSize = 50,
                //    EndTime = DateTime.Parse("13:36, 30 October 2021"),
                //    //EndTime = DateTime.Now,
                //    TypeFilters = RecentChangesFilterTypes.Log,
                //    Tag = "",
                //};

                //var items = await generator.EnumItemsAsync().ToListAsync();
                List<string> newUserNames = new List<string>();
                foreach(var item in items)
                {
                    newUserNames.Add(item.user);
                }
                var parser = new WikitextParser();
                var welcomeTemplate = parser.Parse(_templateMarkup).EnumDescendants().OfType<Template>().First();
                int count = 0;
                
                //For each new user, pull their userpage. If no welcome template, place template at top of page
                foreach (string newUserName in newUserNames)
                {
                    var page = new WikiPage(wiki, $"User_talk:{newUserName}");
                    await page.RefreshAsync(PageQueryOptions.FetchContent
                        | PageQueryOptions.ResolveRedirects);
                    var wikiText = parser.Parse(page.Content ?? "");
                    if (wikiText.ToString().Length <= 1 || !wikiText.EnumDescendants().OfType<Template>().Any(template => template.Name.Equals("welcome")))
                    {
                        Log.Information($"Welcoming {newUserName}");
                        wikiText.InsertBefore(welcomeTemplate);
                        page.Content = wikiText.ToString();
                        if (!unitTest)
                        {
                            await page.UpdateContentAsync(_editSummary, true, true);
                        }
                    }
                    count++;
                }

                Log.Information($"{count} users welcomed.");
                
            }

            
        }
    }
}
