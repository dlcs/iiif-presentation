namespace Models.Database.Collections;

public interface IHaveEtag
{
    /// <summary>
    /// Stored ETag value, mandatory, application managed
    /// </summary>
    public Guid Etag { get; set; }
}
