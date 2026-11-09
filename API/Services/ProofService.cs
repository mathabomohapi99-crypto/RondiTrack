using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

// NEW 9.1: the rules for a proof of payment, in one place
public static class ProofRules
{
    public const long MaxBytes = 5 * 1024 * 1024;               // 5 MB: the file limit
    public const long MaxRequestBytes = MaxBytes + 64 * 1024;   // plus room for the multipart wrapper
    public const string NotFoundText = "Proof of payment was not found.";   // ONE text for every "no", so nobody can tell why

    // Looks at the first bytes of the file. The file name and the browser's Content-Type are never used.
    public static string? DetectType(ReadOnlySpan<byte> b)
    {
        if (b.StartsWith("%PDF-"u8)) return "application/pdf";
        if (b.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })) return "image/jpeg";
        if (b.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) && b.Slice(8, 4).SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public static string ExtensionFor(string contentType) => contentType switch
    {
        "application/pdf" => ".pdf",
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}

public sealed record ProofFile(byte[] Data, string ContentType, string DownloadName);

public interface IProofService
{
    Task<ProofResponse> SaveAsync(Guid callerId, Guid stokvelId, Guid contributionId, IFormFile? file, CancellationToken ct);
    Task<ProofFile> GetAsync(Guid callerId, Guid stokvelId, Guid contributionId, CancellationToken ct);
    Task DeleteAsync(Guid callerId, Guid stokvelId, Guid contributionId, CancellationToken ct);
    Task<IReadOnlyList<ProofResponse>> ListAsync(Guid callerId, Guid stokvelId, Guid? cycleId, CancellationToken ct);
}

public sealed class ProofService(RondiTrackDbContext db, TimeProvider time) : IProofService
{
    private static DomainNotFoundException NotFound() => new(ProofRules.NotFoundText);

    // a field error on "file", so the form can show it under the input
    private static ValidationException Invalid(string message) =>
        new(new[] { new ValidationFailure("file", message) });

    private static CodedDomainException Locked() =>
        new(409, "proof_locked", "This cycle is closed, so the proof of payment can no longer be changed.");

    // The caller's role IN THIS STOKVEL decides everything. Not a member of it = the same 404 as a missing file.
    private async Task<(Contribution Contribution, bool IsOfficer)> ResolveAsync(
        Guid callerId, Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        var role = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.UserId == callerId && m.StokvelId == stokvelId)
            .Select(m => (StokvelRole?)m.Role).FirstOrDefaultAsync(ct);
        if (role is null) throw NotFound();

        var contribution = await db.Contributions.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contributionId && c.StokvelId == stokvelId, ct) ?? throw NotFound();

        return (contribution, role is StokvelRole.Treasurer or StokvelRole.Admin);
    }

    private Task<bool> IsLockedAsync(Guid cycleId, CancellationToken ct) =>
        db.ContributionCycles.AsNoTracking().Where(c => c.Id == cycleId)
            .Select(c => c.ClosedAtUtc != null || c.Status != CycleStatus.Open).FirstOrDefaultAsync(ct);

    private static string CleanName(string? name)
    {
        var n = new string(Path.GetFileName(name ?? "").Where(ch => !char.IsControl(ch)).ToArray()).Trim();
        if (n.Length == 0) n = "proof";
        return n.Length > 100 ? n[..100] : n;
    }

    public async Task<ProofResponse> SaveAsync(
        Guid callerId, Guid stokvelId, Guid contributionId, IFormFile? file, CancellationToken ct)
    {
        var (contribution, officer) = await ResolveAsync(callerId, stokvelId, contributionId, ct);
        if (!officer && contribution.UserId != callerId) throw NotFound();   // someone else's contribution
        if (await IsLockedAsync(contribution.CycleId, ct)) throw Locked();

        // The API checks again (the browser's check is only a courtesy)
        if (file is null || file.Length == 0) throw Invalid("Choose a file to upload.");
        if (file.Length > ProofRules.MaxBytes) throw Invalid("That file is too large. The most we accept is 5 MB.");

        byte[] data;
        using (var ms = new MemoryStream((int)file.Length))
        {
            await file.CopyToAsync(ms, ct);
            data = ms.ToArray();
        }

        var type = ProofRules.DetectType(data)
            ?? throw Invalid("That file is not a photo (JPEG, PNG or WebP) or a PDF.");   // judged by the BYTES

        var now = time.GetUtcNow().UtcDateTime;
        var name = CleanName(file.FileName);
        var proof = await db.ProofsOfPayment.FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct);
        if (proof is null)
        {
            proof = new ProofOfPayment(contributionId, stokvelId, callerId, type, name, data, now);
            db.ProofsOfPayment.Add(proof);
        }
        else
        {
            proof.Replace(callerId, type, name, data, now);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DomainConflictException("Another upload for this contribution just finished. Please try again.");
        }

        return new ProofResponse(contributionId, proof.FileName, proof.ContentType, proof.SizeBytes, proof.UploadedAtUtc, false);
    }

    public async Task<ProofFile> GetAsync(Guid callerId, Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        var (contribution, officer) = await ResolveAsync(callerId, stokvelId, contributionId, ct);
        var proof = await db.ProofsOfPayment.AsNoTracking().FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct)
            ?? throw NotFound();

        // only the uploader, the member the contribution belongs to, or a treasurer of THIS stokvel
        if (!(officer || contribution.UserId == callerId || proof.UploadedByUserId == callerId)) throw NotFound();

        return new ProofFile(proof.Data, proof.ContentType, "proof-of-payment" + ProofRules.ExtensionFor(proof.ContentType));
    }

    public async Task DeleteAsync(Guid callerId, Guid stokvelId, Guid contributionId, CancellationToken ct)
    {
        var (contribution, officer) = await ResolveAsync(callerId, stokvelId, contributionId, ct);
        var proof = await db.ProofsOfPayment.FirstOrDefaultAsync(p => p.ContributionId == contributionId, ct) ?? throw NotFound();
        if (!(officer || contribution.UserId == callerId || proof.UploadedByUserId == callerId)) throw NotFound();
        if (await IsLockedAsync(contribution.CycleId, ct)) throw Locked();

        db.ProofsOfPayment.Remove(proof);
        await db.SaveChangesAsync(ct);
    }

    // Which contributions have a proof the CALLER may open? (no file bytes are read)
    public async Task<IReadOnlyList<ProofResponse>> ListAsync(Guid callerId, Guid stokvelId, Guid? cycleId, CancellationToken ct)
    {
        var role = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.UserId == callerId && m.StokvelId == stokvelId)
            .Select(m => (StokvelRole?)m.Role).FirstOrDefaultAsync(ct);
        if (role is null) throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");
        var officer = role is StokvelRole.Treasurer or StokvelRole.Admin;

        var rows = await (
            from p in db.ProofsOfPayment.AsNoTracking()
            join c in db.Contributions.AsNoTracking() on p.ContributionId equals c.Id
            join cy in db.ContributionCycles.AsNoTracking() on c.CycleId equals cy.Id
            where p.StokvelId == stokvelId
                  && (cycleId == null || c.CycleId == cycleId)
                  && (officer || p.UploadedByUserId == callerId || c.UserId == callerId)
            select new
            {
                p.ContributionId, p.FileName, p.ContentType, p.SizeBytes, p.UploadedAtUtc,
                Locked = cy.ClosedAtUtc != null || cy.Status != CycleStatus.Open
            }).ToListAsync(ct);

        return rows.Select(r => new ProofResponse(r.ContributionId, r.FileName, r.ContentType, r.SizeBytes, r.UploadedAtUtc, r.Locked)).ToList();
    }
}