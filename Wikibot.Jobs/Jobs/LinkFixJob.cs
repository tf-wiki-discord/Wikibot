﻿using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Extensions;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Logic;
using WikiClientLibrary.Client;
using WikiClientLibrary.Pages;
using WikiClientLibrary.Pages.Queries;
using WikiClientLibrary.Pages.Queries.Properties;
using WikiClientLibrary.Sites;
using WikiFunctions;

namespace Wikibot.Logic.Jobs
{
    public class LinkFixJob : WikiJob
    {
        private IWikiAccessLogic _wikiAccessLogic;
        private int _throttleSpeedInSeconds;

        public LinkFixJob()
        { }

        public LinkFixJob(Serilog.ILogger log, IWikiAccessLogic wikiAccessLogic, IWikiJobRetriever retriever, RequestData jobData, int throttleSpeedInSeconds)
        {
            Log = log;
            _wikiAccessLogic = wikiAccessLogic;
            JobData = jobData;
            _throttleSpeedInSeconds = throttleSpeedInSeconds;
            Retriever = retriever;
        }

        public override void Execute()
        {
            SetJobStart();

            try
            {
                using (var client = new WikiClient())
                {
                    var site = _wikiAccessLogic.GetLoggedInWikiSite(WikiConfig, client, Log);
                    var parser = new WikitextParser();
                    var wikiText = parser.Parse(FromText);
                    var fromLinkTarget = wikiText.Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>()).FirstOrDefault().Target.ToPlainText();
                    

                    var PageList = GetBackLinksPageList(site, fromLinkTarget);

                    string filename = "";
                    string diff = "";
                    string filePath = "";
                    var folderName = Request.ID.ToString();
                    var folderPath = Path.Combine(Configuration["DiffDirectory"], folderName);
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    foreach (WikiPage page in PageList)
                    {
                        Log.Information("Processing page {PageName}", page.Title);

                        page.RefreshAsync(PageQueryOptions.FetchContent | PageQueryOptions.ResolveRedirects).Wait(); //Load page content

                        var beforeContent = page.Content;   
                        var wikiPageText = parser.Parse(beforeContent);
                        IEnumerable<WikiLink> wikiLinks = null;
                        if (string.IsNullOrWhiteSpace(string.Join(' ', HeadersToSearch)))
                        {
                            wikiLinks = wikiPageText.Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>());
                        }
                        else
                        {
                            var header = wikiPageText.Lines.SelectMany(x => x.EnumDescendants().OfType<Heading>()).Where(y => y.ToPlainText().Equals(HeadersToSearch)).Single();
                            wikiLinks = header.EnumDescendants().OfType<WikiLink>();
                        }
                        var matchingLinks = wikiLinks.Where(link => CompareLinks(link.Target.ToString(), fromLinkTarget)).ToList();

                        if (!matchingLinks.Any() || page.Title.Equals(Configuration["WikiRequestPage"], StringComparison.OrdinalIgnoreCase))
                        {
                            Request.Pages.RemoveAll(x => x.Name.Equals(page.Title, StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            foreach (WikiLink link in matchingLinks)
                            {
                                Log.Debug($"Link target starts: {link.Target}");
                                var newTarget = parser.Parse(ToText).Lines.SelectMany(x => x.EnumDescendants().OfType<WikiLink>()).FirstOrDefault().Target.ToPlainText();
                                if (link.Text == null && (!link.Target.ToPlainText().Contains("(") && newTarget.Contains("(")))
                                {
                                    link.Text = new Run(new PlainText(link.Target.ToPlainText())); //Maintain original link text if the link had no custom text and no disambig
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
                                //var wikiDiff = new WikiDiff();
                                //diff = $"{WikiDiff.DiffHead()}</head><body>{WikiDiff.TableHeader}{wikiDiff.GetDiff(beforeContent, afterContent, 1)}</table></body></html>";
                                //filename = "Diff-" + Request.ID + "-" + page.Title + ".txt"; //Set filename for this page
                                //filename = Utilities.SanitizeFilename(filename, '_');

                                //filePath = Path.Combine(Configuration["DiffDirectory"], folderName, filename);
                                //File.WriteAllText(filePath, diff);
                                JobData.SaveWikiJobRequest(Request); //Save page list                        
                            }
                            else //Apply changes
                            {
                                Log.Information("Applying replacement for page {PageName}", page.Title);
                                var editMessage = $"{WikiConfig["Username"]} Text Replacement {FromText} => {ToText}";
                                ((TFWikiJobRetriever)Retriever).UpdatePageContent(afterContent, editMessage, page).Wait();
                            }
                        }
                        Thread.Sleep(1000 * _throttleSpeedInSeconds);
                    }
                }
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                Request.Status = JobStatus.Failed;
                Log.Error(ex, $"TextReplacementJob with ID: {Request.ID} failed.");
            }
            finally
            {
                SetJobEnd();
                SaveRequest();
            }
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

            //var page = new WikiPage(site, link.Target.ToPlainText());
            //var provider = WikiPageQueryProvider.FromOptions(PageQueryOptions.None);
            //provider.Properties.Add(new LinksHerePropertyProvider());
            //await page.RefreshAsync(new WikiPageQueryProvider
            //{
            //    Properties =
            //    {
            //        new LinksHerePropertyProvider()
            //    },

            //});
            //return page.GetPropertyGroup<LinksHerePropertyGroup>().LinkedPages.Select(page=> new WikiPage(site, page.Title));
        }

        public bool CompareLinks(string x, string y)
        {

            if (y.StartsWith(char.ToLower(x[0])) && x.Substring(1).Equals(y.Substring(1)))
                return true;
            else
                return x.Equals(y);
        }
    }
}
