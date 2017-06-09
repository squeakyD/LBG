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

        //private static readonly object _contentKeyLock = new object();

        public static void RemoveEncryptionKey(IndexJobData data)
        {
            //lock (_contentKeyLock)
            //{
            data.ContentKey = data.InputAsset.ContentKeys.AsEnumerable()
                .FirstOrDefault(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
            data.ContentKeyData = data.ContentKey.GetClearKeyValue();
            data.InputAsset.ContentKeys.Remove(data.ContentKey);
            data.InputAsset.Update();
            //}

            _logger.Verbose("Removed encryption key {key} from asset {asset}", data.ContentKey.Id, data.InputAsset.ToLog());
        }

        public static void RemoveEncryptionKey(CloudMediaContext context, IndexJobData data)
        {
            //lock (_contentKeyLock)
            //{
            int c1 = context.ContentKeys.AsEnumerable().Count(k => k.ContentKeyType == ContentKeyType.StorageEncryption);

                 data.ContentKey = data.InputAsset.ContentKeys.AsEnumerable()
                    .FirstOrDefault(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
                data.ContentKeyData = data.ContentKey.GetClearKeyValue();
                data.InputAsset.ContentKeys.Remove(data.ContentKey);
                data.InputAsset.Update();
            //context.ContentKeys
            //var newKey = context.ContentKeys.Create(Guid.Parse(GuidFromId(data.ContentKey.Id)), data.ContentKeyData, data.ContentKey.Name, data.ContentKey.ContentKeyType);
            //data.ContentKey= newKey;
            //}

            int c2 = context.ContentKeys.AsEnumerable().Count(k => k.ContentKeyType == ContentKeyType.StorageEncryption);
            var ck = context.ContentKeys.Where(k => k.Id == data.ContentKey.Id).ToArray();

            //foreach (var key in context.ContentKeys.ToList().Where(k => KeysListIDs.Contains(k.Id)).ToList())
            //{
            //    //try
            //    //{
            //        key.Delete();
            //    //}
            //    //catch (Exception e)
            //    //{

            //    //    Error = true;
            //    //}
            //}

            _logger.Verbose("Removed encryption key {key} from asset {asset}", data.ContentKey.Id, data.InputAsset.ToLog());
        }


        public static void RestoreEncryptionKey(CloudMediaContext context, IndexJobData data)
        {
            _logger.Verbose("Restoring key {key} to asset {asset}", data.ContentKey.Id, data.InputAsset.ToLog());

            //lock (_contentKeyLock)
            //{
                var newKey = context.ContentKeys.Create(Guid.Parse(GuidFromId(data.ContentKey.Id)), data.ContentKeyData, data.ContentKey.Name,
                    data.ContentKey.ContentKeyType);
                data.InputAsset.ContentKeys.Add(newKey);
            //}
        }

        public static void DeleteAsset(IAsset asset)
        {
            if (asset != null)
            {
                foreach (IAssetFile file in asset.AssetFiles)
                {
                    _logger.Verbose("Deleting asset file {file} in asset {asset}", file.ToLog(), asset.ToLog());
                    file.Delete();
                }

                _logger.Verbose("Deleting asset {asset}", asset.ToLog());
                asset.Delete();
            }
        }

        public static string GuidFromId(string id)
        {
            string guid = id.Substring(id.IndexOf(KeyIdentifierPrefix, StringComparison.OrdinalIgnoreCase) + KeyIdentifierPrefix.Length);
            return guid;
        }
    }

}
