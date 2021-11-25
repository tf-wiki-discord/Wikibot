CREATE PROCEDURE [dbo].[spCreateWikiJobRequest]
	@Comment NVARCHAR(1000),
	@Username NVARCHAR(50),
	@Status INT,
	@RawRequest NVARCHAR(MAX),
	@SubmittedDate DateTime2(7),
	@JobType NVARCHAR(100),
	@ID BIGINT Output
AS
BEGIN

	INSERT INTO [dbo].WikiJobRequest (Comment, StatusID, RequestingUsername, RawRequest, SubmittedDateUTC, JobType)
	VALUES (@Comment, @Status, @Username, @RawRequest, @SubmittedDate, @JobType)

	SELECT @ID = SCOPE_IDENTITY()

END