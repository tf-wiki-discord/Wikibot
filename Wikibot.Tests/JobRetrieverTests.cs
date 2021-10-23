using Moq;
using System.Threading;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Extensions;
using Wikibot.Logic.FileManagers;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Jobs;
using Wikibot.Logic.Logic;
using Wikibot.Logic.UserRetrievers;
using WikiClientLibrary;
using Xunit;
using Xunit.Abstractions;

namespace Wikibot.Tests
{
    public class JobRetrieverTests
    {
        private ITestOutputHelper _output;
        public JobRetrieverTests(ITestOutputHelper output)
        {
            _output = output;
        }

        // [Fact]
        // public void Startup()
        // {
        //     String[] args = new string[0];
        //     var hostBuilder = Host.CreateDefaultBuilder(args)
        //         .ConfigureWebHostDefaults(webBuilder =>
        //         {
        //             webBuilder.UseStartup<Startup>();
        //         });
        //     //var startup = new Startup(config);
        //     Assert.NotNull(hostBuilder.Build());
        // }



        [Fact]
        public void GetNewJobDefinitionsTextFile()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var textFileManager = new TextFileManager();
            var retriever = new TextFileJobRetriever(iConfig, "WikiJobTest.txt", textFileManager);
            Assert.NotNull(retriever.GetNewJobDefinitions().Result);
        }

        [Fact]
        public void RunJobRetrievalJobWithTextFileRetriever()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var requestData = Utilities.GetRequestData(null);
            var logger = Utilities.GetLogger(iConfig, _output);
            var wikiAccessLogic = new WikiAccessLogic(iConfig, logger);
            var userRetriever = new TFWikiUserRetriever(wikiAccessLogic);
            var textFileManager = new TextFileManager();
            var retriever = new TextFileJobRetriever(iConfig, "WikiJobTest.txt",textFileManager);
            
            var job = new JobRetrievalJob(iConfig, logger, retriever, userRetriever, requestData);
            job.Execute();
        }

        [Fact]
        public void GetNewJobDefinitionsWiki()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var logger = Utilities.GetLogger(iConfig, _output);
            var wikiAccessLogic = new WikiAccessLogic(iConfig,logger);
            var sqlDataAccess = new SqlDataAccess(iConfig);
            var userRetriever = new TFWikiUserRetriever(wikiAccessLogic);
            var retriever = new TFWikRequestRetriever(iConfig, logger, sqlDataAccess);
            var definitions = retriever.GetNewJobDefinitions().Result;
            Assert.NotNull(definitions);
        }

        [Fact]
        public void RunJobRetrievalJobWithTFWikiRetriever()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var requestData = Utilities.GetRequestData(null);
            var logger = Utilities.GetLogger(iConfig, _output);
            var wikiAccessLogic = new WikiAccessLogic(iConfig, logger);
            var sqlDataAccess = new SqlDataAccess(iConfig);
            var userRetriever = new TFWikiUserRetriever(wikiAccessLogic);
            var retriever = new TFWikRequestRetriever(iConfig, logger, sqlDataAccess);
            var job = new JobRetrievalJob(iConfig, logger, retriever, userRetriever, requestData);
            job.Execute();
        }

        [Fact]
        public void TestSearchResults()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var wiki = Utilities.GetWiki(iConfig);
            var results = from s in wiki.Query.search("Optimus Prime")
            select new { s.title, snippet = s.snippet };
            var count1 = results.ToList().Count;
            var resultlist = results.ToList();

            var wikiSite = Utilities.GetWikiSite(iConfig);
            var results2 = wikiSite.OpenSearchAsync("Optimus Prime").Result;
            var count2 = results2.Count;

            var result3 = wikiSite.Search("{{Deceptitran", 10, BuiltInNamespaces.Main, WikiSiteExtension.SearchOptions.text, CancellationToken.None).Result;

            Assert.True(count1 > count2);
        }
    }
}
