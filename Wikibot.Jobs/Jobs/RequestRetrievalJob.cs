using FluentScheduler;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.JobRetrievers;
using Wikibot.Logic.Logic;
using Wikibot.Logic.UserRetrievers;

namespace Wikibot.Logic.Jobs
{
    public class RequestRetrievalJob : AbstractJob
    {

        private IWikiRequestRetriever _requestRetriever;
        private IUserRetriever _userRetriever;
        private INotificationService _notificationService;

        public RequestRetrievalJob(IConfiguration config, Serilog.ILogger log, IWikiRequestRetriever requestRetriever, IUserRetriever userRetriever, INotificationService notifcationService, RequestData jobData )
        {
            Configuration = config;
            Log = log;
            _requestRetriever = requestRetriever;
            _userRetriever = userRetriever;
            _notificationService = notifcationService;
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
                var requests = _requestRetriever.RequestDefinitions?.Where(request => statusesToProcess.Contains(request.Status)).ToList();

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
                                JobData.CreateWikiJobRequest(request);

                                //Add to update list
                                requestsToUpdate.Add(request);

                                //Send notification
                                _notificationService.SendNewRequestNotification(_userRetriever.GetReviewerUsers(), request.RequestingUsername, request.Comment);
                            }
                            else
                            {
                                Log.Information("Request has been previously approved or preapproved");
                                var existingRequest = JobData.GetWikiJobRequestByID(request.ID);
                                requestIsValid = requestIsValid && request.RawRequest.Equals(existingRequest.RawRequest);
                                Log.Information($"Existing request {existingRequest.RawRequest} equals is {requestIsValid}");


                                //If the request matches an existing one and the status is either Approved or PreApproved
                                // i.e. the job isn't pending approval of some sort, nor is it currently processing
                                if (requestIsValid && (existingRequest.Status == JobStatus.Approved || existingRequest.Status == JobStatus.PreApproved))
                                {
                                    Log.Information("Scheduling request");
                                    //Schedule jobs in 10 minute intervals
                                    //How to deal with potential page edit overlaps? -> Check page lists and id overlaps;
                                    var job = _requestRetriever.GetJobForRequest(request);
                                    JobData.UpdateStatus(request.ID, JobStatus.Processing);
                                    JobManager.AddJob(() => job.Execute(), (s) => s.ToRunOnceIn(runin + offset).Minutes());
                                    offset = offset + 10;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Error processing request {request.RawRequest}:");
                        }
                    }

                    Log.Information("Saving requests");
                    //Update job status on the wiki page the job was retreived from
                    _requestRetriever.UpdateRequests(requestsToUpdate);


                }
                else
                {
                    Log.Information("No requests found.");
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

    }
}