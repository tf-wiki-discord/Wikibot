using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic;
using Wikibot.Logic.JobRetrievers;

namespace Wikibot.App.Controllers
{
    [Produces("application/json")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "BotAdmin")]
    [Route("api/[controller]")]
    [ApiController]
    public class RequestController : ControllerBase
    {
        private RequestData _requestData;
        private string diffFileNamePattern = "";
        private IWikiRequestRetriever _jobRetriever;
        public RequestController(IDataAccess dataAccess, IWikiRequestRetriever jobRetriever, IConfiguration config)
        {
            _requestData = new RequestData(dataAccess);
            diffFileNamePattern = config["DiffFileNamePattern"];
            _jobRetriever = jobRetriever;
        }

        //Get Requests
        [HttpGet("requests")]
        public IActionResult GetRequests()
        {
            var config = new MapperConfiguration(cfg => cfg.CreateMap<WikiJobRequest, Wikibot.App.Models.WikiJobRequest>()
                .ForMember(dest => dest.Diffs, opt => opt.MapFrom(src =>src.Pages.Select(x=>  Utilities.SanitizeFilename(string.Format(diffFileNamePattern, src.ID, x.Name),'_'))))
                .ForMember(dest => dest.RequestingUsername, opt=> opt.MapFrom(src => src.RequestingUsername))
                .ForMember(dest => dest.StatusName, opt=> opt.MapFrom(src => src.Status))
                );
            var requestList = _requestData.GetWikiJobRequestsForApproval(1, 200, "ASC", "ID");
            var mapper = new Mapper(config);
            var modelList = mapper.Map<List<WikiJobRequest>, List<Models.WikiJobRequest>>(requestList);
            return new OkObjectResult(modelList);
        }

        //Pre Approve Request
        [HttpPost("preapprove")]
        public IActionResult PreApproveRequest(int requestId)
        {
            _requestData.UpdateStatus(requestId, JobStatus.PreApproved);
            var requests = _requestData.GetWikiJobRequestByID(requestId);
            _jobRetriever.UpdateRequests(new List<WikiJobRequest> { requests });
            return new OkObjectResult("Request status successfully updated");
        }

        //Approve Request
        [HttpPost("approve")]
        public IActionResult ApproveRequest(int requestId)
        {
            _requestData.UpdateStatus(requestId, JobStatus.Approved);
            var requests = _requestData.GetWikiJobRequestByID(requestId);
            _jobRetriever.UpdateRequests(new List<WikiJobRequest> { requests });
            return new OkObjectResult("Request status successfully updated");   
        }
        //Reject Request
        [HttpPost("reject")]
        public IActionResult RejectRequest(int requestId)
        {
            _requestData.UpdateStatus(requestId, JobStatus.Rejected);
            var requests = _requestData.GetWikiJobRequestByID(requestId);
            _jobRetriever.UpdateRequests(new List<WikiJobRequest> { requests });
            return new OkObjectResult("Request status successfully updated");
        }
    }
}
