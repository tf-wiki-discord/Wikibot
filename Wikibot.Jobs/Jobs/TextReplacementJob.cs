using MwParserFromScratch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Extensions;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Logic;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;
using WikiFunctions;

namespace Wikibot.Logic.Jobs
{
    public class TextReplacementJob: WikiJob
    {

        private IWikiAccessLogic _wikiAccessLogic;
        private int _throttleSpeedInSeconds;

        public TextReplacementJob()
        { }

        public TextReplacementJob(Serilog.ILogger log, IWikiAccessLogic wikiAccessLogic, IWikiRequestRetriever retriever, INotificationService notificationService, RequestData jobData, int throttleSpeedInSeconds)
        {
            Log = log;
            _wikiAccessLogic = wikiAccessLogic;
            JobData = jobData;
            _throttleSpeedInSeconds = throttleSpeedInSeconds;
            Retriever = retriever;
            Notifier = notificationService;
        }

        public override void Execute()
        {
            SetJobStart();
 
            try
            {

                using (WikiClient client = new WikiClient())
                {   
                    var site = _wikiAccessLogic.GetLoggedInWikiSite(client);

                    var PageList = GetPageList(site);

                    string filename = "";
                    string folderName = Request.ID.ToString();

                    foreach (WikiPage page in PageList)
                    {
                        Log.Information("Processing page {PageName}", page.Title);
                        filename = "Diff-" + Request.ID + "-" + page.Title + ".txt"; //Set filename for this page
                        page.RefreshAsync(PageQueryOptions.FetchContent | PageQueryOptions.ResolveRedirects).Wait(); //Load page content

                        var beforeContent = page.Content;
                        var afterContent = page.Content.Replace(FromText, ToText);

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
                        Thread.Sleep(1000 * _throttleSpeedInSeconds);
                    }
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

        private IEnumerable<WikiPage> GetPageList(WikiSite site)
        {
            if (Request.Pages == null || Request.Pages.Count == 0)
            {
                Log.Information("Searching for relevant pages for job {JobID}", Request.ID);
                //Search for relevant pages
                Request.Pages = site.Search(FromText, 0).Result.Select(x => new Page { PageID = 0, Name = x.Title }).ToList(); //Search Main namespace by default
            }

            return Request.Pages.Select(page => new WikiPage(site, page.Name));
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
