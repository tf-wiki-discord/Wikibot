CREATE PROCEDURE [dbo].[spCreateUpdateWikiJobRequest]
	@Comment NVARCHAR(1000),
	@Username NVARCHAR(50),
	@Status INT,
	@RawRequest NVARCHAR(MAX),
	@SubmittedDate DateTime2(7),
	@JobType NVARCHAR(100),
	@ID BIGINT Output
AS
BEGIN
	IF(@ID = 0)
	BEGIN
		SET @ID = (SELECT Id FROM [dbo].WikiJobRequest WHERE RawRequest = @RawRequest)
	END
	IF EXISTS (SELECT Id FROM [dbo].WikiJobRequest WHERE Id = @ID)
		BEGIN
			UPDATE [dbo].WikiJobRequest SET Comment = @Comment, RequestingUsername = @Username, StatusID = @Status, RawRequest = @RawRequest, SubmittedDateUTC = @SubmittedDate, JobType = @JobType
			WHERE Id = @ID

			SELECT @ID
		END
	ELSE
		BEGIN

			INSERT INTO [dbo].WikiJobRequest (Comment, StatusID, RequestingUsername, RawRequest, SubmittedDateUTC, JobType)
			VALUES (@Comment, @Status, @Username, @RawRequest, @SubmittedDate, @JobType)

			SELECT @ID = SCOPE_IDENTITY()
		END
END