using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Verse;

namespace MurderRimCore
{
    /// <summary>
    /// Simple string->float map with custom XML loader.
    /// </summary>
    public class StringFloatMap : IExposable
    {
        public Dictionary<string, float> data = new Dictionary<string, float>();

        public void ExposeData()
        {
            Scribe_Collections.Look(ref data, "data", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && data == null)
                data = new Dictionary<string, float>();
        }

        public float Get(string key, float fallback = 1f)
        {
            if (data == null || string.IsNullOrEmpty(key))
                return fallback;

            float value;
            if (data.TryGetValue(key, out value))
                return value;

            return fallback;
        }

        public bool TryGetValue(string key, out float value)
        {
            if (data == null)
            {
                value = 0f;
                return false;
            }

            return data.TryGetValue(key, out value);
        }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            if (data == null)
                data = new Dictionary<string, float>();

            foreach (XmlNode child in xmlRoot.ChildNodes)
            {
                if (child.NodeType != XmlNodeType.Element)
                    continue;

                string key = child.Name;
                if (string.IsNullOrEmpty(key))
                    continue;

                float value;
                try
                {
                    value = ParseHelper.FromString<float>(child.InnerText);
                }
                catch
                {
                    Log.Error("[MurderRimCore] Failed to parse float for key '" + key + "' in StringFloatMap.");
                    continue;
                }

                data[key] = value;
            }
        }
    }
}
