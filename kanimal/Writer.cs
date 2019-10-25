using System.Collections.Generic;

namespace kanimal
{
    // TODO: Move code that can be reused in all writers from SCMLWriter to Writer
    public abstract class Writer
    {
        protected List<KBuild.Row> BuildTable;
        protected KBuild.Build BuildData;
        protected KAnim.Anim AnimData;
        protected Dictionary<int, string> AnimHashes;
        protected Dictionary<string, string> FilenameIndex;

        public abstract void Init(KBuild.Build buildData, List<KBuild.Row> buildTable, KAnim.Anim animData,
            Dictionary<int, string> animHashes);

        public abstract void Save(string path);
    }
}