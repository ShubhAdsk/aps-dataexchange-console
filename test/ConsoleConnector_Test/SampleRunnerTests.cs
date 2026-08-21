using System.Threading.Tasks;
using Autodesk.DataExchange.Interface;
using ConsoleConnector.Driver;
using ConsoleConnector.Samples;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ConsoleConnector_Test
{
    [TestClass]
    public class SampleRunnerTests
    {
        // No sample categories are registered yet at this point in the migration (they land in
        // later stacked PRs), so RunAllAsync necessarily iterates zero samples here — this locks
        // in that "nothing registered" behaves as a clean no-op rather than a crash.
        [TestMethod]
        public async Task RunAllAsync_NoSamplesRegistered_ReturnsEmptyResultWithoutThrowing()
        {
            var ctx = new SampleContext(new Mock<IClient>().Object, new Defaults());
            var session = new SessionData();

            var result = await SampleRunner.RunAllAsync(ctx, session);

            Assert.AreEqual(0, result.Results.Count);
            Assert.AreEqual(0, result.Passed);
            Assert.AreEqual(0, result.Failed);
        }
    }
}
