using DataChat.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataChat.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("Documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.FilePath)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(d => d.FileHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(d => d.MimeType)
            .HasMaxLength(100);

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.CreatedBy)
            .HasMaxLength(256);

        builder.Property(d => d.UpdatedBy)
            .HasMaxLength(256);

        builder.HasIndex(d => d.DataSourceId);
        builder.HasIndex(d => d.Status);

        builder.HasOne(d => d.DataSource)
            .WithMany(ds => ds.Documents)
            .HasForeignKey(d => d.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Chunks)
            .WithOne(c => c.Document)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("DocumentChunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired();

        builder.Property(c => c.ContentHash)
            .HasMaxLength(64)
            .IsRequired();

        // Ignore Embedding property - handled via raw SQL in SqlServerVectorStore
        // SQL Server 2025 native VECTOR(1536) type is used with DiskANN index
        // EF Core doesn't natively support the VECTOR type, so we use raw SQL for I/O
        builder.Ignore(c => c.Embedding);

        builder.HasIndex(c => c.DocumentId);
    }
}
