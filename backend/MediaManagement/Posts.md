# Posts API

All endpoints require authentication with a nonempty GUID `sub` or name-identifier claim. Posts are scoped to that owner; another owner's post returns 404.

| Method | Route | Response |
| --- | --- | --- |
| POST | `/api/v1/posts` | 201, post details and a Location header |
| GET | `/api/v1/posts/{id}` | 200, post details |
| GET | `/api/v1/posts?pageNumber=1&pageSize=20` | 200, `{ items, pageNumber, pageSize, totalCount }` |
| PUT | `/api/v1/posts/{id}` | 200, updated post details |

POST and PUT accept the same body:

```json
{
  "caption": "A day at the beach",
  "items": [
    { "mediaAssetId": "<completed-upload-asset-guid>", "altText": "Waves at sunset" }
  ],
  "tagIds": []
}
```

`items` is required with 1–100 distinct media assets. Each asset must belong to the caller, have an `UploadedAt` value, and belong to a completed upload session. Partial upload completion is supported: only the successful assets can be attached. Assets may be reused in multiple posts. The request array defines zero-based item order.

`caption` is nullable with a maximum of 5,000 characters; `altText` is nullable with a maximum of 1,000. `tagIds` is required, allows up to 30 distinct existing tag IDs, and may be empty. Tag creation is outside these endpoints.

PUT replaces caption, item membership/order/alt text, and tag membership. It preserves the post ID, creation time, and IDs of retained items. Removing an item does not delete its media asset. Updates use last-write-wins semantics; no client revision precondition is provided.

Pagination defaults to page 1 and 20 items, accepts sizes 1–100, and sorts by creation time descending then ID descending. Pages beyond the end return an empty array. Offset pagination can shift when posts are inserted between requests; the count and page are separate database queries.

Validation and business errors use the existing Problem Details envelope with stable `post.*` codes. Controllers map application `Result<T>` values using the existing response extensions. Services validate direct callers too; repositories handle EF queries and persistence.

Apply the new migration before running against an existing database, from `backend/MediaManagement`:

```powershell
dotnet ef database update --project MediaManagement
dotnet test MediaManagement.slnx
```

`AddPostPagination` converts existing post timestamps to UTC ticks and adds an owner/creation-time/ID index for SQLite pagination.
