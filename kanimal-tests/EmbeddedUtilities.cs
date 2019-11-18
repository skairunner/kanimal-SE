using System.IO;
using System.Reflection;

namespace kanimal_tests
{
    public static class EmbeddedUtilities
    {
        private static Assembly assembly = Assembly.GetExecutingAssembly();
        public static Stream GetResource(string resourceName)
        {
            return assembly.GetManifestResourceStream(resourceName);
        }
    }
}