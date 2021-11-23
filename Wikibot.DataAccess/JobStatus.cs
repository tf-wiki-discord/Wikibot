namespace Wikibot.DataAccess
{
    public enum JobStatus
    {
        ToBeProcessed,
        PendingPreApproval,
        PreApproved,
        PendingApproval,
        Approved,
        Rejected,
        Done,
        Failed,
        Processing
    }
}
