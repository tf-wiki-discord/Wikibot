using Dapper;
using Moq;
using MwParserFromScratch;
using MwParserFromScratch.Nodes;
using System;
using System.Collections.Generic;
using System.Linq;
using Wikibot.DataAccess;
using Wikibot.DataAccess.Objects;
using Wikibot.Logic.Factories;
using Xunit;

namespace Wikibot.Tests
{
    public class DatabaseIntegrationTests
    {
        private WikiJobRequest BuildRequest()
        {
            var parser = new WikitextParser();
            var ast = parser.Parse("{{User:Tigerpaw28/Sandbox/Template:WikiBotRequest|type=Text Replacement|username=Tigerpaw28|timestamp=14:58, 30 June 2020 (EDT)|before=Deceptitran|after=not a Robot|comment=Test job|status=PendingPreApproval}}");
            var templates = ast.Lines.First<LineNode>().EnumDescendants().OfType<Template>();
            var request = WikiJobRequestFactory.GetWikiJobRequest(JobType.TextReplacementJob, TimeZoneInfo.Local, templates.First());
            List<Page> pages = new List<Page>();
            pages.Add(new Page(0, "Test"));
            pages.Add(new Page(1, "Commercial"));
            request.Pages = pages;
            return request;
        }

        [Fact]
        public void SaveRequest()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var request = BuildRequest();

            var r = new DynamicParameters();
            r.Add("@Comment", request.Comment, System.Data.DbType.String);
            r.Add("@JobType", request.JobType, System.Data.DbType.String);
            r.Add("@RawRequest", request.RawRequest, System.Data.DbType.String);
            r.Add("@Status", request.Status, System.Data.DbType.Int32);
            r.Add("@SubmittedDate", request.SubmittedDateUTC, System.Data.DbType.DateTime2);
            r.Add("@Username", request.RequestingUsername, System.Data.DbType.String);
            r.Add("@ID", request.ID, System.Data.DbType.Int64, System.Data.ParameterDirection.Output);

            r.RemoveUnused = true;

            requestData.CreateWikiJobRequest(request);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spCreateWikiJobRequest", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, r)), "JobDb"), Times.Exactly(1));
        }

        [Fact]
        public void GetJobs()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var jobList = Utilities.GetRequestData(mockDataAccess.Object).GetWikiJobRequests();
            Assert.NotEmpty(jobList);
        }

        [Fact]
        public void GetJobs2()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var jobList = Utilities.GetRequestData(mockDataAccess.Object).GetWikiJobRequestsWithPages(1,10,"ASC","ID");
            Assert.NotEmpty(jobList);
        }

        [Fact]
        public void GetJobs3()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var jobList = requestData.GetWikiJobRequestsForApproval(1, 10, "ASC", "ID");
            Assert.NotEmpty(jobList);
        }

        [Fact]
        public void GetWikiJobRequestByID()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var requestList = requestData.GetWikiJobRequestByID(2);
            Assert.NotNull(requestList);
        }

        [Fact]
        public void UpdateRequestStatus()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var requestList = requestData.GetWikiJobRequests();
            var request = requestList.First();
            request.Status = JobStatus.PreApproved;            
            requestData.UpdateStatus(request.ID, request.Status);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spUpdateWikiJobRequestStatus", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, Utilities.GetUpdateStatusParams())), "JobDb"), Times.Exactly(1));
        }

        [Fact]
        public void UpdateRequestTimePreStarted()
        {
            var now = DateTime.UtcNow; 
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var requestList = requestData.GetWikiJobRequests();
            var request = requestList.First();      
            requestData.UpdateTimePreStarted(request.ID, now);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spUpdateWikiJobRequestTimePreStarted", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, Utilities.GetTimePreStartParams(now))), "JobDb"), Times.Exactly(1));
        }

        [Fact]
        public void UpdateRequestTimeStarted()
        {   
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var requestList = requestData.GetWikiJobRequests();
            var request = requestList.First();
            requestData.UpdateTimeStarted(request.ID, now);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spUpdateWikiJobRequestTimeStarted", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, Utilities.GetTimeStartParams(now))), "JobDb"), Times.Exactly(1));

        }

        [Fact]
        public void UpdateRequestTimePreFinished()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var requestList = requestData.GetWikiJobRequests();
            var request = requestList.First();        
            requestData.UpdateTimePreFinished(request.ID, now);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spUpdateWikiJobRequestTimePreFinished", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, Utilities.GetTimePreFinishParams(now))), "JobDb"), Times.Exactly(1));

        }

        [Fact]
        public void UpdateRequestTimeFinished()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var requestList = requestData.GetWikiJobRequests();
            var request = requestList.First();
            
            requestData.UpdateTimeFinished(request.ID, now);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spUpdateWikiJobRequestTimeFinished", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, Utilities.GetTimeFinishParams(now))), "JobDb"), Times.Exactly(1));

        }

        [Fact]
        public void UpdatePages()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var requestData = Utilities.GetRequestData(mockDataAccess.Object);
            var requestList = requestData.GetWikiJobRequests();
            var request = requestList.First();
            List<Page> pages = new List<Page>();
            pages.Add(new Page(0, "NewPage"));
            var pageData = new PageData(mockDataAccess.Object);
            pageData.UpdatePagesForWikiJobRequest(pages, request.ID);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spUpdatePagesForWikiJobRequest", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, Utilities.GetUpdatePageParams(pages,request.ID))), "JobDb"), Times.Exactly(1));
        }


        [Fact]
        public void SaveReviewComment()
        {
            var now = DateTime.Now;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var reviewCommentData = Utilities.GetReviewCommentData(mockDataAccess.Object);
            var request = BuildRequest();

            
            var r = new
            {
                requestId = 1,
                comment = "Sample comment",
                timestamp = now
            };

            reviewCommentData.AddComment(1, "Sample comment", now);
            mockDataAccess.Verify(dataAccess => dataAccess.SaveData<dynamic>("dbo.spCreateReviewComment", It.Is<object>(y => VerifyHelper.AreEqualObjects(y, Utilities.GetAddCommentParams(1, "Sample comment", now))), "JobDb"), Times.Exactly(1));
        }

        [Fact]
        public void GetReviewComments()
        {
            var now = DateTime.UtcNow;
            var mockDataAccess = Utilities.GetMockDataAccess(now);
            var reviewCommentData = Utilities.GetReviewCommentData(mockDataAccess.Object);

            var reviewComment = reviewCommentData.GetMostRecentCommentForRequest(1);
            Assert.NotNull(reviewComment);
        }
    }
}
