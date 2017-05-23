using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace MediaSvcsIndexer2Demo
{
    public static class LoggerExtensions
    {
        public static object ToLog(this IJob obj)
        {
            return new { obj.Name, obj.Id };
        }
        public static object ToLog(this IAsset obj)
        {
            return new {obj.Name, obj.Id, ContentKeyCount = obj.ContentKeys?.Count ?? 0};
        }
        public static object ToLog(this IAssetFile obj)
        {
            return new {obj.Name, obj.Id, obj.EncryptionScheme, obj.IsEncrypted};
        }
        public static object ToLog(this ITask obj)
        {
            return new { obj.Name, obj.Id, obj.EncryptionScheme };
        }
    }

    //public static class ExtensionMethods
    //{
    //    // Deep clone
    //    public static T DeepClone<T>(this T a)
    //    {
    //        using (MemoryStream stream = new MemoryStream())
    //        {
    //            BinaryFormatter formatter = new BinaryFormatter();
    //            formatter.Serialize(stream, a);
    //            stream.Position = 0;
    //            return (T)formatter.Deserialize(stream);
    //        }
    //    }
    //}
}