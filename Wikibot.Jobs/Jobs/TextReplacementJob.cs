using Microsoft.Extensions.Configuration;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Extensions;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Logic;
using Wikibot.Logic.UserRetrievers;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Sites;
using WikiFunctions;

namespace Wikibot.Logic.Jobs
{
    public class TextReplacementJob: WikiJob
    {

        private IWikiAccessLogic _wikiAccessLogic;
        private int _throttleSpeedInSeconds;
        private string _requestPickupPage;

        public TextReplacementJob()
        { }

        public TextReplacementJob(IConfiguration config, Serilog.ILogger log, IWikiAccessLogic wikiAccessLogic, IWikiRequestRetriever retriever, INotificationService notificationService, IUserRetriever userRetriever, RequestData jobData, int throttleSpeedInSeconds)
        {
            Log = log;
            _wikiAccessLogic = wikiAccessLogic;
            JobData = jobData;
            _throttleSpeedInSeconds = throttleSpeedInSeconds;
            Retriever = retriever;
            Notifier = notificationService;
            UserRetriever = userRetriever;
            _requestPickupPage = config.GetValue<string>("WikiRequestPage");
        }

        public override void Execute()
        {
            SetJobStart();
 
            try
            {

                using (WikiClient client = new WikiClient())
                {   
                    var site = _wikiAccessLogic.GetLoggedInWikiSite(client);
                    var parser = new WikitextParser();
                    var wikiText = parser.Parse(FromText);
                    var searchText = new StringBuilder().Append('"').Append(wikiText.ToPlainText()).Append('"').ToString();
                    var wikiLinks = wikiText.EnumDescendants().OfType<WikiLink>();
                    var templates = wikiText.EnumDescendants().OfType<Template>();
                    var backLinks = new List<WikiPage>();
                    var TPbacklinks = new List<WikiPage>();

                    if (wikiLinks.Any())
                    {
                        foreach (WikiLink link in wikiLinks)
                        {
                            if (!backLinks.Any())
                            {
                                backLinks.AddRange(GetBackLinksPageList(site, link.Target.ToPlainText()));
                            }
                            else
                            {
                                backLinks = backLinks.Intersect(GetBackLinksPageList(site, link.Target.ToPlainText())).ToList();
                            }
                        }
                    }

                   var PageList = SearchPageText(site, searchText);

                    if (backLinks.Any())
                    {
                        PageList = PageList.Intersect(backLinks);
                    }

                    string filename = "";
                    string folderName = Request.ID.ToString();
                    List<WikiPage> pagesToRemove = new List<WikiPage>();

                    foreach (WikiPage page in PageList)
                    {
                        Log.Information("Processing page {PageName}", page.Title);
                        filename = "Diff-" + Request.ID + "-" + page.Title + ".txt"; //Set filename for this page
                        page.RefreshAsync(PageQueryOptions.FetchContent | PageQueryOptions.ResolveRedirects).Wait(); //Load page content
                        
                        var beforeContent = page.Content;
                        var afterContent = page.Content.Replace(FromText, ToText);
                        if (!afterContent.Equals(beforeContent) && page.Title != Configuration.GetValue<string>("WikiRequestPage"))
                        {
                            if (Request.Status != JobStatus.Approved) //Create diffs for approval
                            {
                                Log.Information("Generating diff for page {PageName}", page.Title);
                                var folderPath = Path.Combine(Configuration["DiffDirectory"], folderName);
                                if (!Directory.Exists(folderPath))
                                {
                                    Directory.CreateDirectory(folderPath);
                                }

                                Utilities.GenerateAndSaveDiff(beforeContent, afterContent, page.Title, Request.ID, Configuration["DiffDirectory"], folderName);

                                JobData.SaveWikiJobRequest(Request); //Save page list                        
                            }
                            else //Apply changes
                            {
                                Log.Information("Applying replacement for page {PageName}", page.Title);
                                var editMessage = $"{WikiConfig["Username"]} Text Replacement {FromText} => {ToText}";
                                UpdatePageContentWithMessage(page, afterContent, editMessage);
                            }
                        }
                        else
                        {
                            pagesToRemove.Add(page);
                        }
                        Thread.Sleep(1000 * _throttleSpeedInSeconds);
                    }

                    PageList = PageList.Except(pagesToRemove);
                    Request.Pages = PageList.Select(x => new Page(0, x.Title)).ToList();
                    Retriever.UpdateRequests(new List<WikiJobRequest> { Request });
                    site.LogoutAsync().Wait();
                    
                }
            }
            catch(Exception ex)
            {
                FailJob(ex);
            }
            finally
            {
                SetJobEnd();
                SaveRequest();
            }
        }

        private IEnumerable<WikiPage> SearchPageText(WikiSite site, string searchText)
        {
            if (Request.Pages == null || Request.Pages.Count == 0)
            {
                Log.Information("Searching for relevant pages for job {JobID}", Request.ID);
                //Search for relevant pages
                Request.Pages = site.Search(searchText, 0).Result.Select(x => new Page { PageID = 0, Name = x.Title }).ToList(); //Search Main namespace by default
            }

            return Request.Pages.Select(page => new WikiPage(site, page.Name));
        }

        private IEnumerable<WikiPage> GetBackLinksPageList(WikiSite site, string pageTitle)
        {
            Log.Information("Searching for relevant pages for job {JobID}", Request.ID);
            var linkList = new List<WikiSiteExtension.SearchResultEntry>();
            //Search for relevant pages
            if (Request.Pages == null || Request.Pages.Count == 0)
            {
                linkList = site.BackLinks(pageTitle).Result.ToList();
                Request.Pages = linkList.Select(link => new Page(0, link.Title)).ToList();
            }

            return linkList.Select(link => new WikiPage(site, link.Title));

        }

        private void UpdatePageContentWithMessage(WikiPage page, string content, string editMessage)
        {
            var parser = new WikitextParser();
            var wikiText = parser.Parse(content);
            page.Content = wikiText.ToString();
            page.UpdateContentAsync(editMessage).Wait();
        }
    }
}
