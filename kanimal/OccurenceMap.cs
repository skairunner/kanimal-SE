using System.Collections.Generic;

namespace kanimal
{
    using AnimHashTable = Dictionary<int, string>;

    public class OccurenceMap : Dictionary<string, int>
    {
        public void Update(KAnim.Element element, AnimHashTable animHashes)
        {
            var name = element.FindName(animHashes);
            if (!ContainsKey(name))
                this[name] = 0;
            else
                this[name] += 1;
        }

        public string FindOccurenceName(KAnim.Element element, AnimHashTable animHashes)
        {
            var name = element.FindName(animHashes);
            return $"{name}_{this[name]}";
        }
    }
}