using System;
using System.IO;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace MLSandboxPOC
{
    class IndexJobData
    {
        public string Filename { get; set; }
        public IAsset InputAsset { get; set; }
        public IContentKey ContentKey { get; set; }
        public byte[] ContentKeyData { get; set; }
        public IAsset OutputAsset { get; set; }
        public DateTime InputAssetUploadStart { get; set; }
        public DateTime InputFileUploaded { get; set; }
        public DateTime InputAssetKeyRestored { get; set; }
        public DateTime InputAssetDeleted { get; set; }
        public DateTime OutputAssetCreated { get; set; }
        public DateTime OutputAssetDeleted { get; set; }
    }
}