﻿using Microsoft.Extensions.Configuration;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Extensions;
using Wikibot.Logic.Factories;
using Wikibot.Logic.Logic;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;

namespace Wikibot.Logic.JobRetrievers
{
    public class TFWikiJobRetriever: IWikiJobRetriever
    {
        private List<WikiJobRequest> _jobDefinitions;
        private IConfigurationSection _wikiLoginConfig;
        private IConfigurationSection _wikiEditMessages;
        private string _timeZoneID;
        private ILogger _log;
        private IWikiAccessLogic _wikiAccessLogic;
        private RequestData _database;
        private string _wikiRequestPage;
        private string _botRequestTemplate;
        public List<WikiJobRequest> JobDefinitions
        {
            get
            {
                if (_jobDefinitions == null)
                    _jobDefinitions = GetNewJobDefinitions().Result;
                return _jobDefinitions;
            }
        }

        public TFWikiJobRetriever(IConfiguration configuration, ILogger log, IWikiAccessLogic wikiAccessLogic, IDataAccess dataAccess)
        {
            _wikiLoginConfig = configuration.GetSection("WikiLogin");
            _wikiEditMessages = configuration.GetSection("WikiEditMessages");
            _wikiRequestPage = configuration["WikiRequestPage"];
            _botRequestTemplate = configuration["BotRequestTemplate"];
            _timeZoneID = configuration["RequestTimezoneID"];
            _log = log;
            _wikiAccessLogic = wikiAccessLogic;
            _database = new RequestData(dataAccess);
        }

        public async Task<List<WikiJobRequest>> GetNewJobDefinitions()
        {
            IEnumerable<WikiJobRequest> jobs = null;
            
            using (var client = new WikiClient
            {
                ClientUserAgent = "WCLQuickStart/1.0 (your user name or contact information here)"
            })
            {
                try
                {
                    var site = _wikiAccessLogic.GetLoggedInWikiSite(_wikiLoginConfig, client);
                    var page = new WikiPage(site, _wikiRequestPage);

                    _log.Information("Fetching content from job request page.");
                    // Fetch content from the job request page so we can build jobs from it
                    await page.RefreshAsync(PageQueryOptions.FetchContent
                                            | PageQueryOptions.ResolveRedirects);

                    var ast = new WikitextParser().Parse(page.Content);
                    var templates = ast.Lines.First().EnumDescendants().OfType<Template>();

                    _log.Information("Building jobs.");
                    jobs = templates.Select(template => WikiJobRequestFactory.GetWikiJobRequest((JobType)Enum.Parse(typeof(JobType), template.Arguments.Single(arg => arg.Name.ToPlainText() == "type").Value.ToPlainText().Replace(" ", "") + "Job"), GetTimeZone(), template));
                }
                catch(Exception ex)
                {
                    _log.Error(ex, "An error occurred while trying to fetch new requests: ");
                }
            }
            return jobs.ToList();
        }

        public async void UpdateRequests(List<WikiJobRequest> requests)
        {
            using (var client = new WikiClient
            {
                ClientUserAgent = "WCLQuickStart/1.0 (your user name or contact information here)"
            })
            
            {
                try
                {
                    // You can create multiple WikiSite instances on the same WikiClient to share the state.
                    var site = _wikiAccessLogic.GetLoggedInWikiSite(_wikiLoginConfig, client);

                    var page = new WikiPage(site, _wikiRequestPage);

                    _log.Information("Pulling requests from job request page.");

                    // Fetch content of job request page so we can update it
                    await page.RefreshAsync(PageQueryOptions.FetchContent
                                            | PageQueryOptions.ResolveRedirects);

                    var parser = new WikitextParser();
                    var wikiText = parser.Parse(page.Content);

                    foreach (WikiJobRequest request in requests)
                    {
                        _log.Information($"Processing request ID: {request.ID}");
                        //Find corresponding template in the page content
                        var templates = wikiText.Lines.SelectMany(x => x.EnumDescendants().OfType<Template>());
                        var singletemplate = templates.First(x => x.Name.ToPlainText().Equals(_botRequestTemplate) && x.EqualsJob(request));

                        if (singletemplate.Arguments.SingleOrDefault(arg => arg.Name.ToPlainText().Equals("status")) == null) //Status argument doesn't exist in the template
                        {
                            var templateArgument = new TemplateArgument { Name = parser.Parse("status"), Value = parser.Parse(request.Status.ToString()) };
                            singletemplate.Arguments.Add(templateArgument);
                        }
                        else //Status argument exists
                        {
                            singletemplate.Arguments.Single(arg => arg.Name.ToPlainText().Equals("status")).Value = parser.Parse(request.Status.ToString());
                        }

                        if (singletemplate.Arguments.SingleOrDefault(arg => arg.Name.ToPlainText().Equals("id")) == null) //ID argument doesn't exist in the template
                        {
                            var templateArgument = new TemplateArgument { Name = parser.Parse("id"), Value = parser.Parse(request.ID.ToString()) };
                            singletemplate.Arguments.Add(templateArgument);
                        }

                        request.RawRequest = singletemplate.ToString();
                        _database.UpdateRaw(request.ID, request.RawRequest); //TODO: Make batch operation
                    }

                    //Update the content of the page object and push it live
                    await UpdatePageContent(wikiText.ToString(), "Test page update", page);
                    

                    // We're done here
                    await site.LogoutAsync();
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "An error occurred while trying to update requests: ");
                }
            }
        }

        private async Task UpdatePageContent(string content, string message, WikiPage page)
        {
            page.Content = content;
            await page.UpdateContentAsync(message);
        }

        private TimeZoneInfo GetTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(_timeZoneID);
        }
    }
}
