using Microsoft.Extensions.Configuration;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.UserRetrievers;

namespace Wikibot.Logic.Jobs
{
    public abstract class WikiJob : AbstractJob
    {
        public WikiJobRequest Request { get; set; }

        public IConfigurationSection WikiConfig
        {
            get
            {
                return Configuration.GetSection("WikiLogin");
            }
        }

        public INotificationService Notifier { get; set; }

        private string _fromText;
        public string FromText 
        { 
            get
            {
                if (_fromText == null)
                {
                    var template = new WikitextParser().Parse(Request.RawRequest).Lines.SelectMany(x => x.EnumDescendants().OfType<Template>()).Single();
                    _fromText =  template.Arguments.Single(arg => arg.Name.ToPlainText() == "before").Value.ToString().Trim();
                }
                return _fromText;
            }
        }

        private string _toText;
        public string ToText
        {
            get
            {
                if (_toText == null)
                {
                    var template = new WikitextParser().Parse(Request.RawRequest).Lines.SelectMany(x => x.EnumDescendants().OfType<Template>()).Single();
                    _toText = template.Arguments.Single(arg => arg.Name.ToPlainText() == "after").Value.ToString().Trim();
                }
                return _toText;
            } 
            
        }

        private string[] _headersToSearch;
        public string[] HeadersToSearch
        {
            get
            {
                if (_headersToSearch == null)
                {
                    var template = new WikitextParser().Parse(Request.RawRequest).Lines.SelectMany(x => x.EnumDescendants().OfType<Template>()).Single();
                    _headersToSearch = (template.Arguments.SingleOrDefault(arg => arg.Name.ToPlainText() == "headers")?.Value.ToString() ?? "").Split(",").Select(token => token.Trim()).ToArray();

                }
                return _headersToSearch;
            }

        }


        private MediaType? _media;
        public MediaType Media
        {
            get
            {
                if (_media == null)
                {
                    var template = new WikitextParser().Parse(Request.RawRequest).Lines.SelectMany(x => x.EnumDescendants().OfType<Template>()).Single();
                    _media = (MediaType)Enum.Parse(typeof(MediaType), template.Arguments.Single(arg => arg.Name.ToPlainText() == "media").Value.ToString());
                }
                return _media.Value;
            }

        }

        public void SetJobStart()
        {
            if (Request.Status == JobStatus.PreApproved)
            {
                Request.TimePreStartedUTC = DateTime.UtcNow;
                Log.Information("Job started at {DateTime}", Request.TimePreStartedUTC);
            }
            else
            {
                Request.TimeStartedUTC = DateTime.UtcNow;
                Log.Information("Job started at {DateTime}", Request.TimeStartedUTC);
            }
        }

        public void SetJobEnd()
        {
            if (Request.Status == JobStatus.PreApproved)
            {
                Request.TimePreFinishedUTC = DateTime.UtcNow;
                Request.Status = JobStatus.PendingApproval;
                Log.Information("Job ended at {DateTime}", Request.TimePreFinishedUTC);
                Notifier.SendNewApprovalNotification(UserRetriever.GetReviewerUsers(), this.Request.RequestingUsername, this.Request.Comment);
            }
            else if (Request.Status == JobStatus.Approved)
            {
                Request.TimeFinishedUTC = DateTime.UtcNow;
                Request.Status = JobStatus.Done;
                Log.Information("Job ended at {DateTime}", Request.TimeFinishedUTC);
                Notifier.SendRequestCompletedNotification(this.Request.RequestingUsername, this.Request.Comment, this.Request.JobType.ToString());
            }

        }

        public void FailJob(Exception ex)
        {
            Request.Status = JobStatus.Failed;
            Notifier.SendErrorNotification(UserRetriever.GetReviewerUsers(), Request.RequestingUsername, Request.Comment);
            Log.Error(ex, $"TextReplacementJob with ID: {Request.ID} failed.");
        }

        public void SaveRequest()
        {
            Log.Information("Saving job.");

            JobData.UpdateStatus(this.Request.ID, this.Request.Status);
            Retriever.UpdateRequests(new List<WikiJobRequest> { Request });
        }

        public IWikiRequestRetriever Retriever { get; set; }
        public IUserRetriever UserRetriever { get; set; }
    }
}
