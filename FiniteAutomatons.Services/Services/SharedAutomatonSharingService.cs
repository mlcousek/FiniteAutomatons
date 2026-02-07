using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace FiniteAutomatons.Services.Services;

public class SharedAutomatonSharingService(
    ApplicationDbContext context,
    ISharedAutomatonService sharedAutomatonService,
    UserManager<IdentityUser> userManager,
    ILogger<SharedAutomatonSharingService> logger) : ISharedAutomatonSharingService
{
    private readonly ApplicationDbContext context = context;
    private readonly ISharedAutomatonService sharedAutomatonService = sharedAutomatonService;
    private readonly UserManager<IdentityUser> userManager = userManager;
    private readonly ILogger<SharedAutomatonSharingService> logger = logger;

    #region Email Invitations

    public async Task<SharedAutomatonGroupInvitation> InviteByEmailAsync(int groupId, string inviterUserId, string email, SharedGroupRole role, int expirationDays = 7)
    {
        ArgumentNullException.ThrowIfNull(inviterUserId);
        ArgumentNullException.ThrowIfNull(email);

        // Validate email format
        if (!IsValidEmail(email))
        {
            throw new ArgumentException($"Invalid email format: {email}");
        }

        // Check permission - must be Admin or Owner
        if (!await sharedAutomatonService.CanUserManageMembersAsync(groupId, inviterUserId))
        {
            throw new UnauthorizedAccessException($"User {inviterUserId} does not have permission to invite members to group {groupId}");
        }

        // Check if user is already a member
        var existingMember = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == email);

        if (existingMember != null)
        {
            throw new InvalidOperationException($"User {email} is already a member of group {groupId}");
        }

        // Check for existing pending invitation
        var existingInvitation = await context.SharedAutomatonGroupInvitations
            .FirstOrDefaultAsync(i => i.GroupId == groupId && i.Email == email && i.Status == InvitationStatus.Pending);

        if (existingInvitation != null)
        {
            throw new InvalidOperationException($"There is already a pending invitation for {email} to group {groupId}");
        }

        // Generate unique token
        var token = GenerateUniqueToken();

        var invitation = new SharedAutomatonGroupInvitation
        {
            GroupId = groupId,
            Email = email.ToLowerInvariant(),
            Role = role,
            InvitedByUserId = inviterUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(expirationDays),
            Token = token,
            Status = InvitationStatus.Pending
        };

        context.SharedAutomatonGroupInvitations.Add(invitation);
        await context.SaveChangesAsync();

        logger.LogInformation("User {InviterUserId} invited {Email} to group {GroupId} with role {Role}",
            inviterUserId, email, groupId, role);

        return invitation;
    }

    public async Task<List<SharedAutomatonGroupInvitation>> ListPendingInvitationsAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Check permission
        if (!await sharedAutomatonService.CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to view invitations for group {groupId}");
        }

        return await context.SharedAutomatonGroupInvitations
            .Where(i => i.GroupId == groupId && i.Status == InvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<SharedAutomatonGroupInvitation?> GetInvitationByTokenAsync(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var invitation = await context.SharedAutomatonGroupInvitations
            .Include(i => i.Group)
            .FirstOrDefaultAsync(i => i.Token == token);

        if (invitation == null)
            return null;

        // Check expiration
        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
        {
            // Auto-expire
            if (invitation.Status == InvitationStatus.Pending)
            {
                invitation.Status = InvitationStatus.Expired;
                await context.SaveChangesAsync();
            }
        }

        return invitation;
    }

    public async Task AcceptInvitationAsync(string token, string userId)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(userId);

        var invitation = await GetInvitationByTokenAsync(token);

        if (invitation == null)
        {
            throw new InvalidOperationException("Invitation not found");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Invitation is not pending (status: {invitation.Status})");
        }

        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
        {
            invitation.Status = InvitationStatus.Expired;
            await context.SaveChangesAsync();
            throw new InvalidOperationException("Invitation has expired");
        }

        // Get user email to verify
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        // Verify email matches
        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Invitation email ({invitation.Email}) does not match user email ({user.Email})");
        }

        // Check if already a member
        var existingMember = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == invitation.GroupId && m.UserId == userId);

        if (existingMember != null)
        {
            throw new InvalidOperationException($"User is already a member of group {invitation.GroupId}");
        }

        // Add to group
        var member = new SharedAutomatonGroupMember
        {
            GroupId = invitation.GroupId,
            UserId = userId,
            Role = invitation.Role,
            JoinedAt = DateTime.UtcNow,
            InvitedByUserId = invitation.InvitedByUserId
        };

        context.SharedAutomatonGroupMembers.Add(member);

        // Mark invitation as accepted
        invitation.Status = InvitationStatus.Accepted;
        invitation.ResponsedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} accepted invitation {InvitationId} and joined group {GroupId}",
            userId, invitation.Id, invitation.GroupId);
    }

    public async Task DeclineInvitationAsync(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var invitation = await GetInvitationByTokenAsync(token);

        if (invitation == null)
        {
            throw new InvalidOperationException("Invitation not found");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Invitation is not pending (status: {invitation.Status})");
        }

        invitation.Status = InvitationStatus.Declined;
        invitation.ResponsedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("Invitation {InvitationId} for email {Email} was declined",
            invitation.Id, invitation.Email);
    }

    public async Task CancelInvitationAsync(int invitationId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var invitation = await context.SharedAutomatonGroupInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId);

        if (invitation == null)
        {
            throw new InvalidOperationException($"Invitation {invitationId} not found");
        }

        // Check permission
        if (!await sharedAutomatonService.CanUserManageMembersAsync(invitation.GroupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to cancel invitations for group {invitation.GroupId}");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot cancel invitation with status {invitation.Status}");
        }

        invitation.Status = InvitationStatus.Cancelled;
        invitation.ResponsedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} cancelled invitation {InvitationId} for email {Email}",
            userId, invitationId, invitation.Email);
    }

    #endregion

    #region Shareable Links

    public async Task<string> GenerateInviteLinkAsync(int groupId, string userId, SharedGroupRole defaultRole, int? expirationDays = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Check permission
        if (!await sharedAutomatonService.CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to generate invite links for group {groupId}");
        }

        var group = await context.SharedAutomatonGroups
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            throw new InvalidOperationException($"Group {groupId} not found");
        }

        // Generate unique 8-character code
        var inviteCode = GenerateInviteCode();

        // Ensure uniqueness
        while (await context.SharedAutomatonGroups.AnyAsync(g => g.InviteCode == inviteCode))
        {
            inviteCode = GenerateInviteCode();
        }

        group.InviteCode = inviteCode;
        group.DefaultRoleForInvite = defaultRole;
        group.IsInviteLinkActive = true;
        group.InviteLinkExpiresAt = expirationDays.HasValue ? DateTime.UtcNow.AddDays(expirationDays.Value) : null;

        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} generated invite link for group {GroupId} with code {InviteCode}",
            userId, groupId, inviteCode);

        // Return the invite code (controller will build full URL)
        return inviteCode;
    }

    public async Task<string?> GetInviteLinkAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Check permission
        if (!await sharedAutomatonService.CanUserViewGroupAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to view group {groupId}");
        }

        var group = await context.SharedAutomatonGroups
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
            return null;

        if (!group.IsInviteLinkActive || string.IsNullOrEmpty(group.InviteCode))
            return null;

        // Check expiration
        if (group.InviteLinkExpiresAt.HasValue && group.InviteLinkExpiresAt.Value < DateTime.UtcNow)
        {
            group.IsInviteLinkActive = false;
            await context.SaveChangesAsync();
            return null;
        }

        return group.InviteCode;
    }

    public async Task DeactivateInviteLinkAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Check permission
        if (!await sharedAutomatonService.CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to deactivate invite link for group {groupId}");
        }

        var group = await context.SharedAutomatonGroups
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null)
        {
            throw new InvalidOperationException($"Group {groupId} not found");
        }

        group.IsInviteLinkActive = false;
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} deactivated invite link for group {GroupId}", userId, groupId);
    }

    public async Task<SharedAutomatonGroup?> JoinViaInviteLinkAsync(string inviteCode, string userId)
    {
        ArgumentNullException.ThrowIfNull(inviteCode);
        ArgumentNullException.ThrowIfNull(userId);

        var group = await context.SharedAutomatonGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode);

        if (group == null)
        {
            throw new InvalidOperationException($"Invalid invite code: {inviteCode}");
        }

        if (!group.IsInviteLinkActive)
        {
            throw new InvalidOperationException("Invite link is no longer active");
        }

        // Check expiration
        if (group.InviteLinkExpiresAt.HasValue && group.InviteLinkExpiresAt.Value < DateTime.UtcNow)
        {
            group.IsInviteLinkActive = false;
            await context.SaveChangesAsync();
            throw new InvalidOperationException("Invite link has expired");
        }

        // Check if already a member
        var existingMember = group.Members.FirstOrDefault(m => m.UserId == userId);
        if (existingMember != null)
        {
            throw new InvalidOperationException($"User is already a member of group {group.Name}");
        }

        // Add member with default role
        var member = new SharedAutomatonGroupMember
        {
            GroupId = group.Id,
            UserId = userId,
            Role = group.DefaultRoleForInvite,
            JoinedAt = DateTime.UtcNow,
            InvitedByUserId = null // Joined via link
        };

        context.SharedAutomatonGroupMembers.Add(member);
        await context.SaveChangesAsync();

        logger.LogInformation("User {UserId} joined group {GroupId} '{GroupName}' via invite link",
            userId, group.Id, group.Name);

        return group;
    }

    #endregion

    #region Email Content

    public Task<string> GetInvitationEmailContentAsync(SharedAutomatonGroupInvitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #007bff; color: white; padding: 20px; text-align: center; border-radius: 5px 5px 0 0; }}
        .content {{ background: #f8f9fa; padding: 30px; border: 1px solid #dee2e6; }}
        .button {{ display: inline-block; padding: 12px 24px; margin: 10px 5px; text-decoration: none; border-radius: 5px; font-weight: bold; }}
        .button-accept {{ background: #28a745; color: white; }}
        .button-decline {{ background: #dc3545; color: white; }}
        .info {{ background: white; padding: 15px; margin: 15px 0; border-left: 4px solid #007bff; }}
        .footer {{ text-align: center; padding: 20px; color: #6c757d; font-size: 12px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>?? Group Invitation</h1>
        </div>
        <div class='content'>
            <h2>You've been invited to join a shared automatons group!</h2>
            
            <div class='info'>
                <p><strong>Group:</strong> {invitation.Group?.Name ?? "Shared Group"}</p>
                <p><strong>Your Role:</strong> {GetRoleDisplayName(invitation.Role)}</p>
                <p><strong>Invited by:</strong> {invitation.InvitedByUserId}</p>
                {(invitation.ExpiresAt.HasValue ? $"<p><strong>Expires:</strong> {invitation.ExpiresAt.Value:g}</p>" : "")}
            </div>

            <h3>What you can do with this role:</h3>
            <ul>
                {GetRolePermissions(invitation.Role)}
            </ul>

            <p style='text-align: center; margin: 30px 0;'>
                <a href='{{ACCEPT_URL}}' class='button button-accept'>? Accept Invitation</a>
                <a href='{{DECLINE_URL}}' class='button button-decline'>? Decline</a>
            </p>

            <p style='color: #6c757d; font-size: 14px;'>
                This invitation will expire on {invitation.ExpiresAt:g}. 
                If you didn't expect this invitation, you can safely ignore it.
            </p>
        </div>
        <div class='footer'>
            <p>Finite Automatons Platform</p>
            <p>This is an automated message, please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

        return Task.FromResult(html);
    }

    #endregion

    #region Helper Methods

    private static string GenerateUniqueToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Avoiding ambiguous characters
        var random = new Random();
        var code = new StringBuilder(8);

        for (int i = 0; i < 8; i++)
        {
            code.Append(chars[random.Next(chars.Length)]);
        }

        return code.ToString();
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private static string GetRoleDisplayName(SharedGroupRole role)
    {
        return role switch
        {
            SharedGroupRole.Viewer => "Viewer (Read Only)",
            SharedGroupRole.Contributor => "Contributor (Can Add)",
            SharedGroupRole.Editor => "Editor (Can Modify)",
            SharedGroupRole.Admin => "Admin (Can Manage)",
            SharedGroupRole.Owner => "Owner (Full Control)",
            _ => role.ToString()
        };
    }

    private static string GetRolePermissions(SharedGroupRole role)
    {
        return role switch
        {
            SharedGroupRole.Viewer => "<li>View all automatons in the group</li>",
            SharedGroupRole.Contributor => "<li>View all automatons</li><li>Add new automatons to the group</li>",
            SharedGroupRole.Editor => "<li>View all automatons</li><li>Add new automatons</li><li>Edit and delete automatons</li>",
            SharedGroupRole.Admin => "<li>All Editor permissions</li><li>Manage group members</li><li>Invite new members</li>",
            SharedGroupRole.Owner => "<li>All Admin permissions</li><li>Delete the group</li><li>Transfer ownership</li>",
            _ => "<li>Unknown role</li>"
        };
    }

    #endregion
}
