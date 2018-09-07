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
using Foundatio.Serializer;

namespace Foundatio.Storage {
    public class AliyunFileStorage : IFileStorage {
        private readonly string _bucket;
        private readonly OssClient _client;
        private readonly ISerializer _serializer;

        public AliyunFileStorage(AliyunFileStorageOptions options) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var connectionString = new AliyunFileStorageConnectionStringBuilder(options.ConnectionString);
            _client = new OssClient(connectionString.Endpoint, connectionString.AccessKey, connectionString.SecretKey);
            _bucket = connectionString.Bucket;
            _serializer = options.Serializer ?? DefaultSerializer.Instance;
            if (!DoesBucketExist(_bucket)) _client.CreateBucket(_bucket);
        }

        public AliyunFileStorage(Builder<AliyunFileStorageOptionsBuilder, AliyunFileStorageOptions> builder)
            : this(builder(new AliyunFileStorageOptionsBuilder()).Build()) { }

        ISerializer IHaveSerializer.Serializer => _serializer;

        public async Task<Stream> GetFileStreamAsync(string path, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            var response = await Task.Factory.FromAsync(
                (request, callback, state) => _client.BeginGetObject(request, callback, state),
                result => _client.EndGetObject(result),
                new GetObjectRequest(_bucket, NormalizePath(path)),
                null
            ).AnyContext();
            return response.Content;
        }

        public Task<FileSpec> GetFileInfoAsync(string path) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            path = NormalizePath(path);
            try {
                var metadata = _client.GetObjectMetadata(_bucket, path);
                return Task.FromResult(new FileSpec {
                    Path = path,
                    Size = metadata.ContentLength,
                    Created = metadata.LastModified,
                    Modified = metadata.LastModified
                });
            } catch (Exception) {
                return Task.FromResult((FileSpec)null);
            }
        }

        public Task<bool> ExistsAsync(string path) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            try {
                return Task.FromResult(_client.DoesObjectExist(_bucket, NormalizePath(path)));
            } catch (Exception ex) when (IsNotFoundException(ex)) {
                return Task.FromResult(false);
            }
        }

        public async Task<bool> SaveFileAsync(string path, Stream stream, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            var seekableStream = stream.CanSeek ? stream : new MemoryStream();
            if (!stream.CanSeek) {
                await stream.CopyToAsync(seekableStream).AnyContext();
                seekableStream.Seek(0, SeekOrigin.Begin);
            }

            try {
                var putResult = await Task.Factory.FromAsync((request, callback, state) => _client.BeginPutObject(request, callback, state), result => _client.EndPutObject(result), new PutObjectRequest(_bucket, NormalizePath(path), seekableStream), null).AnyContext();
                return putResult.HttpStatusCode == HttpStatusCode.OK;
            } catch (Exception) {
                return false;
            } finally {
                if (!stream.CanSeek)
                    seekableStream.Dispose();
            }
        }

        public async Task<bool> RenameFileAsync(string path, string newPath, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            if (String.IsNullOrEmpty(newPath))
                throw new ArgumentNullException(nameof(newPath));

            path = NormalizePath(path);
            newPath = NormalizePath(newPath);
            return await CopyFileAsync(path, newPath, cancellationToken).AnyContext() &&
                    await DeleteFileAsync(path, cancellationToken).AnyContext();
        }

        public async Task<bool> CopyFileAsync(string path, string targetPath, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            if (String.IsNullOrEmpty(targetPath))
                throw new ArgumentNullException(nameof(targetPath));

            try {
                var copyResult = await Task.Factory.FromAsync(
                    (request, callback, state) => _client.BeginCopyObject(request, callback, state),
                    result => _client.EndCopyResult(result),
                    new CopyObjectRequest(_bucket, NormalizePath(path), _bucket, NormalizePath(targetPath)),
                    null
                ).AnyContext();
                return copyResult.HttpStatusCode == HttpStatusCode.OK;
            } catch (Exception) {
                return false;
            }
        }

        public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            try {
                _client.DeleteObject(_bucket, NormalizePath(path));
                return Task.FromResult(true);
            } catch (Exception) {
                return Task.FromResult(false);
            }
        }

        public async Task DeleteFilesAsync(string searchPattern = null, CancellationToken cancellation = default) {
            var files = await GetFileListAsync(searchPattern, cancellationToken: cancellation).AnyContext();
            _client.DeleteObjects(new DeleteObjectsRequest(_bucket, files.Select(spec => spec.Path).ToList()));
        }

        public async Task<IEnumerable<FileSpec>> GetFileListAsync(string searchPattern = null, int? limit = null, int? skip = null,
            CancellationToken cancellationToken = default) {
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
                    result => _client.EndListObjects(result), new ListObjectsRequest(_bucket) {
                        Prefix = prefix,
                        Marker = marker,
                        MaxKeys = limit
                    },
                    null
                ).AnyContext();
                marker = listing.NextMarker;

                blobs.AddRange(listing.ObjectSummaries.Where(blob => patternRegex == null || patternRegex.IsMatch(blob.Key)));
            } while (!cancellationToken.IsCancellationRequested && !String.IsNullOrEmpty(marker) && blobs.Count < limit.GetValueOrDefault(Int32.MaxValue));

            if (limit.HasValue)
                blobs = blobs.Take(limit.Value).ToList();

            return blobs.Select(blob => new FileSpec {
                Path = blob.Key,
                Size = blob.Size,
                Created = blob.LastModified,
                Modified = blob.LastModified
            });
        }

        public void Dispose() { }

        private bool IsNotFoundException(Exception ex) {
            if (ex is AggregateException aggregateException) {
                foreach (var innerException in aggregateException.InnerExceptions) {
                    if (IsNotFoundException(innerException))
                        return true;
                }
            }

            if (ex is WebException webException && webException.Response is HttpWebResponse response)
                return response.StatusCode == HttpStatusCode.NotFound;

            return false;
        }

        private string NormalizePath(string path) {
            return path?.Replace('\\', '/');
        }

        private bool DoesBucketExist(string bucketName) {
            try {
                return _client.DoesBucketExist(bucketName);
            } catch (Exception ex) when (IsNotFoundException(ex)) {
                return false;
            }
        }
    }
}