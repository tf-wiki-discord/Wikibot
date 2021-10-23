using Wikibot.Logic.Factories;
using Wikibot.Logic.FileManagers;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Jobs;
using Wikibot.Logic.Logic;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using System;

namespace Wikibot.Tests
{
    public class JobTests
    {
        private ITestOutputHelper _output;
        public JobTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ExecuteTextReplacementJob()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var logger = Utilities.GetLogger(iConfig, _output);
            var wikiAccessLogic = new WikiAccessLogic(iConfig,logger);
            var log = Utilities.GetLogger(iConfig, _output);
            var jobData = Utilities.GetRequestData(null);
            var request = Utilities.GetSampleJobRequest();
            var textFileManager = new TextFileManager();
            var jobRetriever = new TextFileJobRetriever(iConfig, "WikiJobTest.txt", textFileManager);
            TextReplacementJob job = (TextReplacementJob)WikiJobFactory.GetWikiJob(request, log, wikiAccessLogic, iConfig, jobData, jobRetriever);
            job.Configuration = iConfig;
            job.Execute();
        }

        [Fact]
        public void ExecuteLinkFixJob()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var logger = Utilities.GetLogger(iConfig, _output);
            var wikiAccessLogic = new WikiAccessLogic(iConfig, logger);
            var log = Utilities.GetLogger(iConfig, _output);
            var jobData = Utilities.GetRequestData(null);
            var request = Utilities.GetSampleLinkFixJobRequest();
            var textFileManager = new TextFileManager();
            var jobRetriever = new TextFileJobRetriever(iConfig, "WikiJobTest.txt", textFileManager);
            LinkFixJob job = (LinkFixJob)WikiJobFactory.GetWikiJob(request, log, wikiAccessLogic, iConfig, jobData, jobRetriever);
            job.Configuration = iConfig;
            job.Execute();
        }

        [Fact]
        public void ExecuteLinkFixJobLinkTextNotRetainedIfNoCustomText()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var logger = Utilities.GetLogger(iConfig, _output);
            var wikiAccessLogic = new WikiAccessLogic(iConfig, logger);
            var log = Utilities.GetLogger(iConfig, _output);
            var jobData = Utilities.GetRequestData(null);
            var request = Utilities.GetSampleLinkFixJobRequest();
            var textFileManager = new TextFileManager();
            var jobRetriever = new TextFileJobRetriever(iConfig, "WikiJobTest.txt", textFileManager);
            LinkFixJob job = (LinkFixJob)WikiJobFactory.GetWikiJob(request, log, wikiAccessLogic, iConfig, jobData, jobRetriever);
            job.Configuration = iConfig;
            job.Execute();
        }

        [Fact]
        public void ExecuteContinuityLinkFixJob()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var logger = Utilities.GetLogger(iConfig, _output);
            var wikiAccessLogic = new WikiAccessLogic(iConfig, logger);
            var log = Utilities.GetLogger(iConfig, _output);
            var jobData = Utilities.GetRequestData(null);
            var request = Utilities.GetSampleContinuityLinkFixJobRequest();
            var textFileManager = new TextFileManager();
            var jobRetriever = new TextFileJobRetriever(iConfig, "WikiJobTest.txt", textFileManager);
            
            ContinuityLinkFixJob job = (ContinuityLinkFixJob)WikiJobFactory.GetWikiJob(request, log, wikiAccessLogic, iConfig, jobData, jobRetriever);
            job.Configuration = iConfig;
            job.Execute();
        }
    }
}
