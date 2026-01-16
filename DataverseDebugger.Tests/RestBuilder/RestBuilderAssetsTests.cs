using System.IO;

namespace DataverseDebugger.Tests.RestBuilder
{
    [TestClass]
    public sealed class RestBuilderAssetsTests
    {
        [TestMethod]
        public void RestBuilderIndexExists()
        {
            var root = FindRepoRoot();
            var path = Path.Combine(root, "DataverseDebugger.RestBuilder", "index.html");

            Assert.IsTrue(File.Exists(path), $"Expected file at {path}");
        }

        [TestMethod]
        public void RestBuilderCustomScriptExists()
        {
            var root = FindRepoRoot();
            var path = Path.Combine(root, "DataverseDebugger.RestBuilder", "drb_custom.js");

            Assert.IsTrue(File.Exists(path), $"Expected file at {path}");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 8 && dir != null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "DataverseDebugger.sln");
                if (File.Exists(candidate))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            Assert.Fail("Repository root not found.");
            return string.Empty;
        }
    }
}
