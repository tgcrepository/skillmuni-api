using Careers.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Careers.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable. Every learner-owned table is indexed on learner_id
// (each screen reads one learner's rows); unique keys stop double bookmarks, double tickets and double offers.
// PII columns: cv_files.name (the learner's own file name), job_applications.cv_name and answers_json,
// invitations.note (the learner's reply), fest_bookings.code is a ticket, not PII.

internal static class Columns
{
    public const string Guid = "char(36)";
    public const int TenantLength = 64;
    public const int CatalogueIdLength = 40;

    public static void Keys<T>(EntityTypeBuilder<T> b)
        where T : Entity
    {
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType(Guid).ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(TenantLength).IsRequired();
    }

    public static void Owned<T>(EntityTypeBuilder<T> b)
        where T : LearnerOwned
    {
        Keys(b);
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Guid);
    }
}

internal sealed class CareerLearnerConfiguration : IEntityTypeConfiguration<CareerLearner>
{
    public void Configure(EntityTypeBuilder<CareerLearner> b)
    {
        b.ToTable("learners");
        Columns.Keys(b);
        b.Property(e => e.AssessmentDone).HasColumnName("assessment_done");
        b.Property(e => e.AssessmentAttempt).HasColumnName("assessment_attempt");
        b.Property(e => e.ArchetypeName).HasColumnName("archetype_name").HasMaxLength(120);
        b.Property(e => e.ArchetypeSummary).HasColumnName("archetype_summary").HasMaxLength(1000);
        b.Property(e => e.TopDriversJson).HasColumnName("top_drivers").HasColumnType("json").IsRequired();
        b.Property(e => e.AssessmentTakenAt).HasColumnName("assessment_taken_at");
        b.Property(e => e.JobProfileCompletedAt).HasColumnName("job_profile_completed_at");
        b.Property(e => e.ClaimConfirmedAt).HasColumnName("claim_confirmed_at");
        b.Property(e => e.JobsSeenAt).HasColumnName("jobs_seen_at");
        b.Property(e => e.JobAlertsOn).HasColumnName("job_alerts_on");
        b.Property(e => e.JobAlertsFrequency).HasColumnName("job_alerts_frequency").HasMaxLength(10).IsRequired();
        b.Property(e => e.JobAlertSearchesJson).HasColumnName("job_alert_searches").HasColumnType("json").IsRequired();
        b.Property(e => e.FestAlertsCity).HasColumnName("fest_alerts_city");
        b.Property(e => e.FestAlertsOnline).HasColumnName("fest_alerts_online");
        b.Property(e => e.PublishedAt).HasColumnName("published_at");
        b.Property(e => e.Discoverable).HasColumnName("discoverable");
        b.Property(e => e.InvitesAllowed).HasColumnName("invites_allowed");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Ignore(e => e.TopDrivers);
        b.Ignore(e => e.JobAlertSearches);
    }
}

