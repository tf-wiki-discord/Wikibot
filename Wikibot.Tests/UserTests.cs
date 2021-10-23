using Xunit;
using Xunit.Abstractions;

namespace Wikibot.Tests
{
    public class UserTests
    {
        private ITestOutputHelper _output;
        public UserTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void GetUser()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var userRetriever = Utilities.GetUserRetriever(iConfig, Utilities.GetLogger(iConfig, _output));
            var user = userRetriever.GetUser("Tigerpaw28");
            Assert.NotNull(user);
        }

        [Fact]
        public void GetAutoApprovedUsers()
        {
            var iConfig = Utilities.GetIConfigurationRoot();
            var userRetriever = Utilities.GetUserRetriever(iConfig, Utilities.GetLogger(iConfig, _output));
            var users = userRetriever.GetAutoApprovedUsers();
            Assert.NotNull(users);
            Assert.NotEmpty(users);
        }
    }
}
