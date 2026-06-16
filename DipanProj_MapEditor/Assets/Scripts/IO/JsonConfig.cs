using Newtonsoft.Json;

namespace DipanMapEditor.IO
{
    /// <summary>全專案共用的 Newtonsoft 設定（縮排、略過 null、列舉轉字串）。</summary>
    public static class JsonConfig
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
        };

        public static string Serialize(object obj)
            => JsonConvert.SerializeObject(obj, Settings);

        public static T Deserialize<T>(string json)
            => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
