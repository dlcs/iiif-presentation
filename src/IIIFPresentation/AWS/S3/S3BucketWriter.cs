using System.Diagnostics;
using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using AWS.S3.Models;
using Microsoft.Extensions.Logging;

namespace AWS.S3;

public class S3BucketWriter : IBucketWriter
{
    private readonly IAmazonS3 s3Client;
    private readonly ILogger<S3BucketWriter> logger;

    public S3BucketWriter(IAmazonS3 s3Client, ILogger<S3BucketWriter> logger)
    {
        this.s3Client = s3Client;
        this.logger = logger;
    }

    public async Task WriteToBucket(ObjectInBucket dest, string content, string contentType,
        CancellationToken cancellationToken = default)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = dest.Bucket,
            Key = dest.Key,
            ContentBody = content,
            ContentType = contentType
        };

        PutObjectResponse? response = await WriteToBucketInternal(putRequest, cancellationToken);
    }

    public async Task<bool> WriteToBucket(ObjectInBucket dest, Stream content, string? contentType = null)
    {
        var putRequest = new PutObjectRequest
        {
            BucketName = dest.Bucket,
            Key = dest.Key,
            InputStream = content,
        };

        if (!string.IsNullOrEmpty(contentType)) putRequest.ContentType = contentType;

        var response = await WriteToBucketInternal(putRequest);
        return response != null;
    }

    public async Task<bool> WriteFileToBucket(ObjectInBucket dest, string filePath, string? contentType = null,
        CancellationToken token = default)
    {
        try
        {
            // Transfer utility uses multi-part upload internally if the file is large enough to warrant it (>16MB)
            var uploadRequest = new TransferUtilityUploadRequest
            {
                BucketName = dest.Bucket,
                Key = dest.Key,
                FilePath = filePath,
            };

            if (!string.IsNullOrEmpty(contentType)) uploadRequest.ContentType = contentType;
                
            using var transferUtil = new TransferUtility(s3Client);
            await transferUtil.UploadAsync(uploadRequest, token);
            return true;
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "S3 Error encountered writing file to bucket. Key: '{S3Key}'", dest);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Unknown error encountered writing file to bucket. Key: '{S3Key}'", dest);
        }

        return false;
    }

    public async Task DeleteFromBucket(params ObjectInBucket[] toDelete)
    {
        try
        {
            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                BucketName = toDelete[0].Bucket,
                Objects = toDelete.Select(oib => new KeyVersion { Key = oib.Key }).ToList(),
            };

            await s3Client.DeleteObjectsAsync(deleteObjectsRequest);
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "S3 Error encountered. Message:'{Message}' when deleting objects from bucket",
                e.Message);
        }
        catch (Exception e)
        {
            logger.LogWarning(e,
                "Unknown encountered on server. Message:'{Message}' when deleting objects from bucket", e.Message);
        }
    }

    public async Task DeleteFolder(ObjectInBucket root, bool deleteRoot)
    {
        // NOTE - this is based on the S3DirectoryInfo.Delete method that was removed from SDK
        try
        {
            var listObjectsRequest = new ListObjectsRequest
            {
                BucketName = root.Bucket,
                Prefix = root.Key
            };

            var deleteObjectsRequest = new DeleteObjectsRequest
            {
                BucketName = root.Bucket
            };

            if (deleteRoot && root.Key != null) deleteObjectsRequest.AddKey(root.Key.TrimEnd('/'));

            ListObjectsResponse listObjectsResponse;
            do
            {
                listObjectsResponse = await s3Client.ListObjectsAsync(listObjectsRequest);
                foreach (var item in listObjectsResponse.S3Objects.OrderBy(x => x.Key))
                {
                    deleteObjectsRequest.AddKey(item.Key);
                    if (deleteObjectsRequest.Objects.Count == 1000)
                    {
                        await s3Client.DeleteObjectsAsync(deleteObjectsRequest);
                        deleteObjectsRequest.Objects.Clear();
                    }

                    listObjectsRequest.Marker = item.Key;
                }
            } while (listObjectsResponse.IsTruncated);
            
            if (deleteObjectsRequest.Objects.Count > 0)
            {
                await s3Client.DeleteObjectsAsync(deleteObjectsRequest);
            }
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning("S3 Error encountered. Message:'{Message}' when deleting folder '{Folder}' from bucket",
                e.Message, root);
        }
        catch (Exception e)
        {
            logger.LogWarning(e,
                "Unknown encountered on server. Message:'{Message}' when deleting folder '{Folder}' from bucket",
                e.Message, root);
        }
    }

    private async Task<PutObjectResponse?> WriteToBucketInternal(PutObjectRequest putRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            PutObjectResponse response = await s3Client.PutObjectAsync(putRequest, cancellationToken);
            return response;
        }
        catch (AmazonS3Exception e)
        {
            logger.LogWarning(e, "S3 Error encountered. Message:'{Message}' when writing an object", e.Message);
            return null;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Unknown encountered on server. Message:'{Message}' when writing an object",
                e.Message);
            return null;
        }
    }
}