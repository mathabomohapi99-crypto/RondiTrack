namespace RondiTrack.Domain;

// NEW 9.1: the proof of payment (photo or PDF) of ONE contribution. At most one per contribution.
public sealed class ProofOfPayment
{
    public Guid Id { get; private set; }
    public Guid ContributionId { get; private set; }
    public Guid StokvelId { get; private set; }          // copied here so access checks never guess the stokvel
    public Guid UploadedByUserId { get; private set; }
    public string ContentType { get; private set; } = null!;   // decided from the file's BYTES, never from the browser
    public string FileName { get; private set; } = null!;      // cleaned original name, only for showing
    public long SizeBytes { get; private set; }
    public byte[] Data { get; private set; } = null!;
    public DateTime UploadedAtUtc { get; private set; }

    private ProofOfPayment() { }   // for EF only

    public ProofOfPayment(Guid contributionId, Guid stokvelId, Guid uploadedByUserId,
        string contentType, string fileName, byte[] data, DateTime uploadedAtUtc)
    {
        Id = Guid.NewGuid();
        ContributionId = contributionId;
        StokvelId = stokvelId;
        Replace(uploadedByUserId, contentType, fileName, data, uploadedAtUtc);
    }

    // a new upload replaces the old file (only allowed while the cycle is open: the service checks)
    public void Replace(Guid uploadedByUserId, string contentType, string fileName, byte[] data, DateTime uploadedAtUtc)
    {
        UploadedByUserId = uploadedByUserId;
        ContentType = contentType;
        FileName = fileName;
        Data = data;
        SizeBytes = data.LongLength;
        UploadedAtUtc = uploadedAtUtc;
    }
}