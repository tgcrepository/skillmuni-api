using Learner.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learner.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable and the hand-checked migration deterministic.
// Generated from one spec together with the InitialCreate migration and the snapshot, so the three agree.
// PII columns: learners.first_name/last_name/phone/email/gender/date_of_birth, linked_identities.masked/last4/
// birthday/verified_name, enrolments.bank_* and upi (account number AES-GCM encrypted), quest_answers.answers,
// drafts.value, feedback.comment, job_profiles.about / cv_prefill.

internal static class Columns
{
    public const string Guid = "char(36)";
    public const int TenantLength = 64;

    public static void Keys<T>(EntityTypeBuilder<T> b)
        where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType(Guid).ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(TenantLength).IsRequired();
    }
}

internal sealed class LearnerProfileConfiguration : IEntityTypeConfiguration<LearnerProfile>
{
    public void Configure(EntityTypeBuilder<LearnerProfile> b)
    {
        b.ToTable("learners");
        Columns.Keys(b);
        b.Property(e => e.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        b.Property(e => e.Market).HasColumnName("market").HasMaxLength(2).IsRequired();
        b.Property(e => e.Phone).HasColumnName("phone").HasMaxLength(20);
        b.Property(e => e.Email).HasColumnName("email").HasMaxLength(254);
        b.Property(e => e.PhotoBlob).HasColumnName("photo_blob").HasMaxLength(200);
        b.Property(e => e.PhotoVersion).HasColumnName("photo_version");
        b.Property(e => e.Gender).HasColumnName("gender");
        b.Property(e => e.DateOfBirth).HasColumnName("date_of_birth").HasColumnType("date");
        b.Property(e => e.ProfileSkillsJson).HasColumnName("profile_skills").HasColumnType("json").IsRequired();
        b.Property(e => e.QuestsCompleted).HasColumnName("quests_completed");
        b.Property(e => e.PartsDone).HasColumnName("parts_done");
        b.Property(e => e.ProgrammeWrongTries).HasColumnName("programme_wrong_tries");
        b.Property(e => e.ProgrammeLockedUntil).HasColumnName("programme_locked_until");
        b.Property(e => e.SoundsOn).HasColumnName("sounds_on");
        b.Property(e => e.VoiceOn).HasColumnName("voice_on");
        b.Property(e => e.Language).HasColumnName("language").HasMaxLength(10).IsRequired();
        b.Property(e => e.Xp).HasColumnName("xp");
        b.Property(e => e.Level).HasColumnName("level");
        b.Property(e => e.Rank).HasColumnName("rank_name").HasMaxLength(40).IsRequired();
        b.Property(e => e.LevelStartXp).HasColumnName("level_start_xp");
        b.Property(e => e.NextLevelXp).HasColumnName("next_level_xp");
        b.Property(e => e.Badges).HasColumnName("badges");
        b.Property(e => e.StreakDays).HasColumnName("streak_days");
        b.Property(e => e.EstimatedXp).HasColumnName("estimated_xp");
        b.Property(e => e.EstimatedAt).HasColumnName("estimated_at");
        b.Property(e => e.ArchetypeName).HasColumnName("archetype_name").HasMaxLength(100);
        b.Property(e => e.ArchetypeSummary).HasColumnName("archetype_summary").HasMaxLength(1000);
        b.Property(e => e.JobProfileCompletedAt).HasColumnName("job_profile_completed_at");
        b.Property(e => e.JoinedAt).HasColumnName("joined_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => e.TenantId).HasDatabaseName("ix_learners_tenant");
    }
}

internal sealed class QuestAnswersConfiguration : IEntityTypeConfiguration<QuestAnswers>
{
    public void Configure(EntityTypeBuilder<QuestAnswers> b)
    {
        b.ToTable("quest_answers");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Slug).HasColumnName("slug").HasMaxLength(40).IsRequired();
        b.Property(e => e.AnswersJson).HasColumnName("answers").HasColumnType("json").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => new { e.LearnerId, e.Slug }).IsUnique().HasDatabaseName("ux_quest_answers_learner_slug");
    }
}

internal sealed class DraftConfiguration : IEntityTypeConfiguration<Draft>
{
    public void Configure(EntityTypeBuilder<Draft> b)
    {
        b.ToTable("drafts");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Key).HasColumnName("draft_key").HasMaxLength(100).IsRequired();
        b.Property(e => e.ValueJson).HasColumnName("value").HasColumnType("json").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => new { e.LearnerId, e.Key }).IsUnique().HasDatabaseName("ux_drafts_learner_key");
    }
}

internal sealed class LinkedIdentityConfiguration : IEntityTypeConfiguration<LinkedIdentity>
{
    public void Configure(EntityTypeBuilder<LinkedIdentity> b)
    {
        b.ToTable("linked_identities");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Document).HasColumnName("document").HasMaxLength(20).IsRequired();
        b.Property(e => e.Masked).HasColumnName("masked").HasMaxLength(40).IsRequired();
        b.Property(e => e.Last4).HasColumnName("last4").HasMaxLength(4).IsRequired();
        b.Property(e => e.Birthday).HasColumnName("birthday").HasColumnType("date");
        b.Property(e => e.VerifiedName).HasColumnName("verified_name").HasMaxLength(200);
        b.Property(e => e.VerifiedAt).HasColumnName("verified_at");

        b.HasIndex(e => e.LearnerId).IsUnique().HasDatabaseName("ux_linked_identities_learner");
    }
}

