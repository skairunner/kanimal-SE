using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using NLog;

namespace kanimal
{
    // TODO: Move code that can be reused in all writers from SCMLWriter to Writer
    public abstract class Writer
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        protected List<KBuild.Row> BuildTable;
        protected KBuild.Build BuildData;
        protected Dictionary<int, string> BuildHashes;
        protected KAnim.Anim AnimData;
        protected Dictionary<int, string> AnimHashes;
        protected Dictionary<string, string> FilenameIndex;
        protected List<Sprite> Sprites;
        protected Reader Reader;

        public abstract OutputFiles Save();

        // Invoke Save, then output all artifacts to the provided path
        public abstract void SaveToDir(string path);
    }

    // It's just a filename to stream mapping.
    public class OutputFiles: Dictionary<string, Stream>
    {
        // Reset all stream seek heads to 0 from start
        public void Rewind()
        {
            foreach (var entry in this)
            {
                entry.Value.Seek(0, SeekOrigin.Begin);
            }
        }

        // Saves all files directly to path
        public void Yeet(string path)
        {
            Directory.CreateDirectory(path);
            Rewind();
            foreach (var entry in this)
            {
                using var file = new FileStream(Path.Join(path, entry.Key), FileMode.Create);
                entry.Value.CopyTo(file);
            }
        }
    }
}