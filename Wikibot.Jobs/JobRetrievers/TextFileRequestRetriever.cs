using Microsoft.Extensions.Configuration;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Extensions;
using Wikibot.Logic.Factories;
using Wikibot.Logic.Jobs;
using Wikibot.Logic.Logic;

namespace Wikibot.Logic.JobRetrievers
{
    public class TextFileRequestRetriever : IWikiRequestRetriever
    {
        private List<WikiJobRequest> _requestDefinitions;
        private string _pathToTextFile;
        private readonly IConfiguration _config;
        private string _botRequestTemplate;
        private IFileManager _fileManager;

        public List<WikiJobRequest> RequestDefinitions 
        {
            get {
                if (_requestDefinitions == null)
                    _requestDefinitions = GetNewJobDefinitions().Result;
                return _requestDefinitions;
            }
        }
        
        public TextFileRequestRetriever(IConfiguration configuration, string pathToTextFile, IFileManager fileManager)
        {
            _pathToTextFile = pathToTextFile;
            _config = configuration;
            _botRequestTemplate = configuration["BotRequestTemplate"];
            _fileManager = fileManager;
        }

        public async Task<List<WikiJobRequest>> GetNewJobDefinitions()
        {
            var ast = await parseFile();
            var templates = ast.EnumDescendants().OfType<Template>();
            var jobs = templates.Select(template => WikiJobRequestFactory.GetWikiJobRequest(JobType.TextReplacementJob, GetTimeZone(), template));
            return jobs.ToList();
        }

        public void UpdateRequests(List<WikiJobRequest> jobs)
        {
            var wikiText = parseFile().Result;
            foreach(WikiJobRequest job in jobs)
            {
                var templates = wikiText.EnumDescendants().OfType<Template>();
                var singletemplate = templates.FirstOrDefault(x => x.Name.ToPlainText().Equals(_botRequestTemplate) && x.EqualsJob(job));
                if (singletemplate != null)
                {
                    singletemplate.Arguments.Single(arg => arg.Name.ToPlainText().Equals("status")).Value = new WikitextParser().Parse(job.Status.ToString());
                }
            }
            _fileManager.WriteAllText(_pathToTextFile, wikiText.ToString());
        }

        private async Task<Wikitext> parseFile()
        { 
           string contents = await _fileManager.ReadAllTextAsync(_pathToTextFile);
           return new WikitextParser().Parse(contents);
        }

        private TimeZoneInfo GetTimeZone()
        {
            return TimeZoneInfo.FindSystemTimeZoneById(_config["RequestTimezoneID"]);
        }

        public WikiJob GetJobForRequest(WikiJobRequest request)
        {
            return WikiJobFactory.GetWikiJob(request, null, new WikiAccessLogic(_config, null), _config, null, null, this);
        }
    }
}