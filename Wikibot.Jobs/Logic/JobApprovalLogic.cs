﻿using System.Linq;
using Wikibot.DataAccess;
using Wikibot.Logic.UserRetrievers;

namespace Wikibot.Logic.Logic
{
    public class JobApprovalLogic
    {
        private IUserRetriever _userRetriever;
        public JobApprovalLogic(IUserRetriever userRetriever)
        {
            _userRetriever = userRetriever;
        }
        public bool IsUserAutoApproved(User userToValidate)
        {
            return userToValidate.Username.Equals("Tigerpaw28") || _userRetriever.GetAutoApprovedUsers().Where(user => user == userToValidate).Any(); //TODO: Remove my user as auto approved
        }

        public bool IsUserAuthentic(User userToValidate, string pageName)
        {
            bool isAuthentic = false;

            return isAuthentic;
        }
    }
}
