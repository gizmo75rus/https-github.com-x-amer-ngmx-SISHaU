﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SISHaU.Library.File.Model;
using System.Net.Http;
using System.Threading;
using NHibernate.Util;

namespace SISHaU.Library.File.Enginer
{
    //TODO: EnginerFileRun - Нужна реализация дозагрузки\довыгрузки
    public sealed class EnginerFileRun : IDisposable
    {
        private readonly ResponseRequestOnServer _serverConnect;

        private readonly Repo _repository;

        #region Выгрузка фыйлов

        public EnginerFileRun(Repo repository)
        {
            _repository = repository;
            _serverConnect = new ResponseRequestOnServer(_repository);
        }

        public UploadeResultModel UploadFile(SplitFileModel uploadeMod, ref ParallelOptions qu)
        {
            if (uploadeMod == null || !uploadeMod.Parts.Any())
            {
                return null;
            }

            UploadeResultModel result;

            var count = uploadeMod.Parts.Count;


            if (count > 1)
            {
                result = BigUploadeFile(uploadeMod.FileInfo, count, uploadeMod.Parts, ref qu);

            }
            else if (count == 1)
            {
                result = SmaillUploadeFile(uploadeMod.FileInfo, uploadeMod.Parts.FirstOrDefault());
            }
            else throw new Exception("Что-то пошло не так, количество частей меньше одной.");

            return result;
        }

        private UploadeResultModel BigUploadeFile(ResultModel fileInfo, int partCount, IList<UpPartInfoModel> parts, ref ParallelOptions quite)
        {
            ResponseIdModel session;
            ResponseSessionCloseModel sessionClose;

            //Убираю лимит на количество одновременных запросов.
            ServicePointManager.DefaultConnectionLimit = 15;

            var index = 0;
            while (true)
            {
                var response = _serverConnect.RequestLoadingUnitStartSession(
                    fileInfo.FileName,
                    fileInfo.FileSize,
                    partCount).SendRequest();

                if (index > 0) Thread.Sleep(10000 * index);
                if (index > 6)
                {
                    //Зывершаем поток
                    quite.CancellationToken.ThrowIfCancellationRequested();
                    return new UploadeResultModel
                    {
                        ErrorMessage = new RequestErrorModel
                        {
                            ErrorCode = 400,
                            ErrorInfo = "SereverTimeOut",
                            PointErrorDescript = "Сервер не доступен или соединение было разорвано"
                        }
                    };
                }
                index++;
                if (response?.StatusCode == HttpStatusCode.OK)
                {
                    session = response.ResultEnginer<ResponseIdModel>();
                    break;
                }
                response.Dispose();
            }


            Parallel.ForEach(parts, (part, state) =>
            {

                var response = UpLoadePart(part, sessId: session.UploadId);

                var stateUploaded = response.ResultEnginer<ResponseModel>();
                response.Dispose();

                if (stateUploaded.ServerError != null)
                {
                    //partRes.Add(stateUploaded);
                    //Возникла ошибка при загрузке части
                }
                else
                {

                    if (part.Patch.IndexOf(".tmpart", StringComparison.OrdinalIgnoreCase) > 0) System.IO.File.Delete(part.Patch);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            });

            while (true)
            {
                var response = _serverConnect.RequestLoadingUnitCloseSession(session.UploadId).SendRequest();

                if (index > 0) Thread.Sleep(10000 * index);
                if (index < 6) index++;
                if (response?.StatusCode == HttpStatusCode.OK)
                {
                    sessionClose = response.ResultEnginer<ResponseSessionCloseModel>();
                    break;
                }
                response.Dispose();
            }



            var result = sessionClose.IsClose == false ? new UploadeResultModel
            {
                ErrorMessage = new RequestErrorModel()
            } : new UploadeResultModel
            {
                FileGuid = session.UploadId,
                FileName = fileInfo.FileName,
                FileSize = fileInfo.FileSize,
                GostHash = fileInfo.GostHash,
                Repository = _repository,
                UTime = sessionClose.ResultDate?.DateTime
            };

            return result;
        }

        private UploadeResultModel SmaillUploadeFile(ResultModel fileInfo, UpPartInfoModel part)
        {
            var response = UpLoadePart(part, fileInfo.FileName);
            var uploadeId = response.ResultEnginer<ResponseIdModel>(false);


            var result = uploadeId.ServerError != null ? new UploadeResultModel { ErrorMessage = new RequestErrorModel() }
                : new UploadeResultModel
                {
                    FileName = fileInfo.FileName,
                    FileSize = fileInfo.FileSize,
                    GostHash = fileInfo.GostHash,
                    Repository = _repository,
                    FileGuid = uploadeId.UploadId,
                    UTime = uploadeId.ResultDate?.DateTime
                };

            //обязательно мочим временный экземпляр файла
            if (part.Patch.IndexOf(".tmpart", StringComparison.OrdinalIgnoreCase) > 0) System.IO.File.Delete(part.Patch);

            GC.Collect();
            //GC.WaitForPendingFinalizers();

            return result;

        }

        private HttpResponseMessage UpLoadePart(UpPartInfoModel part, string name = null, string sessId = null)
        {
            
            object param = !string.IsNullOrEmpty(name) ? (object)name : part.Part;

            HttpResponseMessage result;

            var index = 0;
            while (true)
            {
                using (var partStream = System.IO.File.OpenRead(part.Patch))
                {
                    result = _serverConnect.RequestLoadingPart(
                            partStream,
                            partStream.Length,
                            part.Md5Hash,
                            param,
                            sessId).SendRequest();
                }

                if (index > 0) Thread.Sleep(1000 * index);

                if (index < 30) index++;

                if (result?.StatusCode == HttpStatusCode.OK) break;
            }

            return result;
        }

        #endregion

        #region Загрузка файлов

        public DownloadInfoModel DownloadFile(string fileId)
        {
            var result = new DownloadInfoModel();
            ResponseInfoModel fileInfo = null;

            //пытаемся достучаться до сервака
            var index = 0;
            while (true)
            {
                using (var response = _serverConnect.RequestLoadingUnitInfo(fileId).SendRequest())
                {
                    fileInfo = response.ResultEnginer<ResponseInfoModel>();

                    if (index > 0) Thread.Sleep(10000 * index);

                    if (index > 6)
                    {
                        result.ErrorMessage = new RequestErrorModel
                        {
                            ErrorCode = 503,
                            ErrorInfo = "Соединение с сервером небыло установленно",
                            PointErrorDescript = ""
                        };

                        return result;
                    }
                    index++;
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        break;
                    }
                }

            }

            var filePrefix = $@"{Config.TempPath(Config.TempType.Down)}\{Path.GetFileNameWithoutExtension(fileInfo.FileName)}";
            var download = DownloadeInfo(fileInfo);

            result.FileInfo = download.FileInfo;

            var parts = DownloadPartsAsync(fileId, download, filePrefix);
            result.PartInfo = parts.Count > 1 ? (from p in parts orderby p.Part select p).ToList() : parts;

            return result;
        }

