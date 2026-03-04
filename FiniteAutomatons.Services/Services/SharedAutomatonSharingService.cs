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
    UserManager<ApplicationUser> userManager,
    ILogger<SharedAutomatonSharingService> logger) : ISharedAutomatonSharingService
{
    private readonly ApplicationDbContext context = context;
    private readonly ISharedAutomatonService sharedAutomatonService = sharedAutomatonService;
    private readonly UserManager<ApplicationUser> userManager = userManager;
    private readonly ILogger<SharedAutomatonSharingService> logger = logger;

    public async Task<SharedAutomatonGroupInvitation> InviteByEmailAsync(int groupId, string inviterUserId, string email, SharedGroupRole role, int expirationDays = 7)
    {
        ValidateInviteParameters(inviterUserId, email);
        await ValidateInvitePermissionsAsync(groupId, inviterUserId);
        await ValidateNoExistingMemberAsync(groupId, email);
        await ValidateNoPendingInvitationAsync(groupId, email);

        var invitation = CreateInvitation(groupId, inviterUserId, email, role, expirationDays);

        context.SharedAutomatonGroupInvitations.Add(invitation);
        await context.SaveChangesAsync();

        LogInvitationCreated(inviterUserId, email, groupId, role);
        return invitation;
    }

    private static void ValidateInviteParameters(string inviterUserId, string email)
    {
        ArgumentNullException.ThrowIfNull(inviterUserId);
        ArgumentNullException.ThrowIfNull(email);

        if (!IsValidEmail(email))
        {
            throw new ArgumentException($"Invalid email format: {email}");
        }
    }

    private async Task ValidateInvitePermissionsAsync(int groupId, string inviterUserId)
    {
        if (!await sharedAutomatonService.CanUserManageMembersAsync(groupId, inviterUserId))
        {
            throw new UnauthorizedAccessException($"User {inviterUserId} does not have permission to invite members to group {groupId}");
        }
    }

    private async Task ValidateNoExistingMemberAsync(int groupId, string email)
    {
        var existingMember = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == email);

        if (existingMember != null)
        {
            throw new InvalidOperationException($"User {email} is already a member of group {groupId}");
        }
    }

    private async Task ValidateNoPendingInvitationAsync(int groupId, string email)
    {
        var existingInvitation = await context.SharedAutomatonGroupInvitations
            .FirstOrDefaultAsync(i => i.GroupId == groupId && i.Email == email && i.Status == InvitationStatus.Pending);

        if (existingInvitation != null)
        {
            throw new InvalidOperationException($"There is already a pending invitation for {email} to group {groupId}");
        }
    }

    private static SharedAutomatonGroupInvitation CreateInvitation(int groupId, string inviterUserId, string email, SharedGroupRole role, int expirationDays)
    {
        var token = GenerateUniqueToken();

        return new SharedAutomatonGroupInvitation
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
    }

    private void LogInvitationCreated(string inviterUserId, string email, int groupId, SharedGroupRole role)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {InviterUserId} invited {Email} to group {GroupId} with role {Role}",
                inviterUserId, email, groupId, role);
        }
    }

    public async Task<List<SharedAutomatonGroupInvitation>> ListPendingInvitationsAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

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

        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
        {
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

        var invitation = await LoadAndValidateInvitationAsync(token);
        await ValidateUserMatchesInvitationAsync(userId, invitation);
        await ValidateNotAlreadyMemberAsync(userId, invitation.GroupId);

        await AddMemberFromInvitationAsync(userId, invitation);
        MarkInvitationAsAccepted(invitation);

        await context.SaveChangesAsync();
        LogInvitationAccepted(userId, invitation.Id, invitation.GroupId);
    }

    private async Task<SharedAutomatonGroupInvitation> LoadAndValidateInvitationAsync(string token)
    {
        var invitation = await GetInvitationByTokenAsync(token)
            ?? throw new InvalidOperationException("Invitation not found");

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

        return invitation;
    }

    private async Task ValidateUserMatchesInvitationAsync(string userId, SharedAutomatonGroupInvitation invitation)
    {
        var user = await userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found");

        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException($"Invitation email ({invitation.Email}) does not match user email ({user.Email})");
        }
    }

    private async Task ValidateNotAlreadyMemberAsync(string userId, int groupId)
    {
        var existingMember = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (existingMember != null)
        {
            throw new InvalidOperationException($"User is already a member of group {groupId}");
        }
    }

    private async Task AddMemberFromInvitationAsync(string userId, SharedAutomatonGroupInvitation invitation)
    {
        var member = new SharedAutomatonGroupMember
        {
            GroupId = invitation.GroupId,
            UserId = userId,
            Role = invitation.Role,
            JoinedAt = DateTime.UtcNow,
            InvitedByUserId = invitation.InvitedByUserId
        };

        context.SharedAutomatonGroupMembers.Add(member);
    }

    private static void MarkInvitationAsAccepted(SharedAutomatonGroupInvitation invitation)
    {
        invitation.Status = InvitationStatus.Accepted;
        invitation.ResponsedAt = DateTime.UtcNow;
    }

    private void LogInvitationAccepted(string userId, int invitationId, int groupId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} accepted invitation {InvitationId} and joined group {GroupId}",
                userId, invitationId, groupId);
        }
    }

    public async Task DeclineInvitationAsync(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var invitation = await GetInvitationByTokenAsync(token) ?? throw new InvalidOperationException("Invitation not found");
        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException($"Invitation is not pending (status: {invitation.Status})");
        }

        invitation.Status = InvitationStatus.Declined;
        invitation.ResponsedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Invitation {InvitationId} for email {Email} was declined",
            invitation.Id, invitation.Email);
        }
    }

    public async Task CancelInvitationAsync(int invitationId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var invitation = await context.SharedAutomatonGroupInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId) ?? throw new InvalidOperationException($"Invitation {invitationId} not found");

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

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} cancelled invitation {InvitationId} for email {Email}",
            userId, invitationId, invitation.Email);
        }
    }

    public async Task<string> GenerateInviteLinkAsync(int groupId, string userId, SharedGroupRole defaultRole, int? expirationDays = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        await ValidateCanManageMembersAsync(groupId, userId);
        var group = await LoadGroupAsync(groupId);
        var inviteCode = await GenerateUniqueInviteCodeAsync();

        UpdateGroupInviteSettings(group, inviteCode, defaultRole, expirationDays);
        await context.SaveChangesAsync();

        LogInviteLinkGenerated(userId, groupId, inviteCode);
        return inviteCode;
    }

    private async Task ValidateCanManageMembersAsync(int groupId, string userId)
    {
        if (!await sharedAutomatonService.CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to generate invite links for group {groupId}");
        }
    }

    private async Task<SharedAutomatonGroup> LoadGroupAsync(int groupId)
    {
        return await context.SharedAutomatonGroups
            .FirstOrDefaultAsync(g => g.Id == groupId)
            ?? throw new InvalidOperationException($"Group {groupId} not found");
    }

    private async Task<string> GenerateUniqueInviteCodeAsync()
    {
        var inviteCode = GenerateInviteCode();

        while (await context.SharedAutomatonGroups.AnyAsync(g => g.InviteCode == inviteCode))
        {
            inviteCode = GenerateInviteCode();
        }

        return inviteCode;
    }

    private static void UpdateGroupInviteSettings(SharedAutomatonGroup group, string inviteCode, SharedGroupRole defaultRole, int? expirationDays)
    {
        group.InviteCode = inviteCode;
        group.DefaultRoleForInvite = defaultRole;
        group.IsInviteLinkActive = true;
        group.InviteLinkExpiresAt = expirationDays.HasValue ? DateTime.UtcNow.AddDays(expirationDays.Value) : null;
    }

    private void LogInviteLinkGenerated(string userId, int groupId, string inviteCode)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} generated invite link for group {GroupId} with code {InviteCode}",
                userId, groupId, inviteCode);
        }
    }

    public async Task<string?> GetInviteLinkAsync(int groupId, string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);

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

        if (!await sharedAutomatonService.CanUserManageMembersAsync(groupId, userId))
        {
            throw new UnauthorizedAccessException($"User {userId} does not have permission to deactivate invite link for group {groupId}");
        }

        var group = await context.SharedAutomatonGroups
            .FirstOrDefaultAsync(g => g.Id == groupId) ?? throw new InvalidOperationException($"Group {groupId} not found");
        group.IsInviteLinkActive = false;
        await context.SaveChangesAsync();

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} deactivated invite link for group {GroupId}", userId, groupId);
        }
    }

    public async Task<SharedAutomatonGroup?> JoinViaInviteLinkAsync(string inviteCode, string userId)
    {
        ArgumentNullException.ThrowIfNull(inviteCode);
        ArgumentNullException.ThrowIfNull(userId);

        var group = await LoadGroupByInviteCodeAsync(inviteCode);
        await ValidateInviteLinkActiveAsync(group);
        ValidateNotExistingMember(group, userId);

        await AddMemberViaInviteLinkAsync(group, userId);
        LogUserJoinedViaLink(userId, group.Id, group.Name);

        return group;
    }

    private async Task<SharedAutomatonGroup> LoadGroupByInviteCodeAsync(string inviteCode)
    {
        return await context.SharedAutomatonGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.InviteCode == inviteCode)
            ?? throw new InvalidOperationException($"Invalid invite code: {inviteCode}");
    }

    private async Task ValidateInviteLinkActiveAsync(SharedAutomatonGroup group)
    {
        if (!group.IsInviteLinkActive)
        {
            throw new InvalidOperationException("Invite link is no longer active");
        }

        if (group.InviteLinkExpiresAt.HasValue && group.InviteLinkExpiresAt.Value < DateTime.UtcNow)
        {
            group.IsInviteLinkActive = false;
            await context.SaveChangesAsync();
            throw new InvalidOperationException("Invite link has expired");
        }
    }

    private static void ValidateNotExistingMember(SharedAutomatonGroup group, string userId)
    {
        var existingMember = group.Members.FirstOrDefault(m => m.UserId == userId);
        if (existingMember != null)
        {
            throw new InvalidOperationException($"User is already a member of group {group.Name}");
        }
    }

    private async Task AddMemberViaInviteLinkAsync(SharedAutomatonGroup group, string userId)
    {
        var member = new SharedAutomatonGroupMember
        {
            GroupId = group.Id,
            UserId = userId,
            Role = group.DefaultRoleForInvite,
            JoinedAt = DateTime.UtcNow,
            InvitedByUserId = null
        };

        context.SharedAutomatonGroupMembers.Add(member);
        await context.SaveChangesAsync();
    }

    private void LogUserJoinedViaLink(string userId, int groupId, string groupName)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("User {UserId} joined group {GroupId} '{GroupName}' via invite link",
                userId, groupId, groupName);
        }
    }

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
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
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
}
