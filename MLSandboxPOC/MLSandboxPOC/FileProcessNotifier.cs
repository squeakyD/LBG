using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLSandboxPOC
{
    class FileProcessNotifier
    {
        private IFileProcessedObserver _observer;

        private static FileProcessNotifier _instance = new FileProcessNotifier();

        public static FileProcessNotifier Instance => _instance;

        private FileProcessNotifier()
        {
        }

        public void AddFileObserver(IFileProcessedObserver observer)
        {
            _observer = observer;
        }

        public void NotifyFileProcessed(string fileName)
        {
            _observer.FileProcessed(fileName);
        }
    }

}
