namespace MediaManagement.Entities;

public static class BusinessErrorCodes
{
    public const string NotFound = "upload.object_missing";
    public const string Expired = "upload.session_expired";
    public const string Exists = "resource.exists";
    public const string Invalid = "request.invalid";
    public const string Mismatch = "resource.mismatch";

    public static class UploadSession
    {
        public const string NotFound = "resource.not_found";
        public const string Expired = "upload.session_expired";
        public const string Invalid = "request.invalid";
        public const string AssetNotInSession = "upload.asset_not_in_session";
        public const string AssetIdsDuplicate = "upload.asset_ids.duplicate";
        public const string ObjectMetadataMismatch = "upload.object_metadata_mismatch";
        public const string UploadedMediaAssetIdsMismatch = "upload.assets.mismatch";
        public const string StatusExpired = "upload.session_expired";
    }

    public static class Post
    {
        public const string NotFound = "Post:NotFound";
        public const string MediaAssetIdsMismatch = "Post:MediaAssetIds:Mismatch";
        public const string TagsMismatch = "Post:Tags:Mismatch";
    }
}
