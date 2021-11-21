
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Extensions;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Logic;
using Wikibot.Logic.UserRetrievers;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Sites;

namespace Wikibot.Logic.Jobs
{
    public class ContinuityLinkFixJob : WikiJob
    {
        private IWikiAccessLogic _wikiAccessLogic;
        private int _throttleSpeedInSeconds;

        public ContinuityLinkFixJob()
        { }

        public ContinuityLinkFixJob(Serilog.ILogger log, IWikiAccessLogic wikiAccessLogic, IWikiRequestRetriever retriever, INotificationService notificationService, IUserRetriever userRetriever, RequestData jobData, int throttleSpeedInSeconds)
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
            List<WikiPage> AlreadyUpdatedPages = new List<WikiPage>();
            List<WikiPage> NoUpdateNeededPages = new List<WikiPage>();
            try
            {
                using (var client = new WikiClient())
                {
                    var site = _wikiAccessLogic.GetLoggedInWikiSite(client);
                    var parser = new WikitextParser();
                    var wikiText = parser.Parse(FromText);
                    var fromLinkTarget = wikiText.Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>()).FirstOrDefault()?.Target.ToPlainText() ?? FromText;
                    wikiText = parser.Parse(ToText);
                    var toLinkTarget = wikiText.Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>()).FirstOrDefault().Target.ToPlainText();

                    var PageList = GetBackLinksPageList(site, fromLinkTarget);
                    var storyLinkPageList = GetStoryLinksPageList(site, toLinkTarget);

                    var folderName = Request.ID.ToString();
                    var folderPath = Path.Combine(Configuration["DiffDirectory"], folderName);
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    
                    foreach (WikiPage page in PageList)
                    {
                        Log.Information("Processing page {PageName}", page.Title);

                        IEnumerable<WikiLink> wikiLinks = null;
                        
                        page.RefreshAsync(PageQueryOptions.FetchContent | PageQueryOptions.ResolveRedirects).Wait(); //Load page content
                        
                        //Get page text
                        var beforeContent = page.Content;
                        var wikiPageText = parser.Parse(beforeContent);  
                        
                        if (string.IsNullOrWhiteSpace(string.Join(' ',HeadersToSearch)))
                        {
                            throw new Exception("No continuity header specified");
                        }
                        else
                        {
                            //Get any wiki links under the header
                            var headers = wikiPageText.EnumDescendants().OfType<Heading>();
                            var matchingHeaders = headers.Where(y => HeadersToSearch.Contains(y.ToPlainText()) || HeadersToSearch.Contains(y.ToString()));
                            //Need to handle cases like Armada Megatron where links are on cartoon/comic/whatever subpage. Look for subpage header based on media type, get subpage, add to Pages list and get links.
                            if (matchingHeaders.Any())
                            {

                                var contentNodes = GetContentBetweenHeaders(headers, matchingHeaders, wikiPageText);
                                wikiLinks = contentNodes.SelectMany(x=> x.EnumDescendants().OfType<WikiLink>());
                            }
                            else
                            {
                                var templates = wikiPageText.Lines.SelectMany(x => x.EnumDescendants().OfType<Template>());

                                var mediaTemplate = templates.Where(template => template.Name.ToPlainText().Equals(GetTemplateNameByMedia(Media))).SingleOrDefault();

                                if (mediaTemplate != null || HasFileTemplateForMedia(templates, Media))
                                {
                                    wikiLinks = wikiPageText.Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>());
                                }
                                else
                                {
                                    var mainTemplates = templates.Where(template => template.Name.ToPlainText().Equals("main"));
                                    if (mainTemplates.Any())
                                    {
                                        wikiLinks = GetMainTemplatePageLinks(mainTemplates, site);
                                    }
                                }
                            }
                        }
                        var matchingLinks = wikiLinks?.Where(link => CompareLinks(link.Target.ToString(), fromLinkTarget)).ToList();

                        if (matchingLinks == null || !matchingLinks.Any() || page.Title.Equals(Configuration["WikiRequestPage"], StringComparison.OrdinalIgnoreCase))
                        {
                            Request.Pages.RemoveAll(x => x.Name.Equals(page.Title, StringComparison.OrdinalIgnoreCase));
                            if (wikiLinks?.Where(link => CompareLinks(link.Target.ToString(), toLinkTarget)).Any() ?? false)
                            {
                                AlreadyUpdatedPages.Add(page);
                            }
                            else
                            {
                                NoUpdateNeededPages.Add(page);
                            }
                        }
                        else
                        {
                            foreach (WikiLink link in matchingLinks)
                            {
                                Log.Debug($"Link target starts: {link.Target}");
                                var newTarget = parser.Parse(ToText).Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>()).FirstOrDefault()?.Target.ToPlainText() ?? ToText;
                                if (link.Text == null)
                                {
                                    link.Text = new Run(new PlainText(link.Target.ToPlainText())); //Maintain original link text if the link had no custom text
                                }
                                link.Target = new Run(new PlainText(newTarget));
                                Log.Debug($"Link target ends: {link.Target}");
                            }
                            Log.Debug($"Content after: {wikiPageText}");