internal sealed class SavedJobConfiguration : IEntityTypeConfiguration<SavedJob>
{
    public void Configure(EntityTypeBuilder<SavedJob> b)
    {
        b.ToTable("saved_jobs");
        Columns.Owned(b);
        b.Property(e => e.JobId).HasColumnName("job_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.SavedAt).HasColumnName("saved_at");
        b.HasIndex(e => new { e.LearnerId, e.JobId }).IsUnique().HasDatabaseName("ux_saved_jobs_learner_job");
    }
}

internal sealed class CvFileConfiguration : IEntityTypeConfiguration<CvFile>
{
    public void Configure(EntityTypeBuilder<CvFile> b)
    {
        b.ToTable("cv_files");
        Columns.Owned(b);
        b.Property(e => e.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        b.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        b.Property(e => e.SizeBytes).HasColumnName("size_bytes");
        b.Property(e => e.BlobName).HasColumnName("blob_name").HasMaxLength(200).IsRequired();
        b.Property(e => e.UploadedAt).HasColumnName("uploaded_at");
        b.HasIndex(e => new { e.LearnerId, e.UploadedAt }).HasDatabaseName("ix_cv_files_learner");
    }
}

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> b)
    {
        b.ToTable("job_applications");
        Columns.Owned(b);
        b.Property(e => e.JobId).HasColumnName("job_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.Title).HasColumnName("title").HasMaxLength(120);
        b.Property(e => e.CvId).HasColumnName("cv_id").HasColumnType(Columns.Guid);
        b.Property(e => e.CvName).HasColumnName("cv_name").HasMaxLength(160).IsRequired();
        b.Property(e => e.AnswersJson).HasColumnName("answers").HasColumnType("json").IsRequired();
        b.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        b.Property(e => e.Source).HasColumnName("source").HasMaxLength(20).IsRequired();
        b.Property(e => e.InvitationId).HasColumnName("invitation_id").HasColumnType(Columns.Guid);
        b.Property(e => e.FestOfferKey).HasColumnName("fest_offer_key").HasMaxLength(90);
        b.Property(e => e.AppliedAt).HasColumnName("applied_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Property(e => e.WithdrawnAt).HasColumnName("withdrawn_at");
        b.Ignore(e => e.Answers);
        b.Ignore(e => e.Active);
        b.HasIndex(e => new { e.LearnerId, e.JobId, e.AppliedAt }).HasDatabaseName("ix_job_applications_learner_job");
        b.HasIndex(e => new { e.LearnerId, e.FestOfferKey }).IsUnique().HasDatabaseName("ux_job_applications_fest_offer");
    }
}

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> b)
    {
        b.ToTable("invitations");
        Columns.Owned(b);
        b.Property(e => e.JobId).HasColumnName("job_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.RecruiterName).HasColumnName("recruiter_name").HasMaxLength(120).IsRequired();
        b.Property(e => e.RecruiterInitials).HasColumnName("recruiter_initials").HasMaxLength(4).IsRequired();
        b.Property(e => e.RecruiterTitle).HasColumnName("recruiter_title").HasMaxLength(160).IsRequired();
        b.Property(e => e.Type).HasColumnName("type").HasMaxLength(20).IsRequired();
        b.Property(e => e.InterviewAt).HasColumnName("interview_at");
        b.Property(e => e.Message).HasColumnName("message").HasMaxLength(2000).IsRequired();
        b.Property(e => e.RespondBy).HasColumnName("respond_by");
        b.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        b.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.RespondedAt).HasColumnName("responded_at");
        b.HasIndex(e => new { e.LearnerId, e.CreatedAt }).HasDatabaseName("ix_invitations_learner");
    }
}

