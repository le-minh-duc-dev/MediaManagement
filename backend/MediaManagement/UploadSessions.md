# Upload sessions

The API creates one session containing multiple media assets. Clients upload raw files directly to S3 and then submit the final list of successful asset IDs. Entity classes remain simple; `Uploads/UploadSessionService.cs` owns validation and state transitions.

## Configure and run

1. Configure `Uploads:BucketName` and `Uploads:Region` (environment variables: `Uploads__BucketName`, `Uploads__Region`). Use a private, general-purpose S3 bucket with **versioning disabled** for this implementation. `DeleteObject` removes the object in an unversioned bucket; in a versioned bucket it only creates a delete marker, so physical version cleanup would require additional implementation/lifecycle policy.
2. Supply AWS credentials through the SDK's standard credential chain: a local AWS profile for development, or an IAM role in AWS. Do not put access keys in appsettings or send them to clients.
3. Configure `Authentication:Authority` and `Authentication:Audience` for your JWT issuer. Tokens must have a GUID `sub` (or GUID name-identifier claim). Ownership is derived from the validated token; requests cannot specify `ownerId`. An issuer with non-GUID subjects needs a user-ID mapping before using these endpoints.
4. Apply the migration from `backend/MediaManagement`:

   ```powershell
   dotnet ef database update --project MediaManagement/MediaManagement.csproj
   dotnet run --project MediaManagement/MediaManagement.csproj
   ```

   This repository had no previous migrations. `InitialMediaSchema` creates the complete existing model plus upload fields. For a database created previously with `EnsureCreated`, baseline/reconcile its schema before applying this migration; do not drop an existing database to make it fit. The API deliberately does not migrate databases automatically at startup.

5. Permit the frontend's actual origin in the API CORS policy (`DependencyInjection.cs`) and configure S3 bucket CORS separately:

   ```json
   [
     {
       "AllowedOrigins": ["https://myfrontend.com"],
       "AllowedMethods": ["PUT"],
       "AllowedHeaders": ["content-type", "if-none-match"],
       "ExposeHeaders": ["ETag"],
       "MaxAgeSeconds": 300
     }
   ]
   ```

The backend role needs `s3:PutObject`, `s3:GetObject` (HEAD verification), and `s3:DeleteObject` on `arn:aws:s3:::YOUR_BUCKET/uploads/*`. `s3:ListBucket` on the bucket permits S3 to return 404 for missing objects; without it, HEAD can return 403 and the API treats that as an infrastructure failure. Restrict the role to the application's bucket. Keep S3 public access blocked. SSE-KMS buckets also require the appropriate KMS grants.

## Client flow

All API calls require `Authorization: Bearer <token>`.

### 1. Create a session

`POST /api/v1/upload-sessions`

```json
{
  "items": [
    { "fileName": "photo.jpg", "sizeBytes": 123456, "contentType": "image/jpeg" },
    { "fileName": "clip.mp4", "sizeBytes": 987654, "contentType": "video/mp4" }
  ]
}
```

Returns **201 Created**, a `Location` pointing to the session, and:

```json
{
  "id": "a17ecf41-088b-47f1-bc29-af24b8540403",
  "expiresAt": "2026-09-27T01:00:00+00:00",
  "items": [
    {
      "mediaAssetId": "89cc85e9-9444-4c7d-aa46-dd76d1d774be",
      "fileName": "photo.jpg",
      "sizeBytes": 123456,
      "contentType": "image/jpeg",
      "uploadUrl": "https://YOUR_BUCKET.s3.ap-southeast-1.amazonaws.com/uploads/...?X-Amz-...",
      "method": "PUT",
      "headers": { "Content-Type": "image/jpeg", "If-None-Match": "*" },
      "urlExpiresAt": "2026-09-27T00:15:00+00:00"
    }
  ]
}
```

The example abbreviates the response to one item. Actual results contain one target for every input item, in the same order. Duplicate filenames are supported; use `mediaAssetId` and input position to correlate files. Object keys use server-generated IDs and never contain filenames.

Defaults: 1–20 items, 1–104857600 bytes per file (100 MiB), JPEG/PNG/WebP/GIF/MP4 content types. Limits are configurable. This is single-PUT upload, not S3 multipart upload. Declared size is verified at completion; the URL does not impose a transport-level size quota. MIME metadata is not proof of file contents; content scanning/transcoding is outside this flow.

