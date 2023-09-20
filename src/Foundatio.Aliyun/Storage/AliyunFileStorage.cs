using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aliyun.OSS;
using Foundatio.Aliyun.Extensions;
using Foundatio.Extensions;
using Foundatio.Serializer;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Foundatio.Storage {
    public class AliyunFileStorage : IFileStorage {
        private readonly string _bucket;
        private readonly OssClient _client;
        private readonly ISerializer _serializer;
        protected readonly ILogger _logger;

        public AliyunFileStorage(AliyunFileStorageOptions options) {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            
            _serializer = options.Serializer ?? DefaultSerializer.Instance;
            _logger = options.LoggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;

            var connectionString = new AliyunFileStorageConnectionStringBuilder(options.ConnectionString);
            _client = new OssClient(connectionString.Endpoint, connectionString.AccessKey, connectionString.SecretKey);
            
            _bucket = connectionString.Bucket;
            if (DoesBucketExist(_bucket)) 
                return;
            
            _logger.LogInformation("Creating {Bucket}", _bucket);
            _client.CreateBucket(_bucket);
            _logger.LogInformation("Created {Bucket}", _bucket);
        }

        public AliyunFileStorage(Builder<AliyunFileStorageOptionsBuilder, AliyunFileStorageOptions> builder)
            : this(builder(new AliyunFileStorageOptionsBuilder()).Build()) { }

        ISerializer IHaveSerializer.Serializer => _serializer;
        public OssClient Client => _client;

        public async Task<Stream> GetFileStreamAsync(string path, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string normalizedPath = NormalizePath(path);
            _logger.LogTrace("Getting file stream for {Path}", normalizedPath);
            
            var response = await Task.Factory.FromAsync(_client.BeginGetObject, _client.EndGetObject, new GetObjectRequest(_bucket, normalizedPath), null).AnyContext();
            if (!response.HttpStatusCode.IsSuccessful()) {
                _logger.LogError("[{HttpStatusCode}] Unable to get file stream for {Path}", response.HttpStatusCode, normalizedPath);
                return null;
            }
            
            return new ActionableStream(response.ResponseStream, () => {
                _logger.LogTrace("Disposing file stream for {Path}", normalizedPath);
                response.Dispose();
            });
        }

        public Task<FileSpec> GetFileInfoAsync(string path) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string normalizedPath = NormalizePath(path);
            _logger.LogTrace("Getting file info for {Path}", normalizedPath);
            
            try {
                var metadata = _client.GetObjectMetadata(_bucket, normalizedPath);
                return Task.FromResult(new FileSpec {
                    Path = normalizedPath,
                    Size = metadata.ContentLength,
                    Created = metadata.LastModified,
                    Modified = metadata.LastModified
                });
            } catch (Exception ex) {
                _logger.LogError(ex, "Unable to get file info for {Path}: {Message}", path, ex.Message);
                return Task.FromResult((FileSpec)null);
            }
        }

        public Task<bool> ExistsAsync(string path) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string normalizedPath = NormalizePath(path);
            _logger.LogTrace("Checking if {Path} exists", normalizedPath);
            
            try {
                return Task.FromResult(_client.DoesObjectExist(_bucket, normalizedPath));
            } catch (Exception ex) when (IsNotFoundException(ex)) {
                _logger.LogDebug(ex, "Unable to check if {Path} exists: {Message}", normalizedPath, ex.Message);
                return Task.FromResult(false);
            }
        }

        public async Task<bool> SaveFileAsync(string path, Stream stream, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            string normalizedPath = NormalizePath(path);
            _logger.LogTrace("Saving {Path}", normalizedPath);
            
            var seekableStream = stream.CanSeek ? stream : new MemoryStream();
            if (!stream.CanSeek) {
                await stream.CopyToAsync(seekableStream).AnyContext();
                seekableStream.Seek(0, SeekOrigin.Begin);
            }

            try {
                var putResult = await Task.Factory.FromAsync(_client.BeginPutObject, _client.EndPutObject, new PutObjectRequest(_bucket, normalizedPath, seekableStream), null).AnyContext();
                return putResult.HttpStatusCode == HttpStatusCode.OK;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error saving {Path}: {Message}", normalizedPath, ex.Message);
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

            string normalizedPath = NormalizePath(path);
            string normalizedNewPath = NormalizePath(newPath);
            _logger.LogInformation("Renaming {Path} to {NewPath}", normalizedPath, normalizedNewPath);
            
            return await CopyFileAsync(normalizedPath, normalizedNewPath, cancellationToken).AnyContext() &&
                   await DeleteFileAsync(normalizedPath, cancellationToken).AnyContext();
        }

        public async Task<bool> CopyFileAsync(string path, string targetPath, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));
            if (String.IsNullOrEmpty(targetPath))
                throw new ArgumentNullException(nameof(targetPath));

            string normalizedPath = NormalizePath(path);
            string normalizedTargetPath = NormalizePath(targetPath);
            _logger.LogInformation("Copying {Path} to {TargetPath}", normalizedPath, normalizedTargetPath);
            
            try {
                var copyResult = await Task.Factory.FromAsync(
                    _client.BeginCopyObject, 
                    _client.EndCopyResult,
                    new CopyObjectRequest(_bucket, normalizedPath, _bucket, normalizedTargetPath),
                    null
                ).AnyContext();
                return copyResult.HttpStatusCode == HttpStatusCode.OK;
            } catch (Exception ex) {
                _logger.LogError(ex, "Error copying {Path} to {TargetPath}: {Message}", normalizedPath, normalizedTargetPath, ex.Message);
                return false;
            }
        }

        public Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default) {
            if (String.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            string normalizedPath = NormalizePath(path);
            _logger.LogTrace("Deleting {Path}", normalizedPath);
            
            try {
                var deleteResult = _client.DeleteObject(_bucket, normalizedPath);
                return Task.FromResult(deleteResult.HttpStatusCode == HttpStatusCode.OK);
            } catch (Exception ex) {
                _logger.LogError(ex, "Unable to delete {Path}: File not found", normalizedPath);
                return Task.FromResult(false);
            }
        }

        public async Task<int> DeleteFilesAsync(string searchPattern = null, CancellationToken cancellation = default) {
            var files = await GetFileListAsync(searchPattern, cancellationToken: cancellation).AnyContext();
            _logger.LogInformation("Deleting {FileCount} files matching {SearchPattern}", files.Count, searchPattern);
            var result = _client.DeleteObjects(new DeleteObjectsRequest(_bucket, files.Select(spec => spec.Path).ToList()));
            if (result.HttpStatusCode != HttpStatusCode.OK) 
                throw new Exception($"[{result.HttpStatusCode}] Unable to delete files");

            int count = result.Keys?.Length ?? 0;
            _logger.LogTrace("Finished deleting {FileCount} files matching {SearchPattern}", count, searchPattern);
            return count;
        }

        public async Task<PagedFileListResult> GetPagedFileListAsync(int pageSize = 100, string searchPattern = null, CancellationToken cancellationToken = default) {
            if (pageSize <= 0)
                return PagedFileListResult.Empty;

            var result = new PagedFileListResult(_ => GetFiles(searchPattern, 1, pageSize, cancellationToken));
            await result.NextPageAsync().AnyContext();
            return result;
        }

        private async Task<NextPageResult> GetFiles(string searchPattern, int page, int pageSize, CancellationToken cancellationToken) {
            var criteria = GetRequestCriteria(searchPattern);
            
            int pagingLimit = pageSize;
            int skip = (page - 1) * pagingLimit;
            if (pagingLimit < Int32.MaxValue)
                pagingLimit++;

            _logger.LogTrace(
                s => s.Property("SearchPattern", searchPattern).Property("Limit", pagingLimit).Property("Skip", skip), 
                "Getting file list matching {Prefix} and {Pattern}...", criteria.Prefix, criteria.Pattern
            );

            string marker = null;
            int totalLimit = pagingLimit < Int32.MaxValue ? skip + pagingLimit : Int32.MaxValue;
            var blobs = new List<OssObjectSummary>();
            do {
                var listing = await Task.Factory.FromAsync(
                    _client.BeginListObjects,
                    _client.EndListObjects, 
                    new ListObjectsRequest(_bucket) {
                        Prefix = criteria.Prefix,
                        Marker = marker,
                        MaxKeys = pagingLimit
                    },
                    null
                ).AnyContext();
                marker = listing.NextMarker;

                foreach (var blob in listing.ObjectSummaries) {
                    if (criteria.Pattern != null && !criteria.Pattern.IsMatch(blob.Key)) {
                        _logger.LogTrace("Skipping {Path}: Doesn't match pattern", blob.Key);
                        continue;
                    }
                    
                    blobs.Add(blob);
                }
            } while (!cancellationToken.IsCancellationRequested && !String.IsNullOrEmpty(marker) && blobs.Count < totalLimit);

            var list = blobs
                .Skip(skip)
                .Take(pagingLimit)
                .Select(blob => new FileSpec {
                    Path = blob.Key,
                    Size = blob.Size,
                    Created = blob.LastModified,
                    Modified = blob.LastModified
                })
                .ToList();

            bool hasMore = false;
            if (list.Count == pagingLimit) {
                hasMore = true;
                list.RemoveAt(pagingLimit - 1);
            }

            return new NextPageResult {
                Success = true,
                HasMore = hasMore,
                Files = list,
                NextPageFunc = hasMore ? _ => GetFiles(searchPattern, page + 1, pageSize, cancellationToken) : null
            };
        }

        private async Task<List<FileSpec>> GetFileListAsync(string searchPattern = null, int? limit = null, int? skip = null, CancellationToken cancellationToken = default) {
            if (limit is <= 0)
                return new List<FileSpec>();
            
            var criteria = GetRequestCriteria(searchPattern);

            _logger.LogTrace(
                s => s.Property("SearchPattern", searchPattern).Property("Limit", limit).Property("Skip", skip), 
                "Getting file list matching {Prefix} and {Pattern}...", criteria.Prefix, criteria.Pattern
            );
            
            int totalLimit = limit.GetValueOrDefault(Int32.MaxValue) < Int32.MaxValue 
                ? skip.GetValueOrDefault() + limit.Value
                : Int32.MaxValue;
            
            string marker = null;
            var blobs = new List<OssObjectSummary>();
            do {
                var listing = await Task.Factory.FromAsync(
                    _client.BeginListObjects,
                    _client.EndListObjects, 
                    new ListObjectsRequest(_bucket) {
                        Prefix = criteria.Prefix,
                        Marker = marker,
                        MaxKeys = limit
                    },
                    null
                ).AnyContext();
                marker = listing.NextMarker;

                foreach (var blob in listing.ObjectSummaries) {
                    if (criteria.Pattern != null && !criteria.Pattern.IsMatch(blob.Key)) {
                        _logger.LogTrace("Skipping {Path}: Doesn't match pattern", blob.Key);
                        continue;
                    }
                    
                    blobs.Add(blob);
                }
            } while (!cancellationToken.IsCancellationRequested && !String.IsNullOrEmpty(marker) && blobs.Count < totalLimit);

            if (skip.HasValue)
                blobs = blobs.Skip(skip.Value).ToList();
            
            if (limit.HasValue)
                blobs = blobs.Take(limit.Value).ToList();

            return blobs.Select(blob => new FileSpec {
                Path = blob.Key,
                Size = blob.Size,
                Created = blob.LastModified,
                Modified = blob.LastModified
            }).ToList();
        }

        private class SearchCriteria {
            public string Prefix { get; set; }
            public Regex Pattern { get; set; }
        }

        private SearchCriteria GetRequestCriteria(string searchPattern) {
            if (String.IsNullOrEmpty(searchPattern))
                return new SearchCriteria { Prefix = String.Empty };
            
            string normalizedSearchPattern = NormalizePath(searchPattern);
            int wildcardPos = normalizedSearchPattern.IndexOf('*');
            bool hasWildcard = wildcardPos >= 0;

            string prefix = normalizedSearchPattern;
            Regex patternRegex = null;
            
            if (hasWildcard) {
                patternRegex = new Regex($"^{Regex.Escape(normalizedSearchPattern).Replace("\\*", ".*?")}$");
                int slashPos = normalizedSearchPattern.LastIndexOf('/');
                prefix = slashPos >= 0 ? normalizedSearchPattern.Substring(0, slashPos) : String.Empty;
            }

            return new SearchCriteria {
                Prefix = prefix,
                Pattern = patternRegex
            };
        }
        
        public void Dispose() { }

        private bool IsNotFoundException(Exception ex) {
            if (ex is AggregateException aggregateException) {
                foreach (var innerException in aggregateException.InnerExceptions) {
                    if (IsNotFoundException(innerException))
                        return true;
                }
            }

            if (ex is WebException { Response: HttpWebResponse response })
                return response.StatusCode == HttpStatusCode.NotFound;

            return false;
        }

        private string NormalizePath(string path) {
            return path?.Replace('\\', '/');
        }

        private bool DoesBucketExist(string bucketName) {
            _logger.LogTrace("Checking if bucket {Bucket} exists", _bucket);
            try {
                return _client.DoesBucketExist(bucketName);
            } catch (Exception ex) when (IsNotFoundException(ex)) {
                _logger.LogError(ex, "Unable to check if {Bucket} bucket exists: {Message}", bucketName, ex.Message);
                return false;
            }
        }
    }
}