﻿using System;
using System.Configuration;
using System.IO;
using System.Linq;
using Microsoft.WindowsAzure.MediaServices.Client;
using Serilog;

namespace MLSandboxPOC
{
    class Program
    {
        // Field for service context.
        private static CloudMediaContext _context = null;

        private static ILogger _logger;

        static void Main(string[] args)
        {
            try
            {
                _logger = Logger.GetLog<Program>();

                // Is this running as a service?
                if (!Environment.UserInteractive)
                {
                    System.ServiceProcess.ServiceBase.Run(new MLSBService());
                    return;
                }

                //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

                Console.WriteLine("ML Sandbox POC");
                Console.WriteLine("==============");
                Console.WriteLine();

                //string src = Path.Combine(_sourceDir, "4th Apr 17_612026009250275cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009249280 040417_1.wav");
                //string src2 = Path.Combine(_sourceDir, "612026009249280 040417_2.wav");
                //string src = Path.Combine(_sourceDir, "612026009249280 040417 0932_1191101cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009249955 040417 1028_1191101cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009250579 040417 1110_1191101cut.wav");
                //string src = Path.Combine(_sourceDir, "612026009280132 110417_ATJStest1191100.wav");

                if (args.Length > 0)
                {
                    _context = CloudMediaContextFactory.Instance.CloudMediaContext;

                    if (args[0].Equals("delOnly", StringComparison.InvariantCultureIgnoreCase))
                    {
                        DeleteAssetFiles();
                    }
                    else if (args[0].Equals("getAssets", StringComparison.InvariantCultureIgnoreCase))
                    {
                        GetAllAssetFiles();
                    }
                    //else if (args[0].Equals("-f", StringComparison.InvariantCultureIgnoreCase))
                    //{
                    //    src = Path.Combine(_sourceDir, args[1]);
                    //}

                    return;
                }

                ProcessRunner.Instance.Run();

                Console.WriteLine("Started filewatcher");
                Console.WriteLine("-> Press any key to exit");

                Console.ReadKey();

                ProcessRunner.Instance.Shutdown();

                // Run indexing job.
                //uploadManager.QueueItem(src);
                //uploadManager.QueueItem(src2);

                // Download the job output asset.
                //DownloadAsset(asset, _outDir);
                //downloadManager.QueueItem(asset);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Fatal error in demo program!!!");
            }
        }

        private static void GetAllAssetFiles()
        {
            //CreateCredentials();

            _logger.Information("Downloading all stored audio file info");

            foreach (var asset in _context.Assets)
            {
                var key = asset.ContentKeys.FirstOrDefault();
                if (key == null)
                {
                    _logger.Information("Asset {asset} has no ContentKey.", asset.ToLog());
                }
                else
                {
                    _logger.Information("Asset {asset} has content key: {key} ", asset.ToLog(),
                        new { key.ContentKeyType, key.EncryptedContentKey, key.ProtectionKeyId });
                }

                foreach (var af in asset.AssetFiles)
                {
                    _logger.Information("   Asset has file {file}", af.ToLog());
                }
            }
        }

        private static IAsset CreateAssetFromFolder()
        {
            _logger.Debug("Creating asset and uploading all files in {folder}", Config.Instance.SourceDirectory);
            var asset = _context.Assets.CreateFromFolder(Config.Instance.SourceDirectory, AssetCreationOptions.StorageEncrypted);

            // Create a manifest file that contains all the asset file names and upload to storage.
            _logger.Debug("Creating manifest");
            string manifestFile = "input.lst";

            var filenames = asset.AssetFiles.AsEnumerable().Select(f => f.Name);
            File.WriteAllLines(manifestFile, filenames);
            var assetFile = asset.AssetFiles.Create(Path.GetFileName(manifestFile));
            assetFile.Upload(manifestFile);

            var assetFile2 = asset.AssetFiles.Create(Path.GetFileName("indexerCfg.xml"));
            assetFile2.Upload("indexerCfg.xml");
            return asset;
        }


        private static void DeleteAssetFiles()
        {
            foreach (var asset in _context.Assets)
            {
                foreach (var af in asset.AssetFiles)
                {
                    _logger.Information("Deleting {file} from asset {asset}", af.ToLog(), asset.ToLog());
                    af.Delete();
                }

                _logger.Information("Deleting asset {asset}", asset.ToLog());
                asset.Delete();
            }
        }
    }

}
