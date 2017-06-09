using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MLSandboxPOC
{
    class MediaServicesUtils
    {
        private const string KeyIdentifierPrefix = "nb:kid:UUID:";

        private static readonly ILogger _logger = Logger.GetLog<MediaServicesUtils>();

        public static void RemoveEncryptionKey(IndexJobData data)
        {
            data.ContentKey = data.InputAsset.ContentKeys.Single(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
            data.ContentKeyData = data.ContentKey.GetClearKeyValue();
            data.InputAsset.ContentKeys.Remove(data.ContentKey);
            data.InputAsset.Update();

            _logger.Verbose("Removed encryption key {key} from asset {asset}", data.ContentKey.Id, data.InputAsset.ToLog());
        }

        //public static void RemoveEncryptionKey(CloudMediaContext context, IndexJobData data)
        //{
        //         data.ContentKey = data.InputAsset.ContentKeys.AsEnumerable()
        //            .FirstOrDefault(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
        //        data.ContentKeyData = data.ContentKey.GetClearKeyValue();
        //        data.InputAsset.ContentKeys.Remove(data.ContentKey);
        //        data.InputAsset.Update();

        //    //var newKey = context.ContentKeys.Create(Guid.Parse(GuidFromId(data.ContentKey.Id)), data.ContentKeyData, data.ContentKey.Name, data.ContentKey.ContentKeyType);
        //    //data.ContentKey= newKey;
        //    //}

        //    //int c2 = context.ContentKeys.AsEnumerable().Count(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
        //    //var ck = context.ContentKeys.Where(k => k.Id == data.ContentKey.Id).ToArray();

        //    _logger.Verbose("Removed encryption key {key} from asset {asset}", data.ContentKey.Id, data.InputAsset.ToLog());
        //}


        public static void RestoreEncryptionKey(CloudMediaContext context, IndexJobData data)
        {
            _logger.Verbose("Restoring key {key} to asset {asset}", data.ContentKey.Id, data.InputAsset.ToLog());

            var newKey = context.ContentKeys.Create(Guid.Parse(GuidFromId(data.ContentKey.Id)), data.ContentKeyData, data.ContentKey.Name,
                data.ContentKey.ContentKeyType);

            // Cannot add content key directly to data.InputAsset as object.ReferenceEquals(asset, data.InputAsset)==false (probably due to threading)
            var asset = context.Assets.AsEnumerable().Single(a => a.Id == data.InputAsset.Id);
            asset.ContentKeys.Add(newKey);
        }

        public static async void DeleteAsset(IAsset asset)
        {
            if (asset != null)
            {
                foreach (IAssetFile file in asset.AssetFiles)
                {
                    _logger.Verbose("Deleting asset file {file} in asset {asset}", file.ToLog(), asset.ToLog());
                    await file.DeleteAsync();
                }

                _logger.Verbose("Deleting asset {asset}", asset.ToLog());
                await asset.DeleteAsync();
            }
        }

        public static string GuidFromId(string id)
        {
            string guid = id.Substring(id.IndexOf(KeyIdentifierPrefix, StringComparison.OrdinalIgnoreCase) + KeyIdentifierPrefix.Length);
            return guid;
        }
    }

}