        private IList<PartInfoModel> DownloadPartsAsync(string fileId, DownloadFileInfo dinfo, string filePrefix)
        {
            var result = new List<PartInfoModel>();

            var collect = new ConcurrentBag<DownPartInfoModel>(dinfo.Parts);

            Parallel.ForEach(collect, (part, state) =>
                //foreach (var part in collect)
            {
                while (true)
                {

                    try
                    {
                        using (var client = _serverConnect.RequestDownloading(fileId, part))
                        {
                            var stream = client.OpenRead(_serverConnect.RequestUri.UriRequest);

                            if ((client.StatusCode() == HttpStatusCode.OK) && stream!=null)
                            {
                                var patch = $"{filePrefix}_{part.Part:D2}_{dinfo.FileInfo.FileSize}.tmpart";
                                using (var fileStream =
                                    new FileStream(patch,
                                        FileMode.Create, FileAccess.Write))
                                {
                                    stream?.CopyToAsync(fileStream).Wait();
                                }

                                result.Add(new PartInfoModel
                                {
                                    Part = part.Part,
                                    Patch = patch
                                });

                                break;
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        var ass = ex.Message;
                    }
                }
            });
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return result;
        }


        private DownloadFileInfo DownloadeInfo(ResponseInfoModel info)
        {
            var result = new DownloadFileInfo();
            long partFromSize = 0;

            result.FileInfo = new ResultModel { FileSize = info.FileSize, FileName = info.FileName };

            var range = new List<DownPartInfoModel>();

            foreach (var part in info.FileCompleateParts)
            {
                var thisPartSize = (partFromSize + Config.MaxPartSize);
                var partToSize = info.FileSize < thisPartSize ? info.FileSize : thisPartSize;

                var to = partToSize - 1;

                range.Add(new DownPartInfoModel
                {
                    Part = part,
                    From = partFromSize,
                    To = to
                });
                partFromSize = thisPartSize;
            }

            result.Parts = range;

            return result;
        }

        #endregion

        private void Dispose(bool disposing)
        {
            if (!disposing) return;
            _serverConnect?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~EnginerFileRun()
        {
            Dispose(false);
        }
    }

}