internal sealed class ProgrammeConfiguration : IEntityTypeConfiguration<Programme>
{
    public void Configure(EntityTypeBuilder<Programme> b)
    {
        b.ToTable("programmes");
        Columns.Keys(b);
        b.Property(e => e.Code).HasColumnName("code").HasMaxLength(40).IsRequired();
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(e => e.Partner).HasColumnName("partner").HasMaxLength(200).IsRequired();
        b.Property(e => e.Centre).HasColumnName("centre").HasMaxLength(200).IsRequired();
        b.Property(e => e.BatchStarts).HasColumnName("batch_starts").HasColumnType("date");
        b.Property(e => e.Duration).HasColumnName("duration").HasMaxLength(100).IsRequired();
        b.Property(e => e.Stipend).HasColumnName("stipend").HasMaxLength(100).IsRequired();
        b.Property(e => e.MarksFrom).HasColumnName("marks_from").HasMaxLength(2).IsRequired();
        b.Property(e => e.MinEnglish).HasColumnName("min_english");
        b.Property(e => e.MinMaths).HasColumnName("min_maths");
        b.Property(e => e.Eligibility).HasColumnName("eligibility").HasMaxLength(200).IsRequired();

        b.HasIndex(e => new { e.TenantId, e.Code }).IsUnique().HasDatabaseName("ux_programmes_tenant_code");
    }
}

internal sealed class EnrolmentConfiguration : IEntityTypeConfiguration<Enrolment>
{
    public void Configure(EntityTypeBuilder<Enrolment> b)
    {
        b.ToTable("enrolments");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.ProgrammeCode).HasColumnName("programme_code").HasMaxLength(40).IsRequired();
        b.Property(e => e.MarksFrom).HasColumnName("marks_from").HasMaxLength(2).IsRequired();
        b.Property(e => e.English).HasColumnName("english");
        b.Property(e => e.Maths).HasColumnName("maths");
        b.Property(e => e.ProficiencyJson).HasColumnName("proficiency").HasColumnType("json").IsRequired();
        b.Property(e => e.BankHolder).HasColumnName("bank_holder").HasMaxLength(100);
        b.Property(e => e.BankAccountType).HasColumnName("bank_account_type").HasMaxLength(10);
        b.Property(e => e.BankAccountEncrypted).HasColumnName("bank_account_encrypted").HasMaxLength(300);
        b.Property(e => e.BankAccountLast4).HasColumnName("bank_account_last4").HasMaxLength(4);
        b.Property(e => e.BankRouting).HasColumnName("bank_routing").HasMaxLength(20);
        b.Property(e => e.Upi).HasColumnName("upi").HasMaxLength(100);
        b.Property(e => e.SubmittedAt).HasColumnName("submitted_at");

        b.HasIndex(e => new { e.LearnerId, e.ProgrammeCode }).IsUnique().HasDatabaseName("ux_enrolments_learner_programme");
    }
}

internal sealed class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> b)
    {
        b.ToTable("feedback");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Mood).HasColumnName("mood");
        b.Property(e => e.TagsJson).HasColumnName("tags").HasColumnType("json").IsRequired();
        b.Property(e => e.Comment).HasColumnName("comment").HasMaxLength(2000).IsRequired();
        b.Property(e => e.Screen).HasColumnName("screen").HasMaxLength(200).IsRequired();
        b.Property(e => e.XpAwarded).HasColumnName("xp_awarded");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");

        b.HasIndex(e => new { e.LearnerId, e.XpAwarded, e.CreatedAt }).HasDatabaseName("ix_feedback_learner_xp");
    }
}

internal sealed class JobProfileSettingsConfiguration : IEntityTypeConfiguration<JobProfileSettings>
{
    public void Configure(EntityTypeBuilder<JobProfileSettings> b)
    {
        b.ToTable("job_profiles");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.About).HasColumnName("about").HasMaxLength(JobProfileSettings.MaxAboutLength);
        b.Property(e => e.HideContact).HasColumnName("hide_contact");
        b.Property(e => e.ExpiresDays).HasColumnName("expires_days");
        b.Property(e => e.ShareCode).HasColumnName("share_code").HasMaxLength(16).IsRequired();
        b.Property(e => e.CvPrefillJson).HasColumnName("cv_prefill").HasColumnType("json").IsRequired();
        b.Property(e => e.CvId).HasColumnName("cv_id").HasColumnType(Columns.Guid);
        b.Property(e => e.CvBlob).HasColumnName("cv_blob").HasMaxLength(200);
        b.Property(e => e.AppliedChangesJson).HasColumnName("applied_changes").HasColumnType("json").IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        b.HasIndex(e => e.LearnerId).IsUnique().HasDatabaseName("ux_job_profiles_learner");
        b.HasIndex(e => new { e.TenantId, e.ShareCode }).IsUnique().HasDatabaseName("ux_job_profiles_tenant_share_code");
    }
}

internal sealed class TrophyFileConfiguration : IEntityTypeConfiguration<TrophyFile>
{
    public void Configure(EntityTypeBuilder<TrophyFile> b)
    {
        b.ToTable("trophy_files");
        Columns.Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.BlobName).HasColumnName("blob_name").HasMaxLength(200).IsRequired();
        b.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(200).IsRequired();
        b.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        b.Property(e => e.Size).HasColumnName("size");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");

        b.HasIndex(e => e.LearnerId).HasDatabaseName("ix_trophy_files_learner");
    }
}
