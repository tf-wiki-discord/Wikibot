using Microsoft.Extensions.Configuration;
using System;
using Wikibot.Logic.Jobs;
using Wikibot.Logic.Logic;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.JobRetrievers;

namespace Wikibot.Logic.Factories
{
    public static class WikiJobFactory
    {
        public static WikiJob GetWikiJob(WikiJobRequest request, Serilog.ILogger log, IWikiAccessLogic wikiAccessLogic, IConfiguration config, RequestData jobData, IWikiRequestRetriever retriever)
        {

            WikiJob job;
            var throttleSpeedInSeconds = int.Parse(config["ThreadThrottleSpeedInSeconds"]);

            switch (request.JobType)
            {
                case JobType.TextReplacementJob:
                    job = new TextReplacementJob(log, wikiAccessLogic, retriever, jobData, throttleSpeedInSeconds);
                    break;
                case JobType.LinkFixJob:
                    job = new LinkFixJob(log, wikiAccessLogic, retriever, jobData, throttleSpeedInSeconds);
                    break;
                case JobType.ContinuityLinkFixJob:
                    job = new ContinuityLinkFixJob(log, wikiAccessLogic, retriever, jobData, throttleSpeedInSeconds);
                    break;
                default:
                    throw new Exception("Job type is undefined");
            }

            job.Configuration = config;
            job.JobData = jobData;
            job.Request = request;
            job.Log = log;

            return job;

        }
    }
}
