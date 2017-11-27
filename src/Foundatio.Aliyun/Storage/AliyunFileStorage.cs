using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aliyun.OSS;
using Foundatio.Extensions;

namespace Foundatio.Storage {
    public class AliyunFileStorage : IFileStorage {
        private readonly string _bucketName;
        private readonly OssClient _client;

        public AliyunFileStorage(string connectionString, string bucketName = "storage") {
            var account = AliyunConnectionString.Parse(connectionString);
            _client = account.CreateClient();
            _bucketName = bucketName;
            if (!_client.DoesBucketExist(_bucketName)) _client.CreateBucket(_bucketName);
        }

        public void Dispose() { }

        public async Task<Stream> GetFileStreamAsync(string path, CancellationToken cancellationToken = default(CancellationToken)) {
            using (var response = await Task.Factory.FromAsync(
                (request, callback, state) => _client.BeginGetObject(request, callback, state),
                result => _client.EndGetObject(result),
                new GetObjectRequest(_bucketName, path), null)) {
                return response.Content;
            }
        }

        public Task<FileSpec> GetFileInfoAsync(string path) {
            try {
                var metadata = _client.GetObjectMetadata(_bucketName, path);
                return Task.FromResult(new FileSpec {
                    Path = path,
                    Size = metadata.ContentLength,
                    Created = metadata.LastModified,
                    Modified = metadata.LastModified
                });
            }
            catch (Exception) {
                return null;
            }
        }

        public Task<bool> ExistsAsync(string path) {
            return Task.FromResult(_client.DoesObjectExist(_bucketName, path));
        }

        public async Task<bool> SaveFileAsync(string path, Stream stream, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                var putResult = await Task.Factory.FromAsync(
                    (request, callback, state) => _client.BeginPutObject(request, callback, state),
                    result => _client.EndPutObject(result), new PutObjectRequest(_bucketName, path, stream), null);
                return putResult.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (Exception) {
                return false;
            }
        }

        public async Task<bool> RenameFileAsync(string oldpath, string newpath, CancellationToken cancellationToken = default(CancellationToken)) {
            return await CopyFileAsync(oldpath, newpath, cancellationToken).AnyContext() &&
                   await DeleteFileAsync(oldpath, cancellationToken).AnyContext();
        }

        public async Task<bool> CopyFileAsync(string path, string targetpath, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                var copyResult = await Task.Factory.FromAsync(
                    (request, callback, state) => _client.BeginCopyObject(request, callback, state),
                    result => _client.EndCopyResult(result),
                    new CopyObjectRequest(_bucketName, path, _bucketName, targetpath), null);
                return copyResult.HttpStatusCode == HttpStatusCode.OK;
            }
            catch (Exception) {
                return false;
            }
        }

        public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default(CancellationToken)) {
            try {
                _client.DeleteObject(_bucketName, path);
                return Task.FromResult(true);
            }
            catch (Exception) {
                return Task.FromResult(false);
            }
        }

        public async Task DeleteFilesAsync(string searchPattern = null, CancellationToken cancellation = default(CancellationToken)) {
            var files = await GetFileListAsync(searchPattern, cancellationToken: cancellation).AnyContext();
            _client.DeleteObjects(new DeleteObjectsRequest(_bucketName, files.Select(spec => spec.Path).ToList()));
        }

        public async Task<IEnumerable<FileSpec>> GetFileListAsync(string searchPattern = null, int? limit = null, int? skip = null,
            CancellationToken cancellationToken = default(CancellationToken)) {
            if (limit.HasValue && limit.Value <= 0)
                return new List<FileSpec>();

            searchPattern = searchPattern?.Replace('\\', '/');
            string prefix = searchPattern;
            Regex patternRegex = null;
            int wildcardPos = searchPattern?.IndexOf('*') ?? -1;
            if (searchPattern != null && wildcardPos >= 0) {
                patternRegex = new Regex("^" + Regex.Escape(searchPattern).Replace("\\*", ".*?") + "$");
                int slashPos = searchPattern.LastIndexOf('/');
                prefix = slashPos >= 0 ? searchPattern.Substring(0, slashPos) : String.Empty;
            }
            prefix = prefix ?? String.Empty;

            string marker = null;
            var blobs = new List<OssObjectSummary>();
            do {
                var listing = await Task.Factory.FromAsync(
                    (request, callback, state) => _client.BeginListObjects(request, callback, state),
                    result => _client.EndListObjects(result), new ListObjectsRequest(_bucketName) {
                        Prefix = prefix,
                        Marker = marker,
                        MaxKeys = limit
                    }, null);
                marker = listing.NextMarker;
                
                blobs.AddRange(listing.ObjectSummaries.Where(blob => patternRegex == null || patternRegex.IsMatch(blob.Key)));
            } while (marker != null && blobs.Count < limit.GetValueOrDefault(Int32.MaxValue));

            if (limit.HasValue)
                blobs = blobs.Take(limit.Value).ToList();

            return blobs.Select(blob => new FileSpec {
                Path = blob.Key,
                Size = blob.Size,
                Created = blob.LastModified,
                Modified = blob.LastModified
            });
        }
    }
}