internal sealed class FestBookingConfiguration : IEntityTypeConfiguration<FestBooking>
{
    public void Configure(EntityTypeBuilder<FestBooking> b)
    {
        b.ToTable("fest_bookings");
        Columns.Owned(b);
        b.Property(e => e.FestId).HasColumnName("fest_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        b.Property(e => e.RemindWhatsapp).HasColumnName("remind_whatsapp");
        b.Property(e => e.RemindSms).HasColumnName("remind_sms");
        b.Property(e => e.RemindCalendar).HasColumnName("remind_calendar");
        b.Property(e => e.CvLabel).HasColumnName("cv_label").HasMaxLength(80).IsRequired();
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.HasMany(e => e.Picks).WithOne().HasForeignKey(p => p.BookingId).OnDelete(DeleteBehavior.Cascade);
        b.Navigation(e => e.Picks).UsePropertyAccessMode(PropertyAccessMode.Field);

        // One ticket per learner and fest (BR-69); seat counts group by fest.
        b.HasIndex(e => new { e.LearnerId, e.FestId }).IsUnique().HasDatabaseName("ux_fest_bookings_learner_fest");
        b.HasIndex(e => new { e.TenantId, e.FestId }).HasDatabaseName("ix_fest_bookings_fest");
    }
}

internal sealed class FestPickConfiguration : IEntityTypeConfiguration<FestPick>
{
    public void Configure(EntityTypeBuilder<FestPick> b)
    {
        b.ToTable("fest_picks");
        Columns.Owned(b);
        b.Property(e => e.BookingId).HasColumnName("booking_id").HasColumnType(Columns.Guid);
        b.Property(e => e.FestId).HasColumnName("fest_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.EmployerId).HasColumnName("employer_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.Date).HasColumnName("date");
        b.Property(e => e.Time).HasColumnName("time").HasMaxLength(5).IsRequired();
        b.HasIndex(e => new { e.BookingId, e.EmployerId }).IsUnique().HasDatabaseName("ux_fest_picks_booking_employer");
        b.HasIndex(e => new { e.TenantId, e.FestId, e.EmployerId, e.Date, e.Time }).HasDatabaseName("ix_fest_picks_slot");
        b.HasIndex(e => e.LearnerId).HasDatabaseName("ix_fest_picks_learner");
    }
}

internal sealed class FestWaitlistConfiguration : IEntityTypeConfiguration<FestWaitlistEntry>
{
    public void Configure(EntityTypeBuilder<FestWaitlistEntry> b)
    {
        b.ToTable("fest_waitlist");
        Columns.Owned(b);
        b.Property(e => e.FestId).HasColumnName("fest_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.EmployerId).HasColumnName("employer_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.AutoBook).HasColumnName("auto_book");
        b.Property(e => e.Whatsapp).HasColumnName("whatsapp");
        b.Property(e => e.JoinedAt).HasColumnName("joined_at");
        b.HasIndex(e => new { e.LearnerId, e.FestId, e.EmployerId }).IsUnique().HasDatabaseName("ux_fest_waitlist_learner_employer");
        b.HasIndex(e => new { e.TenantId, e.FestId, e.EmployerId, e.JoinedAt }).HasDatabaseName("ix_fest_waitlist_queue");
    }
}

internal sealed class FestOutcomeConfiguration : IEntityTypeConfiguration<FestOutcome>
{
    public void Configure(EntityTypeBuilder<FestOutcome> b)
    {
        b.ToTable("fest_outcomes");
        Columns.Owned(b);
        b.Property(e => e.FestId).HasColumnName("fest_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.EmployerId).HasColumnName("employer_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.Outcome).HasColumnName("outcome").HasMaxLength(10).IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(e => new { e.LearnerId, e.FestId, e.EmployerId }).IsUnique().HasDatabaseName("ux_fest_outcomes_learner_interview");
    }
}

internal sealed class FestRatingConfiguration : IEntityTypeConfiguration<FestRating>
{
    public void Configure(EntityTypeBuilder<FestRating> b)
    {
        b.ToTable("fest_ratings");
        Columns.Owned(b);
        b.Property(e => e.FestId).HasColumnName("fest_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.Stars).HasColumnName("stars");
        b.Property(e => e.RatedAt).HasColumnName("rated_at");
        b.HasIndex(e => new { e.LearnerId, e.FestId }).IsUnique().HasDatabaseName("ux_fest_ratings_learner_fest");
    }
}

internal sealed class SavedFestConfiguration : IEntityTypeConfiguration<SavedFest>
{
    public void Configure(EntityTypeBuilder<SavedFest> b)
    {
        b.ToTable("saved_fests");
        Columns.Owned(b);
        b.Property(e => e.FestId).HasColumnName("fest_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.SavedAt).HasColumnName("saved_at");
        b.HasIndex(e => new { e.LearnerId, e.FestId }).IsUnique().HasDatabaseName("ux_saved_fests_learner_fest");
    }
}

internal sealed class MatchStateConfiguration : IEntityTypeConfiguration<MatchState>
{
    public void Configure(EntityTypeBuilder<MatchState> b)
    {
        b.ToTable("match_states");
        Columns.Owned(b);
        b.Property(e => e.MatchId).HasColumnName("match_id").HasMaxLength(Columns.CatalogueIdLength).IsRequired();
        b.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(e => new { e.LearnerId, e.MatchId }).IsUnique().HasDatabaseName("ux_match_states_learner_match");
    }
}
