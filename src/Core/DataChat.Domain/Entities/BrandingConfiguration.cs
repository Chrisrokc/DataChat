namespace DataChat.Domain.Entities;

public class BrandingConfiguration
{
    public int Id { get; set; } = 1; // Single row
    public string ApplicationName { get; set; } = "Enterprise Chat";
    public string? LogoPath { get; set; }
    public string PrimaryColor { get; set; } = "#1976D2";
    public string SecondaryColor { get; set; } = "#424242";
    public string AccentColor { get; set; } = "#FF4081";
    public string? FooterText { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