                            var afterContent = wikiPageText.ToString();

                            if (Request.Status != JobStatus.Approved) //Create diffs for approval
                            {
                                Log.Information("Generating diff for page {PageName}", page.Title);

                                Utilities.GenerateAndSaveDiff(beforeContent, afterContent, page.Title, Request.ID, Configuration["DiffDirectory"], folderName);

                                JobData.SaveWikiJobRequest(Request); //Save page list                        
                            }
                            else //Apply changes
                            {
                                Log.Information("Applying replacement for page {PageName}", page.Title);
                                var editMessage = $"{WikiConfig["Username"]} Text Replacement {FromText} => {ToText}";
                                ((TFWikiRequestRetriever)Retriever).UpdatePageContent(afterContent, editMessage, page).Wait();
                            }
                        }
                   
                        Thread.Sleep(1000 * _throttleSpeedInSeconds);
                     }
                }
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                FailJob(ex);
            }
            finally
            {
                SetJobEnd();
                SaveRequest();
            }
        }

        private IEnumerable<WikiLink> GetMainTemplatePageLinks(IEnumerable<Template> mainTemplates, WikiSite site)
        {
            Wikitext pageText = null;
            IEnumerable<WikiLink> wikiLinks = new List<WikiLink>();
            foreach (Template template in mainTemplates)
            {
                var linkedPage = new WikiPage(site, template.Arguments.First().ToString());
                Log.Information("Processing page {PageName}", linkedPage.Title);
                linkedPage.RefreshAsync(PageQueryOptions.FetchContent | PageQueryOptions.ResolveRedirects).Wait();
                if (linkedPage.Exists)
                {
                    pageText = new WikitextParser().Parse(linkedPage.Content);
                    var matchingPageHeaders = pageText.EnumDescendants().OfType<Heading>().Where(y => HeadersToSearch.Contains(y.ToPlainText()) || HeadersToSearch.Contains(y.ToString()));

                    if (matchingPageHeaders.Any())
                    {
                        wikiLinks = pageText.Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>());
                        break;
                    }
                }
            }
            return wikiLinks;
        }

        private List<Node> GetContentBetweenHeaders(IEnumerable<Heading> allHeaders, IEnumerable<Heading> headersToGetContentFor, Wikitext wikiPageText)
        {
            List<Node> contentNodes = new List<Node>();
            foreach (Heading header in headersToGetContentFor)
            {
                var sameLevelOrAboveHeaders = allHeaders.Where(x => x.Level <= header.Level);
                var firstChunk = wikiPageText.EnumDescendants().SkipWhile(node => node.ToString() != header.ToString());
                var rangeChunk = firstChunk.TakeWhile(node => node == header || !sameLevelOrAboveHeaders.Contains(node));
                contentNodes.AddRange(rangeChunk);
            }
            return contentNodes;
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

        private IEnumerable<WikiPage> GetStoryLinksPageList(WikiSite site, string pageTitle)
        {
            var targetPage = new WikiPage(site, pageTitle);
            targetPage.RefreshAsync(PageQueryOptions.FetchContent | PageQueryOptions.ResolveRedirects).Wait(); //Load page content

            //Get page text
            var parser = new WikitextParser();
            var wikiPageText = parser.Parse(targetPage.Content);

            IEnumerable<Template> templateList = new List<Template>(); 
            var header = wikiPageText.Lines.SelectMany(x => x.EnumDescendants().OfType<Heading>()).Where(y => HeadersToSearch.Contains(y.ToPlainText())).SingleOrDefault();
            if (header != null)
            {
                templateList = header.EnumDescendants().OfType<Template>();
            }
            else
            {
                templateList = wikiPageText.EnumDescendants().OfType<Template>();
            }
            var storyLinkTemplates = templateList.Where(template => template.Name.Equals("storylink"));
            return storyLinkTemplates.Select(template => new WikiPage(site, template.Arguments.Single().Value.ToPlainText()));

        }

        private bool HasFileTemplateForMedia(IEnumerable<Template> templates, MediaType mediaType)
        {
            switch (mediaType)
            {
                case MediaType.Cartoon: //Eventually change this to check against template categories. Only way to tell between movie and cartoon.
                    return templates.Where(template=> template.Name.ToString().ToLower().EndsWith("cap") && !template.Name.ToString().ToLower().Contains("game")).Any();
                case MediaType.Comic:
                    return templates.Where(template => template.Name.ToString().ToLower().Contains("comic")).Any();
                case MediaType.VideoGame:
                    return templates.Where(template => template.Name.ToString().ToLower().Contains("gamecap")).Any();
                default:
                    return false;
            }
        }
        public bool CompareLinks(string x, string y)
        {

            if (y.StartsWith(char.ToLower(x[0])) && x.Substring(1).Equals(y.Substring(1)))
                return true;
            else
                return x.Equals(y);
        }

        private string GetTemplateNameByMedia(MediaType type)
        {
            switch (type)
            {
                case MediaType.Cartoon:
                    return "episode";
                case MediaType.Comic:
                    return "comicstory";
                case MediaType.VideoGame:
                    return "videogame";
                default:
                    return "";
            }

        }
    }
}
