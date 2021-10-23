using FluentScheduler;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Logic;
using Wikibot.Logic.UserRetrievers;

namespace Wikibot.Logic.Jobs
{
    public class JobRetrievalJob : AbstractJob
    {

        private IWikiRequestRetriever _jobRetriever;
        private IUserRetriever _userRetriever;

        public JobRetrievalJob(IConfiguration config, Serilog.ILogger log, IWikiRequestRetriever jobRetriever, IUserRetriever userRetriever, RequestData jobData )
        {
            Configuration = config;
            Log = log;
            _jobRetriever = jobRetriever;
            _userRetriever = userRetriever;

            JobData = jobData;
        }

        public override void Execute() {
            Log.Information("Job retrieval job starting");

            var jobApprovalLogic = new JobApprovalLogic(_userRetriever);
            int offset = 0;
            int runin = 5;
            List<JobStatus> statusesToProcess = new List<JobStatus>() { JobStatus.ToBeProcessed, JobStatus.PreApproved, JobStatus.Approved };

            try
            {
                bool requestIsValid = true;
                var requestsToUpdate = new List<WikiJobRequest>();
                //Get job definitions
                var requests = _jobRetriever.JobDefinitions?.Where(request => statusesToProcess.Contains(request.Status)).ToList();

                if (requests != null && requests.Count > 0)
                {
                    Log.Information($"Processing {requests.Count} requests");
                    foreach (WikiJobRequest request in requests)
                    {
                        Log.Information($"Processing retrieved request: {request.RawRequest}");
                        try
                        {
                            requestIsValid = true;
                            if (request.Status == JobStatus.ToBeProcessed)
                            {
                                Log.Information("Request is ToBeProcessed");
                                //Check For Automatic Approval
                                CheckForUserApproval(request, jobApprovalLogic);

                                //Save Job
                                JobData.SaveWikiJobRequest(request);

                                //Add to update list
                                requestsToUpdate.Add(request);
                            }
                            else
                            {
                                Log.Information("Request has been previously approved or preapproved");
                                var existingRequest = JobData.GetWikiJobRequestByID(request.ID);
                                requestIsValid = requestIsValid && request.RawRequest.Equals(existingRequest.RawRequest);
                                Log.Information($"Existing request {existingRequest.RawRequest} equals is {requestIsValid}");
                            }

                            if (requestIsValid && (request.Status == JobStatus.Approved || request.Status == JobStatus.PreApproved))
                            {
                                Log.Information("Scheduling request");
                                //Schedule jobs in 10 minute intervals
                                //How to deal with potential page edit overlaps? -> Check page lists and id overlaps;
                                var job = _jobRetriever.GetJobForRequest(request);
                                JobManager.AddJob(() => job.Execute(), (s) => s.ToRunOnceIn(runin + offset).Minutes());
                                offset = offset + 10;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Error processing request {request.RawRequest}:");
                        }
                    }

                    Log.Information("Saving requests");
                    //Update job status on the wiki page the job was retreived from
                    _jobRetriever.UpdateRequests(requestsToUpdate);


                }
                else
                {
                    Log.Information("No requests found.");
                }
                string keepAliveURL = Configuration["KeepAliveURL"];
                if (!string.IsNullOrEmpty(keepAliveURL))
                {
                    Log.Information("Keep Alive");
                    HttpGet(keepAliveURL); //hack to keep IIS server alive
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex, "Error occurred while running JobRetrievalJob:");
            }

        }

        private void CheckForUserApproval(WikiJobRequest request, JobApprovalLogic jobApprovalLogic)
        {
            var user = _userRetriever.GetUser(request.RequestingUsername);

            if (jobApprovalLogic.IsUserAutoApproved(user))
            {
                request.Status = JobStatus.PreApproved;
            }
            else
            {
                request.Status = JobStatus.PendingPreApproval;
            }
        }

        private void HttpGet(string serverURI)
        {
            WebClient client = new WebClient();

            // Add a user agent header in case the 
            // requested URI contains a query.

            client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)");

            Stream data = client.OpenRead(serverURI);
        }
    }
}