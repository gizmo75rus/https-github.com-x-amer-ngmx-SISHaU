﻿using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SISHaU.Library.File;
using SISHaU.ServiceModel.Types;
using SISHaU.Library.File.Model;
using System.Linq;
using System;
using System.IO;

namespace SISHaU.UnitTests
{
    [TestClass]
    public class FileExchangeTests
    {
        private Builder _fileServiceBuilder;
        [TestInitialize]
        public void TestSetup()
        {
            Config.InitConfig();

            _fileServiceBuilder = new Builder();
        }

        [TestMethod]
        public void TestUploadFiles()
        {



            /*System.IO.DirectoryInfo dir = new System.IO.DirectoryInfo(@"D:\TestFileUploade");

            var files = dir.EnumerateFiles().Select(x=> $@"{x.FullName}").ToList();*/


            var doUpload = new UploadFiles
            {

                FilesPathList = /*files*/ new List<string>
                  {
                          @"D:\test0.zip",
                          @"D:\test1.zip",
                          @"D:\test2.zip",
                          @"D:\test3.zip",
                          @"D:\test4.zip",
                          @"D:\test5.zip",
                          @"D:\test6.zip",
                          @"D:\test7.zip",
                          @"D:\test8.zip",
                          @"D:\test9.zip",
                          @"D:\test10.zip",
                          @"D:\test11.zip",
                          @"D:\test12.zip",
                          @"D:\test13.zip",
                          @"D:\test14.zip"
                  },
                RepositoryMarker = Repo.Homemanagement
            };
            var result = _fileServiceBuilder.UploadFilesList(doUpload.FilesPathList, doUpload.RepositoryMarker);


        }

        [TestMethod]
        public void TestDownloadFiles()
        {
            /*47*
            var down = new List<DownloadModel>() {
                new DownloadModel{ FileGuid = "e56e6ed5-3315-492e-bf7e-05d2cd58410b"},
                new DownloadModel{ FileGuid = "35a9a939-2e2c-449f-89a7-47801d18e047"},
                new DownloadModel{ FileGuid = "92e561df-46a1-4b00-948c-d6785f551f00"},
                //new DownloadModel{ FileGuid = "2ec6e59e-f74e-44ab-9a04-29fab00d7377"},
            };*/

            /*56*/
            var down = new List<DownloadModel>() {
                new DownloadModel{ FileGuid = "aa791878-4fad-47fb-814b-af7819aabacc"},
                new DownloadModel{ FileGuid = "04ff4133-9029-419f-971f-246a85030ff6"},
                new DownloadModel{ FileGuid = "78d0a58f-2089-4900-b406-b31a3ac2d120"},
                new DownloadModel{ FileGuid = "f7cada60-6939-46a1-b403-60df7f27724f"},
                //new DownloadModel{ FileGuid = "b3f92ef5-7cf4-4a9a-b8f0-41a7fb4fb0e6"}
            };



            var downResult = _fileServiceBuilder.DownloadFilesList(down);

            foreach (var dow in downResult)
            {
                var patch = $@"{Config.TempPath(Config.TempType.Down)}\{dow.FileInfo.FileName}";
                using (var strim = new FileStream(patch, FileMode.Create, FileAccess.Write))
                {
                    foreach (var part in dow.PartInfo)
                    {
                        var bytes = File.ReadAllBytes(part.Patch);
                        strim.Write(bytes, 0, bytes.Length);

                        File.Delete(part.Patch);
                    }

                }
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [TestCleanup]
        public void Clianup() {
            _fileServiceBuilder = null;
        }

    }
}