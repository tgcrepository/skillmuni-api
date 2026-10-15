using Cv.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cv.Infrastructure.Persistence;

// Explicit snake_case names and types keep the schema readable and the hand-checked migration deterministic.
// Generated from one spec together with the InitialCreate migration and the snapshot, so the three agree.
// PII columns: cv_documents.file_name and extract (names, contact, education, work). Never the raw text.

internal static class Columns
{
    public const string Guid = "char(36)";
    public const int TenantLength = 64;
}

internal sealed class CvDocumentConfiguration : IEntityTypeConfiguration<CvDocument>
{
    public void Configure(EntityTypeBuilder<CvDocument> b)
    {
        b.ToTable("cv_documents");
        b.HasKey(e => e.Id);
        b.Property(e => e.Id).HasColumnName("id").HasColumnType(Columns.Guid).ValueGeneratedNever();
        b.Property(e => e.TenantId).HasColumnName("tenant_id").HasMaxLength(Columns.TenantLength).IsRequired();
        b.Property(e => e.LearnerId).HasColumnName("learner_id").HasColumnType(Columns.Guid);
        b.Property(e => e.Market).HasColumnName("market").HasMaxLength(2).IsRequired();
        b.Property(e => e.FileName).HasColumnName("file_name").HasMaxLength(200).IsRequired();
        b.Property(e => e.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        b.Property(e => e.Size).HasColumnName("size");
        b.Property(e => e.BlobNamesJson).HasColumnName("blob_names").HasColumnType("json").IsRequired();
        b.Property(e => e.State).HasColumnName("state");
        b.Property(e => e.Step).HasColumnName("step");
        b.Property(e => e.Quip).HasColumnName("quip").HasMaxLength(200);
        b.Property(e => e.Outcome).HasColumnName("outcome").HasMaxLength(40);
        b.Property(e => e.Detail).HasColumnName("detail").HasMaxLength(100);
        b.Property(e => e.ExtractJson).HasColumnName("extract").HasColumnType("json");
        b.Property(e => e.Attempts).HasColumnName("attempts");
        b.Property(e => e.ClaimedAt).HasColumnName("claimed_at");
        b.Property(e => e.CreatedAt).HasColumnName("created_at");
        b.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        b.Property(e => e.CompletedAt).HasColumnName("completed_at");

        b.HasIndex(e => new { e.LearnerId, e.CreatedAt }).HasDatabaseName("ix_cv_documents_learner");
        b.HasIndex(e => new { e.State, e.CreatedAt }).HasDatabaseName("ix_cv_documents_queue");
    }
}
