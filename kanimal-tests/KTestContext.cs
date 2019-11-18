using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using kanimal;

namespace kanimal_tests
{
    // A virtual scenario of memorystreams to substitute for actual files
    public class KTestContext
    {
        
    }

    public class ScmlContext : KTestContext
    {
        public Stream Scml;
        public Dictionary<string, Bitmap> Sprites;

        // Construct an ScmlContext given a directory (aka namespace) for
        // files embedded into the assembly.
        public ScmlContext(string namespaceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var files = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith("scml") || name.EndsWith("png"));
            Sprites = new Dictionary<string, Bitmap>();
            foreach (var filename in files)
            {
                if (filename.EndsWith("scml"))
                {
                    Scml = EmbeddedUtilities.GetResource(filename);
                }
                else
                {
                    // Need to get the base filename by chopping off the namespace
                    var realName = filename.Substring("kanimal_tests.".Length + namespaceName.Length + 1);
                    Sprites[Utilities.WithoutExtension(realName)] =
                        new Bitmap(EmbeddedUtilities.GetResource(filename));
                }
            }
        }
    }
}