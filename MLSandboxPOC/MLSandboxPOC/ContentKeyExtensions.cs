//using Microsoft.WindowsAzure.MediaServices.Client;
//using Microsoft.WindowsAzure.MediaServices.Client.TransientFaultHandling;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Security.Cryptography.X509Certificates;
//using System.Text;
//using System.Threading.Tasks;

//namespace MLSandboxPOC
//{
//    static class ContentKeyExtensions
//    {
//        /// <summary>
//        /// Creates a content key with the specifies key identifier and value.
//        /// </summary>
//        /// <param name="keyId">The key identifier.</param>
//        /// <param name="contentKey">The value of the content key.</param>
//        /// <param name="name">A friendly name for the content key.</param>
//        /// <param name="contentKeyType">Type of content key to create.</param>
//        /// <param name="trackIdentifiers">A list of tracks to be encrypted by this content key.</param>
//        /// <returns>A <see cref="IContentKey"/> that can be associated with an <see cref="IAsset"/>.</returns>
//        public static IContentKey CreateExt(this ContentKeyCollection ckc, Guid keyId, byte[] contentKey, string name, ContentKeyType contentKeyType, IEnumerable<string> trackIdentifiers)
//        {
//            try
//            {
//                Task<IContentKey> task = ckc.CreateAsyncExt(keyId, contentKey, name, contentKeyType, trackIdentifiers);
//                task.Wait();

//                return task.Result;
//            }
//            catch (AggregateException exception)
//            {
//                throw exception.InnerException;
//            }
//        }

//        /// <summary>
//        /// Asynchronously creates a content key with the specifies key identifier and value.
//        /// </summary>
//        /// <param name="keyId">The key identifier.</param>
//        /// <param name="contentKey">The value of the content key.</param>
//        /// <param name="name">A friendly name for the content key.</param>
//        /// <param name="contentKeyType">Type of content key to create.</param>
//        /// <param name="trackIdentifiers">A list of tracks to be encrypted by this content key.</param>
//        /// <returns>
//        /// A function delegate that returns the future result to be available through the Task&lt;IContentKey&gt;.
//        /// </returns>
//        public static Task<IContentKey> CreateAsyncExt(this ContentKeyCollection ckc, Guid keyId, byte[] contentKey, string name, ContentKeyType contentKeyType, IEnumerable<string> trackIdentifiers)
//        {
//            var allowedKeyTypes = new[]
//            {
//                ContentKeyType.CommonEncryption,
//                ContentKeyType.StorageEncryption,
//                ContentKeyType.CommonEncryptionCbcs,
//                ContentKeyType.EnvelopeEncryption,
//                ContentKeyType.FairPlayASk,
//                ContentKeyType.FairPlayPfxPassword,
//            };

//            if (!allowedKeyTypes.Contains(contentKeyType))
//            {
//                throw new ArgumentException("StringTable.ErrorUnsupportedContentKeyType", "contentKey");
//            }

//            if (keyId == Guid.Empty)
//            {
//                throw new ArgumentException("StringTable.ErrorCreateKey_EmptyGuidNotAllowed", "keyId");
//            }

//            if (contentKey == null)
//            {
//                throw new ArgumentNullException("contentKey");
//            }

//            if (contentKeyType != ContentKeyType.FairPlayPfxPassword &&
//                contentKeyType != ContentKeyType.StorageEncryption &&
//                contentKey.Length != EncryptionUtils.KeySizeInBytesForAes128)
//            {
//                throw new ArgumentException("StringTable.ErrorCommonEncryptionKeySize", "contentKey");
//            }

//            IMediaDataServiceContext dataContext = ckc.MediaContext.MediaServicesClassFactory.CreateDataServiceContext();
//            X509Certificate2 certToUse = GetCertificateToEncryptContentKey(ckc.MediaContext, ContentKeyType.CommonEncryption);

//            ContentKeyData contentKeyData = null;

//            if (contentKeyType == ContentKeyType.CommonEncryption)
//            {
//                contentKeyData = ckc.InitializeCommonContentKey(keyId, contentKey, name, certToUse);
//            }
//            else if (contentKeyType == ContentKeyType.StorageEncryption)
//            {
//                certToUse = GetCertificateToEncryptContentKey(MediaContext, ContentKeyType.StorageEncryption);
//                contentKeyData = InitializeStorageContentKey(new FileEncryption(contentKey, keyId), certToUse);
//                contentKeyData.Name = name;
//            }
//            else if (contentKeyType == ContentKeyType.CommonEncryptionCbcs)
//            {
//                contentKeyData = InitializeCommonContentKey(keyId, contentKey, name, certToUse);
//                contentKeyData.ContentKeyType = (int)ContentKeyType.CommonEncryptionCbcs;
//            }
//            else if (contentKeyType == ContentKeyType.EnvelopeEncryption)
//            {
//                contentKeyData = InitializeEnvelopeContentKey(keyId, contentKey, name, certToUse);
//            }
//            else if (contentKeyType == ContentKeyType.FairPlayPfxPassword)
//            {
//                contentKeyData = InitializeFairPlayPfxPassword(keyId, contentKey, name, certToUse);
//            }
//            else if (contentKeyType == ContentKeyType.FairPlayASk)
//            {
//                contentKeyData = InitializeFairPlayASk(keyId, contentKey, name, certToUse);
//            }

//            dataContext.AddObject(ContentKeySet, contentKeyData);

//            contentKeyData.TrackIdentifiers = (trackIdentifiers != null && trackIdentifiers.Any()) ? string.Join(",", trackIdentifiers) : null;


//            MediaRetryPolicy retryPolicy = this.MediaContext.MediaServicesClassFactory.GetSaveChangesRetryPolicy(dataContext as IRetryPolicyAdapter);

//            return retryPolicy.ExecuteAsync<IMediaDataServiceResponse>(() => dataContext.SaveChangesAsync(contentKeyData))
//                .ContinueWith<IContentKey>(
//                    t =>
//                    {
//                        t.ThrowIfFaulted();

//                        return (ContentKeyData)t.Result.AsyncState;
//                    },
//                    TaskContinuationOptions.ExecuteSynchronously);
//        }

//    }
//}
