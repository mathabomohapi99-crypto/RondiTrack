using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Common;
using RondiTrack.Data;
using RondiTrack.Domain;
using RondiTrack.Dtos;

namespace RondiTrack.Services;

public interface IInviteService
{
    Task<InviteResponse> CreateAsync(Guid stokvelId, Guid callerId, CancellationToken ct);
    Task<InvitePreviewResponse> PreviewAsync(string code, Guid callerId, CancellationToken ct);
    Task<JoinInviteResponse> JoinAsync(string code, Guid callerId, CancellationToken ct);
}

// NEW 9.1: create, preview and use invite codes
public sealed class InviteService(
    RondiTrackDbContext db, IStokvelRepository stokvels, IUserRepository users, TimeProvider time) : IInviteService
{
    // no 0, O, 1, I or L, so a code read from WhatsApp is hard to mistype
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private static CodedDomainException NotFound() => new(404, "invite_not_found", "We could not find that invite code.");
    private static CodedDomainException AlreadyMember() => new(409, "already_member", "You are already a member of this stokvel.");

    private static string NewCode() =>
        string.Create(Invite.CodeLength, 0, (span, _) =>
        {
            for (var i = 0; i < span.Length; i++) span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        });

    // "k7m2 9qxp" and "K7M2-9QXP" both become K7M29QXP. Wrong length = not found.
    private static string? Normalize(string raw)
    {
        var code = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return code.Length == Invite.CodeLength ? code : null;
    }

    // Unknown code = 404. Expired or revoked = 410. Same two answers for preview and join.
    private async Task<Invite> FindUsableAsync(string raw, CancellationToken ct)
    {
        var code = Normalize(raw) ?? throw NotFound();
        var invite = await db.Invites.AsNoTracking().FirstOrDefaultAsync(i => i.Code == code, ct) ?? throw NotFound();
        if (!invite.IsUsableAt(time.GetUtcNow().UtcDateTime))
            throw new CodedDomainException(410, "invite_expired", "That invite has expired. Ask for a new one.");
        return invite;
    }

    public async Task<InviteResponse> CreateAsync(Guid stokvelId, Guid callerId, CancellationToken ct)
    {
        if (!await db.Stokvels.AnyAsync(s => s.Id == stokvelId, ct))
            throw new DomainNotFoundException($"Stokvel {stokvelId} was not found.");

        var now = time.GetUtcNow().UtcDateTime;
        string code;
        do { code = NewCode(); } while (await db.Invites.AnyAsync(i => i.Code == code, ct));   // the unique index is the final guard

        var invite = new Invite(stokvelId, callerId, code, now, Lifetime);
        db.Invites.Add(invite);
        await db.SaveChangesAsync(ct);
        return new InviteResponse(invite.Code, invite.ExpiresAtUtc);
    }

    public async Task<InvitePreviewResponse> PreviewAsync(string rawCode, Guid callerId, CancellationToken ct)
    {
        var invite = await FindUsableAsync(rawCode, ct);

        // ONLY what someone deciding to join needs. No member names, balances or payout order are even read.
        var stokvel = await db.Stokvels.AsNoTracking().Where(s => s.Id == invite.StokvelId)
            .Select(s => new { s.Name, s.ContributionAmount, s.Frequency, s.MaxMembers })
            .FirstOrDefaultAsync(ct) ?? throw NotFound();
        var count = await db.StokvelMembers.CountAsync(m => m.StokvelId == invite.StokvelId, ct);
        var already = await db.StokvelMembers.AnyAsync(m => m.StokvelId == invite.StokvelId && m.UserId == callerId, ct);

        return new InvitePreviewResponse(
            stokvel.Name, Money.ToCents(stokvel.ContributionAmount), stokvel.Frequency,
            count, stokvel.MaxMembers, invite.ExpiresAtUtc,
            already, already ? invite.StokvelId : null);   // the stokvel id is shown only to its own members
    }

    public async Task<JoinInviteResponse> JoinAsync(string rawCode, Guid callerId, CancellationToken ct)
    {
        var invite = await FindUsableAsync(rawCode, ct);
        var stokvel = await stokvels.GetByIdAsync(invite.StokvelId) ?? throw NotFound();

        if (stokvel.HasMember(callerId)) throw AlreadyMember();
        if (stokvel.IsFull) throw new CodedDomainException(409, "stokvel_full", "This stokvel is full.");

        var user = await users.GetByIdAsync(callerId) ?? throw new DomainNotFoundException("User was not found.");
        stokvel.AddMember(user);

        try
        {
            await stokvels.UpdateAsync(stokvel);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException
            || ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A double-click: another request joined this person a moment ago. One membership, not two.
            if (await db.StokvelMembers.AsNoTracking().AnyAsync(m => m.UserId == callerId && m.StokvelId == stokvel.Id, ct))
                throw AlreadyMember();
            throw new DomainConflictException("Someone joined at the same moment. Please try again.");
        }

        return new JoinInviteResponse(stokvel.Id);
    }
}