### 2. Upload each raw file

```javascript
const response = await fetch(target.uploadUrl, {
  method: target.method,
  headers: target.headers,
  body: file,
});
```

Send the returned headers exactly. Do not use `FormData`, add the API bearer token, or log/share the signed URL. Signed URLs expire after 15 minutes by default (temporary AWS credentials can expire earlier). `If-None-Match: *` prevents replay from overwriting an existing object after verification. If a PUT succeeded but its response was lost, a retry can return 412; ask the completion API to verify that asset rather than attempting to overwrite it.

### 3. Finalize the successful subset

`POST /api/v1/upload-sessions/{id}/complete`

```json
{
  "uploadedMediaAssetIds": ["89cc85e9-9444-4c7d-aa46-dd76d1d774be"]
}
```

The API checks ownership, membership, duplicate IDs, session expiry, and S3 object existence/size/content type. Every reported asset must pass before anything is marked uploaded. A failed verification leaves the session pending so the client can retry within its one-hour window.

Returns **200 OK** with `{ id, status, expiresAt, completedAt, items }`; each item contains `{ mediaAssetId, fileName, sizeBytes, contentType, uploaded }`. A nonempty verified subset makes the session `Completed`; an empty list explicitly finalizes it as `Failed`. Omitted assets are final failures, even if their objects exist in S3. Wait until all client upload attempts have settled before finalizing. To upload more files after finalization, create a new session.

Identical completion retries return success, including after cleanup. A different final asset set returns 409. Concurrent requests cannot replace the winning set. Unknown/other-owner sessions return 404. Bad request items return the existing localizable 400 ProblemDetails envelope. Verification, expiry, and finalization conflicts return 409 with stable `upload.*` error codes. S3/network/permission failures propagate through the existing exception handler rather than being treated as successful uploads.

`GET /api/v1/upload-sessions/{id}` returns the current session and remaining assets, without reissuing upload URLs. Only verified (`uploaded: true`) assets should be eligible for future post attachment.

## Cleanup behavior

`UploadCleanupWorker` runs at startup and every five minutes, resolving a fresh scope for each batch. Starting 23 hours after creation, it expires abandoned sessions, deletes unconfirmed objects, then removes their media rows. Confirmed objects and media rows are preserved. Session records remain for auditing and idempotent completion. Successful cleanup records `CleanedUpAt`.

Deletion failures preserve the database rows and schedule another attempt in five minutes. A failing session does not prevent processing other sessions. Batches contain at most 100 sessions and are drained while work remains. Concurrency tokens prevent stale completion writes from reviving sessions selected for cleanup. Multiple worker instances may repeat an S3 delete safely.

The default schedule targets removal before 24 hours while the application, database, and S3 are available; outages or a backlog can exceed that target. Keep at least one backend instance running and alert on cleanup errors/overdue `NextCleanupAt`. Do not scale the only worker to zero. Upload URLs expire well before cleanup, leaving a long grace period for in-flight PUTs. S3 checks URL expiry when a request starts, so a pathological PUT still running at cleanup could finish afterwards; strict handling of that case needs S3 object-created event reconciliation. No cloud infrastructure or event integration is provisioned by this change.

## Verification

Run `dotnet test MediaManagement.slnx`. Upload tests use the real HTTP pipeline, SQLite migrations, fake time/storage, and a real AWS signer with dummy credentials. They cover batch persistence, validation, ownership, partial and empty completion, metadata rejection without partial updates, idempotency, competing completion requests, expiry, cleanup retries, and preservation of successful assets. They do not prove live AWS permissions, bucket CORS, or networking; exercise one full browser-to-S3 upload with deployment credentials before release.

AWS references: [presigned URLs](https://docs.aws.amazon.com/AmazonS3/latest/userguide/using-presigned-url.html), [conditional writes](https://docs.aws.amazon.com/AmazonS3/latest/userguide/conditional-writes.html), [SDK presigning request](https://docs.aws.amazon.com/sdkfornet/v4/apidocs/items/S3/TGetPreSignedUrlRequest.html).
